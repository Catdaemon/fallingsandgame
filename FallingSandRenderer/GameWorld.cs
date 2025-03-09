using System;
using System.Collections.Generic;
using Arch.Core;
using FallingSand.Entity.System;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.FallingSandRenderer;

class GameWorld
{
    private const int TARGET_SAND_FPS = 60;
    private readonly Dictionary<ChunkPosition, GameChunk> gameChunks = [];
    public FallingSandWorld.FallingSandWorld sandWorld;
    public nkast.Aether.Physics2D.Dynamics.World physicsWorld;
    private SpriteBatch spriteBatch;
    private GameChunkPool gameChunkPool;
    private readonly AsyncChunkGenerator asyncChunkGenerator;

    private readonly DoEvery createNewChunksTimer;
    private readonly DoEvery updateGameWorldTimer;

    public GameWorld()
    {
        asyncChunkGenerator = new AsyncChunkGenerator(this);
        createNewChunksTimer = new DoEvery(CreateNewChunks, 100);
        updateGameWorldTimer = new DoEvery(UpdateGameWorld, 1000 / TARGET_SAND_FPS);
    }

    public void Init(
        string seed,
        SpriteBatch spriteBatch,
        GraphicsDevice graphicsDevice,
        nkast.Aether.Physics2D.Dynamics.World physicsWorld,
        FallingSandWorld.FallingSandWorld sandWorld
    )
    {
        var generator = new FallingSandWorldGenerator(
            seed,
            new WorldGeneratorConfig()
            {
                TerrainScale = 0.0005f,
                BiomeScale = 0.1f,
                OctaveCount = 2,
                BiomeTransitionSize = 0.2f,
            }
        );
        this.sandWorld = sandWorld;
        this.physicsWorld = physicsWorld;

        this.spriteBatch = spriteBatch;

        gameChunkPool = new GameChunkPool(
            graphicsDevice,
            spriteBatch,
            sandWorld,
            generator,
            physicsWorld
        );
        gameChunkPool.Initialize(100);

        asyncChunkGenerator.Start();
    }

    public void CreateNewChunks()
    {
        // Loop through all chunks and create new ones if they are not loaded
        foreach (var (position, chunk) in gameChunks)
        {
            if (!chunk.HasGeneratedMap)
            {
                asyncChunkGenerator.EnqueueChunk(position);
                break; // Only request one chunk per update
            }
        }
    }

    public void UnloadOffScreenChunks()
    {
        // Find all chunks that are off screen and unload them
        var (cameraStart, cameraEnd) = Camera.GetVisibleArea();

        // Padding to avoid unloading chunks that are just off screen
        var paddingAmount = Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;

        // Calculate positions to unload which are outside the camera view with padding
        var positionsToUnload = new List<ChunkPosition>();
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
                positionsToUnload.Add(position);
            }
        }

        // Now unload them
        foreach (var position in positionsToUnload)
        {
            if (gameChunks.TryGetValue(position, out var chunk))
            {
                chunk.Unload();
                gameChunkPool.Return(chunk);
                gameChunks.Remove(position);
            }
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
    }

    public void Update(GameTime gameTime)
    {
        UnloadOffScreenChunks();
        createNewChunksTimer.Update(gameTime);
        updateGameWorldTimer.Update(gameTime);

        var chunks = GetCameraGameChunks(true);

        var foundPositions = new HashSet<WorldPosition>();
        foreach (var (_, chunk) in chunks)
        {
            if (foundPositions.Contains(chunk.WorldOrigin))
            {
                Console.WriteLine(
                    "Warning: Duplicate chunk found in update list at {0}, {1}",
                    chunk.WorldOrigin.X,
                    chunk.WorldOrigin.Y
                );
                continue;
            }
            foundPositions.Add(chunk.WorldOrigin);
        }

        foreach (var (pos, chunk) in chunks)
        {
            // Update the chunk every so often
            if (chunk.lastUpdateTime > gameTime.TotalGameTime.TotalMilliseconds - 300)
            {
                continue;
            }
            asyncChunkGenerator.EnqueueChunk(pos);
            chunk.Update(gameTime);
            chunk.lastUpdateTime = gameTime.TotalGameTime.TotalMilliseconds;
        }
    }

    public void Draw(GameTime gameTime)
    {
        var visibleChunks = GetCameraGameChunks(false);

        // Draw to the render targets
        foreach (var (_, chunk) in visibleChunks)
        {
            chunk.Draw();
        }

        // Draw the render targets to the screen
        spriteBatch.Begin(
            transformMatrix: Camera.GetTransformMatrix(),
            samplerState: SamplerState.PointWrap
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
                Microsoft.Xna.Framework.Color.White
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
            chunk = gameChunkPool.Get(newChunkWorldPosition);
            gameChunks[chunkPosition] = chunk;
        }

        return chunk;
    }

    public FallingSandPixel GetPixelAtWorldPosition(WorldPosition worldPosition)
    {
        return sandWorld.GetPixel(worldPosition);
    }
}
