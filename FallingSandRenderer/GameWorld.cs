using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Arch.Core;
using FallingSand.Entity.System;
using FallingSand.WorldGenerator;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.FallingSandRenderer;

class GameWorld
{
    private const int TARGET_SAND_FPS = 60;
    private readonly ConcurrentDictionary<ChunkPosition, GameChunk> gameChunks = new(
        2,
        Constants.INITIAL_CHUNK_POOL_SIZE
    );
    public FallingSandWorld.FallingSandWorld sandWorld;
    public nkast.Aether.Physics2D.Dynamics.World physicsWorld;
    private SpriteBatch spriteBatch;
    private GameChunkPool gameChunkPool;
    private readonly AsyncChunkGenerator asyncChunkGenerator;
    private readonly AsyncChunkPhysicsCalculator asyncChunkPhysicsCalculator;
    private Effect waterShaderEffect;

    private readonly DoEvery createNewChunksTimer;
    private readonly DoEvery updateGameWorldTimer;
    private readonly MaterialTextureSampler materialTextureSampler = new();
    private readonly List<(ChunkPosition, GameChunk)> visibleChunks = new(
        Constants.INITIAL_CHUNK_POOL_SIZE
    );

    private GeneratedWorldInstance worldTiles;

    public GameWorld()
    {
        asyncChunkGenerator = new AsyncChunkGenerator(2);
        asyncChunkPhysicsCalculator = new AsyncChunkPhysicsCalculator(2);
        createNewChunksTimer = new DoEvery(CreateNewChunks, 10);
        updateGameWorldTimer = new DoEvery(UpdateGameWorld, 1000 / TARGET_SAND_FPS);
    }

    public void GenerateInitialChunks()
    {
        // Generate initial chunks
        foreach (var (_, chunk) in GetCameraGameChunks(true))
        {
            chunk.Generate(true);
            chunk.UpdatePhysicsPolygons();
            chunk.CreatePhysicsBodies();
        }
    }

    public void Init(
        string seed,
        SpriteBatch spriteBatch,
        GraphicsDevice graphicsDevice,
        nkast.Aether.Physics2D.Dynamics.World physicsWorld,
        FallingSandWorld.FallingSandWorld sandWorld,
        Game game
    )
    {
        var generator = new WorldGenerationManager();
        generator.LoadAssets();
        materialTextureSampler.Load();
        worldTiles = generator.GenerateWorld(seed, "Caves", 500, 500, []);

        if (!worldTiles.IsValid())
        {
            var violations = worldTiles.ValidateConstraints();
            Console.WriteLine(
                $"Found {violations.Count} constraint violations in the generated world."
            );
        }

        this.sandWorld = sandWorld;
        this.physicsWorld = physicsWorld;

        this.spriteBatch = spriteBatch;

        // Load the water shader
        waterShaderEffect = null; // game.Content.Load<Effect>("Shaders/WaterEffect");

        var pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        pixelTexture.SetData([Color.White]);

        gameChunkPool = new GameChunkPool(
            graphicsDevice,
            pixelTexture,
            spriteBatch,
            sandWorld,
            worldTiles,
            physicsWorld,
            materialTextureSampler,
            waterShaderEffect
        );
        gameChunkPool.Initialize(Constants.INITIAL_CHUNK_POOL_SIZE);

        GenerateInitialChunks();

        asyncChunkGenerator.Start();
        asyncChunkPhysicsCalculator.Start();
    }

    public void CreateNewChunks()
    {
        // Loop through all chunks and create new ones if they are not loaded
        foreach (var (_, chunk) in gameChunks)
        {
            if (!chunk.HasGeneratedMap)
            {
                asyncChunkGenerator.Enqueue(chunk);
                // break; // Only request one chunk per update
            }
        }
    }

    private readonly Queue<ChunkPosition> chunkUnloadQueue = new(Constants.INITIAL_CHUNK_POOL_SIZE);
    private const int MAX_CHUNKS_TO_UNLOAD = 2;

    public void UnloadOffScreenChunks()
    {
        // Find all chunks that are off screen and unload them
        var (cameraStart, cameraEnd) = Camera.GetVisibleArea();

        // Padding to avoid unloading chunks that are just off screen
        var paddingAmount = Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;

        // Calculate positions to unload which are outside the camera view with padding
        foreach (var (position, _) in gameChunks)
        {
            if (
                position.X
                    < (int)Math.Floor(cameraStart.X / (float)Constants.CHUNK_WIDTH) - paddingAmount
                || position.X
                    > (int)Math.Ceiling(cameraEnd.X / (float)Constants.CHUNK_WIDTH) + paddingAmount
                || position.Y
                    < (int)Math.Floor(cameraStart.Y / (float)Constants.CHUNK_HEIGHT) - paddingAmount
                || position.Y
                    > (int)Math.Ceiling(cameraEnd.Y / (float)Constants.CHUNK_HEIGHT) + paddingAmount
            )
            {
                if (!chunkUnloadQueue.Contains(position))
                {
                    chunkUnloadQueue.Enqueue(position);
                }
            }
        }

        int processed = 0;
        while (chunkUnloadQueue.Count > 0 && processed < MAX_CHUNKS_TO_UNLOAD)
        {
            var position = chunkUnloadQueue.Dequeue();
            if (gameChunks.TryRemove(position, out var chunk))
            {
                chunk.Unload();
                gameChunkPool.Return(chunk);
            }
            processed++;
        }
    }

    public void UpdateGameWorld()
    {
        // Get the visible world area with additional padding for off-screen updates
        var (visibleStart, visibleEnd) = Camera.GetVisibleArea();
        var start = visibleStart;
        var end = visibleEnd;

        // Add some padding to the start and end positions
        var paddingAmount = Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        start = new WorldPosition(
            start.X - Constants.CHUNK_WIDTH * paddingAmount,
            start.Y - Constants.CHUNK_HEIGHT * paddingAmount
        );
        end = new WorldPosition(
            end.X + Constants.CHUNK_WIDTH * paddingAmount,
            end.Y + Constants.CHUNK_HEIGHT * paddingAmount
        );

        sandWorld.Update(start, end);
        // sandWorld.UpdateSynchronously(start, end);
    }

    public void Update(GameTime gameTime)
    {
        createNewChunksTimer.Update(gameTime);
        updateGameWorldTimer.Update(gameTime);

        visibleChunks.Clear();
        visibleChunks.AddRange(GetCameraGameChunks(true));

        UnloadOffScreenChunks();

        foreach (var (_, chunk) in visibleChunks)
        {
            if (chunk.SandChunk == null || !chunk.SandChunk.isAwake)
            {
                continue;
            }

            if (chunk.IsCalculatingPhysics)
            {
                continue;
            }

            asyncChunkPhysicsCalculator.Enqueue(chunk);
            chunk.CreatePhysicsBodies();
        }
    }

    public void DrawRenderTargets()
    {
        // Draw to the render targets
        foreach (var (_, chunk) in visibleChunks)
        {
            chunk.Draw(Camera.GameTime);
        }
    }

    public void Draw(GameTime gameTime)
    {
        // Draw the render targets to the screen
        spriteBatch.Begin(
            transformMatrix: Camera.GetTransformMatrix(),
            samplerState: SamplerState.PointWrap,
            blendState: BlendState.AlphaBlend
        );
        foreach (var (_, chunk) in visibleChunks)
        {
            spriteBatch.Draw(
                chunk.RenderTarget,
                new Rectangle(
                    chunk.WorldOrigin.X,
                    chunk.WorldOrigin.Y,
                    Constants.CHUNK_WIDTH,
                    Constants.CHUNK_HEIGHT
                ),
                Color.White
            );
        }

        spriteBatch.End();
    }

    public IEnumerable<(ChunkPosition, GameChunk)> GetCameraGameChunks(bool createMissing = false)
    {
        // Returns all chunks visible by the camera
        var (cameraStart, cameraEnd) = Camera.GetVisibleArea();

        // Calculate the starting and ending chunk coordinates with buffer
        var startChunkX =
            (int)Math.Floor(cameraStart.X / (float)Constants.CHUNK_WIDTH)
            - Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        var startChunkY =
            (int)Math.Floor(cameraStart.Y / (float)Constants.CHUNK_HEIGHT)
            - Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;

        var endChunkX =
            (int)Math.Ceiling(cameraEnd.X / (float)Constants.CHUNK_WIDTH)
            + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        var endChunkY =
            (int)Math.Ceiling(cameraEnd.Y / (float)Constants.CHUNK_HEIGHT)
            + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;

        // Iterate through chunks in view
        for (int chunkX = startChunkX; chunkX < endChunkX; chunkX++)
        {
            for (int chunkY = startChunkY; chunkY < endChunkY; chunkY++)
            {
                // Skip negative chunks
                if (chunkX < 0 || chunkY < 0)
                {
                    continue;
                }

                var chunkPosition = new ChunkPosition(chunkX, chunkY);

                if (!createMissing && !gameChunks.ContainsKey(chunkPosition))
                {
                    continue;
                }

                yield return (chunkPosition, GetOrCreateChunkFromChunkPosition(chunkPosition));
            }
        }
    }

    public GameChunk GetOrCreateChunk(WorldPosition worldPosition)
    {
        var chunkPosition = new ChunkPosition(
            (int)Math.Floor((float)worldPosition.X / Constants.CHUNK_WIDTH),
            (int)Math.Floor((float)worldPosition.Y / Constants.CHUNK_HEIGHT)
        );

        return GetOrCreateChunkFromChunkPosition(chunkPosition);
    }

    public GameChunk GetOrCreateChunkFromChunkPosition(ChunkPosition chunkPosition)
    {
        if (!gameChunks.TryGetValue(chunkPosition, out var chunk))
        {
            var newChunkWorldPosition = new WorldPosition(
                chunkPosition.X * Constants.CHUNK_WIDTH,
                chunkPosition.Y * Constants.CHUNK_HEIGHT
            );
            return gameChunks.AddOrUpdate(
                key: chunkPosition,
                addValueFactory: (key) => gameChunkPool.Get(newChunkWorldPosition),
                updateValueFactory: (key, oldValue) =>
                {
                    // If the chunk is already in the dictionary, return the existing chunk
                    return oldValue;
                }
            );
        }

        return chunk;
    }

    public FallingSandPixel GetPixelAtWorldPosition(WorldPosition worldPosition)
    {
        return sandWorld.GetPixel(worldPosition);
    }
}
