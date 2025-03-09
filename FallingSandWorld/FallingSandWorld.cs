using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FallingSand;

namespace FallingSandWorld;

class FallingSandWorld
{
    public WorldPosition Extents;

    // Dictionary of chunks
    // These chunks are stored in a dictionary with the key being the chunk's x/y index
    // These indexes are calculated by dividing the world x/y position by the chunk width/height
    // This allows us to quickly look up chunks by their x/y position, or convert a world x/y position to a chunk x/y position
    private readonly object SandChunksLock = new();
    private readonly Dictionary<ChunkPosition, FallingSandWorldChunk> SandChunks = [];
    private readonly FallingSandWorldChunkPool ChunkPool;
    public long CurrentFrameId = 0;

    // Pool of threads for updating chunks
    private readonly List<Thread> Threads = [];
    private readonly BlockingCollection<FallingSandWorldChunk> ChunksToUpdate = [];
    private readonly List<ManualResetEventSlim> WorkerEvents = [];
    private volatile bool ShuttingDown = false;
    private readonly object WorldLock = new();

    public FallingSandWorld(WorldPosition extents)
    {
        Extents = extents;

        ChunkPool = new FallingSandWorldChunkPool(this);
        ChunkPool.Initialize(100);

        // Create a thread pool
        for (int i = 0; i < 3; i++)
        {
            var resetEvent = new ManualResetEventSlim(false);
            WorkerEvents.Add(resetEvent);
            Threads.Add(new Thread(() => WorkerThreadFunction(resetEvent)));
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
        lock (SandChunksLock)
        {
            if (SandChunks.TryGetValue(chunkPos, out var chunk))
            {
                return chunk;
            }

            var newChunk = ChunkPool.Get(chunkPos);
            SandChunks.Add(chunkPos, newChunk);

            return newChunk;
        }
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

    public void SetPixelBatch(WorldPosition startPos, FallingSandPixelData[] pixels, int width)
    {
        lock (WorldLock) // One lock for the entire operation
        {
            int height = pixels.Length / width;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int worldX = startPos.X + x;
                    int worldY = startPos.Y + y;

                    // Apply the pixel without individual locking
                    SetPixel(new WorldPosition(worldX, worldY), pixels[y * width + x]);
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

    public void Update(WorldPosition start, WorldPosition end)
    {
        // Get a list of all chunks in the bounding box which are awake
        var chunkInBBox = GetChunksInBBox(start, end).Where(chunk => chunk.isAwake);

        // To avoid thread safety issues, we'll update the chunks in an alternating checkerboard pattern based on the frame count
        var checkerboardChunks = chunkInBBox.Where(chunk =>
        {
            return (chunk.ChunkPos.X + chunk.ChunkPos.Y + CurrentFrameId) % 2 == 0;
        });

        // Skip work entirely if no chunks need updating
        if (!checkerboardChunks.Any())
        {
            CurrentFrameId++;
            return;
        }

        // Add chunks to queue
        foreach (var chunk in chunkInBBox)
        {
            ChunksToUpdate.Add(chunk);
        }

        // Signal workers to start
        foreach (var evt in WorkerEvents)
        {
            evt.Set();
        }

        // Wait for workers with timeout to prevent deadlocks
        bool allFinished = WorkerEvents.All(evt => evt.Wait(100));

        // If timeout occurred, don't wait indefinitely
        if (!allFinished)
        {
            Console.WriteLine("Warning: Update did not complete in time frame");
        }

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
        lock (SandChunksLock)
        {
            foreach (var chunkPos in chunksToUnload)
            {
                if (SandChunks.TryGetValue(chunkPos, out var chunk))
                {
                    SandChunks.Remove(chunkPos);
                    ChunkPool.Return(chunk);
                }
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
