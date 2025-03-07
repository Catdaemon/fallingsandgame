using System;
using System.Collections.Generic;
using fallingsand.nosync;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameWorld;

class GameWorld
{
    private const int TARGET_SAND_FPS = 60;
    private readonly Dictionary<ChunkPosition, GameChunk> gameChunks = [];
    private GraphicsDevice graphicsDevice;
    private FallingSandWorldGenerator generator;
    public FallingSandWorld.FallingSandWorld sandWorld;
    private SpriteBatch spriteBatch;
    private readonly AsyncChunkGenerator asyncChunkGenerator;

    private readonly DoEvery unloadOffScreenChunksTimer;
    private readonly DoEvery createNewChunksTimer;
    private readonly DoEvery updateGameWorldTimer;

    public GameWorld()
    {
        asyncChunkGenerator = new AsyncChunkGenerator(this);
        unloadOffScreenChunksTimer = new DoEvery(UnloadOffScreenChunks, 1000);
        createNewChunksTimer = new DoEvery(CreateNewChunks, 100);
        updateGameWorldTimer = new DoEvery(UpdateGameWorld, 1000 / TARGET_SAND_FPS);
    }

    public void Init(string seed, SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
    {
        generator = new FallingSandWorldGenerator(
            seed,
            new WorldGeneratorConfig()
            {
                TerrainScale = 0.0005f,
                BiomeScale = 0.1f,
                OctaveCount = 2,
                BiomeTransitionSize = 0.2f,
            }
        );
        sandWorld = new FallingSandWorld.FallingSandWorld(new WorldPosition(1000, 1000));

        this.spriteBatch = spriteBatch;
        this.graphicsDevice = graphicsDevice;

        asyncChunkGenerator.Start();
    }

    public void CreateNewChunks()
    {
        // Loop through all chunks and create new ones if they are not loaded
        foreach (var (position, chunk) in gameChunks)
        {
            if (!chunk.hasGeneratedMap)
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

        var startChunkX =
            ((int)Math.Floor(cameraStart.X / (float)Constants.CHUNK_WIDTH))
            - Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;
        var startChunkY =
            ((int)Math.Floor(cameraStart.Y / (float)Constants.CHUNK_HEIGHT))
            - Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;

        var endChunkX =
            ((int)Math.Ceiling(cameraEnd.X / (float)Constants.CHUNK_WIDTH))
            + Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;
        var endChunkY =
            ((int)Math.Ceiling(cameraEnd.Y / (float)Constants.CHUNK_HEIGHT))
            + Constants.OFF_SCREEN_CHUNK_UNLOAD_RADIUS;

        foreach (var chunkPosition in gameChunks.Keys)
        {
            if (
                chunkPosition.X < startChunkX
                || chunkPosition.X > endChunkX
                || chunkPosition.Y < startChunkY
                || chunkPosition.Y > endChunkY
            )
            {
                gameChunks[chunkPosition].Unload();
                gameChunks.Remove(chunkPosition);
                Console.WriteLine($"Unloaded chunk at {chunkPosition}");
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
        var paddingAmount = 8;
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
        unloadOffScreenChunksTimer.Update(gameTime);
        createNewChunksTimer.Update(gameTime);
        updateGameWorldTimer.Update(gameTime);

        var chunks = GetCameraGameChunks();
        foreach (var chunk in chunks)
        {
            chunk.Update();
        }
    }

    public void Draw()
    {
        var visibleChunks = GetCameraGameChunks();

        // Draw to the render targets
        foreach (var chunk in visibleChunks)
        {
            chunk.Draw();
        }

        // Draw the render targets to the screen
        spriteBatch.Begin(
            transformMatrix: Camera.GetTransformMatrix(),
            samplerState: SamplerState.PointWrap
        );
        foreach (var chunk in visibleChunks)
        {
            spriteBatch.Draw(
                chunk.renderTarget,
                new Rectangle(
                    chunk.worldOrigin.X,
                    chunk.worldOrigin.Y,
                    Constants.CHUNK_WIDTH,
                    Constants.CHUNK_HEIGHT
                ),
                Microsoft.Xna.Framework.Color.White
            );
        }
        spriteBatch.End();
    }

    public IEnumerable<GameChunk> GetCameraGameChunks()
    {
        // Returns all chunks visible by the camera
        var (cameraStart, cameraEnd) = Camera.GetVisibleArea();

        // Calculate the starting and ending chunk coordinates with buffer
        var startChunkX =
            ((int)Math.Floor(cameraStart.X / (float)Constants.CHUNK_WIDTH))
            - Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        var startChunkY =
            ((int)Math.Floor(cameraStart.Y / (float)Constants.CHUNK_HEIGHT))
            - Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;

        var endChunkX =
            ((int)Math.Ceiling(cameraEnd.X / (float)Constants.CHUNK_WIDTH))
            + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        var endChunkY = (
            (int)Math.Ceiling(cameraEnd.Y / (float)Constants.CHUNK_HEIGHT)
            + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS
        );

        // Iterate through chunks in view
        for (int chunkX = startChunkX; chunkX < endChunkX; chunkX++)
        {
            for (int chunkY = startChunkY; chunkY < endChunkY; chunkY++)
            {
                var chunkPosition = new ChunkPosition(chunkX, chunkY);
                yield return GetOrCreateChunkFromChunkPosition(chunkPosition);
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
            chunk = new GameChunk(
                graphicsDevice,
                spriteBatch,
                new WorldPosition(
                    chunkPosition.X * Constants.CHUNK_WIDTH,
                    chunkPosition.Y * Constants.CHUNK_HEIGHT
                ),
                sandWorld,
                generator
            );
            gameChunks[chunkPosition] = chunk;

            // asyncChunkGenerator.EnqueueChunk(chunkPosition);
        }

        return chunk;
    }

    public FallingSandPixel GetPixelAtWorldPosition(WorldPosition worldPosition)
    {
        var chunk = GetOrCreateChunk(worldPosition);
        var localPosition = chunk.WorldToLocalPosition(worldPosition);
        return chunk.GetPixel(localPosition);
    }
}
