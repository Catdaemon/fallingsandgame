using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FallingSand;
using Microsoft.Xna.Framework;

namespace FallingSandWorld;

class FallingSandWorld
{
    public WorldPosition Extents;

    // Dictionary of chunks
    // These chunks are stored in a dictionary with the key being the chunk's x/y index
    // These indexes are calculated by dividing the world x/y position by the chunk width/height
    // This allows us to quickly look up chunks by their x/y position, or convert a world x/y position to a chunk x/y position
    private readonly ConcurrentDictionary<ChunkPosition, FallingSandWorldChunk> SandChunks = new(
        5,
        Constants.INITIAL_CHUNK_POOL_SIZE
    );
    private readonly FallingSandWorldChunkPool ChunkPool;
    public long CurrentFrameId = 0;

    // Pool of threads for updating chunks
    private readonly List<Thread> Threads = [];
    private readonly ConcurrentBag<FallingSandWorldChunk> ChunksToUpdate = [];
    private readonly List<ManualResetEventSlim> WorkerEvents = [];
    private volatile bool ShuttingDown = false;

    public FallingSandWorld(WorldPosition extents)
    {
        Extents = extents;

        ChunkPool = new FallingSandWorldChunkPool(this);
        ChunkPool.Initialize(Constants.INITIAL_CHUNK_POOL_SIZE);

        // Create a thread pool
        for (int i = 0; i < 2; i++)
        {
            var resetEvent = new ManualResetEventSlim(false);
            WorkerEvents.Add(resetEvent);
            var newThread = new Thread(() => WorkerThreadFunction(resetEvent))
            {
                IsBackground = true,
            };
            Threads.Add(newThread);
        }

        Threads.ForEach(thread => thread.Start());
    }

    private void WorkerThreadFunction(ManualResetEventSlim resetEvent)
    {
        while (!ShuttingDown)
        {
            // Wait for work signal
            resetEvent.Wait();

            // Process chunks until queue is empty
            while (ChunksToUpdate.TryTake(out var chunk))
            {
                if (chunk.isAwake)
                    chunk.Update();
            }

            // Signal completion and reset
            resetEvent.Reset();
        }
    }

    // Convert a world position to a chunk's position
    public static ChunkPosition WorldToChunkPosition(WorldPosition worldPosition)
    {
        return new ChunkPosition(
            (int)Math.Floor((float)worldPosition.X / Constants.CHUNK_WIDTH),
            (int)Math.Floor((float)worldPosition.Y / Constants.CHUNK_HEIGHT)
        );
    }

    // Convert a local position in a chunk to a world position
    public static WorldPosition ChunkToWorldPosition(
        FallingSandWorldChunk chunk,
        LocalPosition localPosition
    )
    {
        return new WorldPosition(
            chunk.ChunkPos.X * Constants.CHUNK_WIDTH + localPosition.X,
            chunk.ChunkPos.Y * Constants.CHUNK_HEIGHT + localPosition.Y
        );
    }

    // Convert a world position to a local position within a chunk
    public static LocalPosition WorldToLocalPosition(WorldPosition worldPosition)
    {
        // Get the chunk position
        ChunkPosition chunkPos = WorldToChunkPosition(worldPosition);

        // Calculate chunk-relative coordinates using subtraction
        return new LocalPosition(
            worldPosition.X - (chunkPos.X * Constants.CHUNK_WIDTH),
            worldPosition.Y - (chunkPos.Y * Constants.CHUNK_HEIGHT)
        );
    }

    public FallingSandWorldChunk GetOrCreateChunkFromWorldPosition(WorldPosition worldPosition)
    {
        var chunkPos = WorldToChunkPosition(worldPosition);
        return GetOrCreateChunkFromChunkPosition(chunkPos);
    }

    public FallingSandWorldChunk GetOrCreateChunkFromChunkPosition(ChunkPosition chunkPos)
    {
        // Check to see if we already have a chunk at this position
        if (SandChunks.TryGetValue(chunkPos, out var chunk))
        {
            return chunk;
        }

        return SandChunks.AddOrUpdate(chunkPos, ChunkPool.Get, (key, oldValue) => oldValue);
    }

    public IEnumerable<FallingSandWorldChunk> GetChunksInBBox(
        WorldPosition start,
        WorldPosition end
    )
    {
        var startChunkPos = WorldToChunkPosition(start);
        var endChunkPos = WorldToChunkPosition(end);

        for (int x = startChunkPos.X; x <= endChunkPos.X; x++)
        {
            for (int y = startChunkPos.Y; y <= endChunkPos.Y; y++)
            {
                yield return GetOrCreateChunkFromChunkPosition(new ChunkPosition(x, y));
            }
        }
    }

    public void SetPixel(WorldPosition worldPosition, FallingSandPixelData pixel)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        var localPosition = WorldToLocalPosition(worldPosition);

        chunk.SetPixel(localPosition, pixel);
    }

    public void SetPixel(WorldPosition worldPosition, FallingSandPixelData pixel, float velocity)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        var localPosition = WorldToLocalPosition(worldPosition);

        chunk.SetPixel(localPosition, pixel, velocity);
    }

    public void EmptyPixel(WorldPosition worldPosition)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        var localPosition = WorldToLocalPosition(worldPosition);

        chunk.EmptyPixel(localPosition);
    }

    public void DeletePixels(WorldPosition center, int radius)
    {
        var startPos = new WorldPosition(center.X - radius, center.Y - radius);
        var endPos = new WorldPosition(center.X + radius, center.Y + radius);

        int radiusSquared = radius * radius; // Precalculate for performance

        for (int y = startPos.Y; y <= endPos.Y; y++)
        {
            // Calculate y component of distance once per row
            int dy = y - center.Y;
            int dySquared = dy * dy;

            for (int x = startPos.X; x <= endPos.X; x++)
            {
                // Calculate x component and complete the distance check
                int dx = x - center.X;
                if (dx * dx + dySquared <= radiusSquared)
                {
                    EmptyPixel(new WorldPosition(x, y));
                }
            }
        }
    }

    public void ScorchPixels(WorldPosition center, int startRadius, int scorchRadius)
    {
        // Scorch pixels in a circle around the center
        var startPos = new WorldPosition(center.X - scorchRadius, center.Y - scorchRadius);
        var endPos = new WorldPosition(center.X + scorchRadius, center.Y + scorchRadius);

        int scorchRadiusSquared = scorchRadius * scorchRadius;

        for (int y = startPos.Y; y <= endPos.Y; y++)
        {
            int dy = y - center.Y;
            int dySquared = dy * dy;

            for (int x = startPos.X; x <= endPos.X; x++)
            {
                int dx = x - center.X;
                if (dx * dx + dySquared <= scorchRadiusSquared)
                {
                    var worldPos = new WorldPosition(x, y);
                    var pixel = GetPixel(worldPos);
                    if (pixel.Data.Material != Material.Empty)
                    {
                        // Darker version of the pixel Color
                        var color = new Color(
                            pixel.Data.Color.R / 2,
                            pixel.Data.Color.G / 2,
                            pixel.Data.Color.B / 2
                        );
                        SetPixel(
                            worldPos,
                            new FallingSandPixelData
                            {
                                Material = pixel.Data.Material,
                                Color = color,
                            }
                        );
                    }
                }
            }
        }
    }

    public void ExplodePixels(WorldPosition center, int radius)
    {
        var startPos = new WorldPosition(center.X - radius, center.Y - radius);
        var endPos = new WorldPosition(center.X + radius, center.Y + radius);

        for (int x = startPos.X; x <= endPos.X; x++)
        {
            for (int y = startPos.Y; y <= endPos.Y; y++)
            {
                // Calculate distance from center
                float dx = x - center.X;
                float dy = y - center.Y;
                float distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance <= radius)
                {
                    var pixel = GetPixel(new WorldPosition(x, y));
                    if (pixel.Data.Material != Material.Empty)
                    {
                        SetPixel(
                            new WorldPosition(x, y),
                            new FallingSandPixelData
                            {
                                Material = Material.Smoke,
                                Color = Color.Gray,
                            }
                        );
                    }
                }
            }
        }

        ScorchPixels(center, radius, radius * 2);
    }

    public void DisruptPixels(WorldPosition center, int radius)
    {
        var startPos = new WorldPosition(center.X - radius, center.Y - radius);
        var endPos = new WorldPosition(center.X + radius, center.Y + radius);

        int radiusSquared = radius * radius;

        for (int y = startPos.Y; y <= endPos.Y; y++)
        {
            int dy = y - center.Y;
            int dySquared = dy * dy;

            for (int x = startPos.X; x <= endPos.X; x++)
            {
                int dx = x - center.X;
                if (dx * dx + dySquared <= radiusSquared)
                {
                    var worldPosition = new WorldPosition(x, y);
                    var pixel = GetPixel(worldPosition);
                    if (pixel.Data.Material != Material.Empty)
                    {
                        SetPixel(
                            worldPosition,
                            new FallingSandPixelData
                            {
                                Material = Material.Sand,
                                Color = pixel.Data.Color,
                            }
                        );

                        // Wake the chunk to ensure it is updated
                        WakeChunkAt(worldPosition);
                    }
                }
            }
        }
    }

    public FallingSandPixel GetPixel(WorldPosition worldPosition)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        var chunkPos = WorldToChunkPosition(worldPosition);

        return chunk.GetPixel(
            new LocalPosition(
                worldPosition.X - chunkPos.X * Constants.CHUNK_WIDTH,
                worldPosition.Y - chunkPos.Y * Constants.CHUNK_HEIGHT
            )
        );
    }

    public void UpdateSynchronously(WorldPosition start, WorldPosition end)
    {
        var relevantChunks = GetChunksInBBox(start, end);

        foreach (var chunk in relevantChunks)
        {
            chunk.Update();
        }
        CurrentFrameId++;
    }

    private void UpdateSet(IEnumerable<FallingSandWorldChunk> chunks)
    {
        // Add chunks to queue
        foreach (var chunk in chunks)
        {
            ChunksToUpdate.Add(chunk);
        }

        // Signal workers to start
        foreach (var evt in WorkerEvents)
        {
            evt.Set();
        }

        // Wait for workers with timeout to prevent deadlocks
        bool allFinished = WorkerEvents.All(evt => evt.Wait(10));

        // If timeout occurred, don't wait indefinitely
        if (!allFinished)
        {
            Console.WriteLine("Warning: Update did not complete in time frame");
        }
    }

    public void Update(WorldPosition start, WorldPosition end)
    {
        // Get a list of all chunks in the bounding box which are awake
        var chunkInBBox = GetChunksInBBox(start, end).Where(chunk => chunk.isAwake);

        // To avoid thread safety issues, we'll update the chunks in an alternating checkerboard pattern based on the frame count
        var checkerboardChunksA = chunkInBBox.Where(chunk =>
        {
            return (chunk.ChunkPos.X + chunk.ChunkPos.Y + CurrentFrameId) % 2 == 0;
        });
        var checkerboardChunksB = chunkInBBox.Where(chunk =>
        {
            return (chunk.ChunkPos.X + chunk.ChunkPos.Y + CurrentFrameId) % 2 == 0;
        });

        // Skip work entirely if no chunks need updating
        if (!checkerboardChunksA.Any() || !checkerboardChunksB.Any())
        {
            CurrentFrameId++;
            return;
        }

        UpdateSet(checkerboardChunksA);
        UpdateSet(checkerboardChunksB);

        // Increment the frame count
        CurrentFrameId++;
    }

    public void WakeChunkAt(WorldPosition worldPosition)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        chunk.Wake();
    }

    public void UnloadChunks(IEnumerable<ChunkPosition> chunksToUnload)
    {
        foreach (var chunkPos in chunksToUnload)
        {
            if (SandChunks.TryRemove(chunkPos, out var chunk))
            {
                ChunkPool.Return(chunk);
            }
        }
    }

    public void UnloadChunkAt(WorldPosition worldPosition)
    {
        var chunkPos = WorldToChunkPosition(worldPosition);
        UnloadChunks([chunkPos]);
    }

    public void Dispose()
    {
        ShuttingDown = true;
        foreach (var evt in WorkerEvents)
        {
            evt.Set(); // Signal all threads to check shutdown condition
        }

        foreach (var thread in Threads)
        {
            thread.Join(1000); // Give threads 1 second to exit
        }

        foreach (var evt in WorkerEvents)
        {
            evt.Dispose();
        }
    }
}
