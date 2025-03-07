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
    private readonly Dictionary<ChunkPosition, FallingSandWorldChunk> sandChunks = [];
    public long currentFrameId = 0;

    // Pool of threads for updating chunks
    private readonly List<Thread> threads = [];
    private readonly BlockingCollection<FallingSandWorldChunk> chunksToUpdate = [];
    private readonly object worldLock = new();

    public FallingSandWorld(WorldPosition extents)
    {
        Extents = extents;

        // Create a thread pool
        for (int i = 0; i < 4; i++)
        {
            threads.Add(new Thread(WorkerThreadFunction));
        }

        threads.ForEach(thread => thread.Start());
    }

    private void WorkerThreadFunction()
    {
        // Grab a chunk from the bag and update it
        while (true)
        {
            foreach (var chunk in chunksToUpdate.GetConsumingEnumerable())
            {
                chunk.Update();
            }

            Thread.Yield();
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
            chunk.WorldX * Constants.CHUNK_WIDTH + localPosition.X,
            chunk.WorldY * Constants.CHUNK_HEIGHT + localPosition.Y
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
        if (!sandChunks.TryGetValue(chunkPos, out FallingSandWorldChunk value))
        {
            value = new FallingSandWorldChunk(this, chunkPos.X, chunkPos.Y);
            sandChunks[chunkPos] = value;
        }

        return value;
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
        lock (worldLock) // One lock for the entire operation
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
        foreach (var chunk in GetChunksInBBox(start, end))
        {
            chunk.Update();
        }
        currentFrameId++;
    }

    public void Update(WorldPosition start, WorldPosition end)
    {
        // We want to update sand chunks in their own threads
        // Because most CPUs don't have more than 4 cores, we'll limit the number of threads to 4
        // As there are more than 4 chunks, we'll need to update them in batches
        // We don't need to update all chunks every frame, so we'll update a random selection of chunks each frame

        // Get a list of all chunks in the bounding box which are awake
        var chunkInBBox = GetChunksInBBox(start, end).Where(chunk => chunk.isAwake);

        // To avoid thread safety issues, we'll update the chunks in an alternating checkerboard pattern based on the frame count
        var checkerboardChunks = chunkInBBox.Where(chunk =>
        {
            var chunkPos = WorldToChunkPosition(new WorldPosition(chunk.WorldX, chunk.WorldY));
            return (chunkPos.X + chunkPos.Y + currentFrameId) % 2 == 0;
        });

        // Add the chunks to the bag
        foreach (var chunk in checkerboardChunks)
        {
            chunksToUpdate.Add(chunk);
        }

        // Wait for all threads to finish
        while (chunksToUpdate.Count > 0)
        {
            Thread.Yield();
        }

        // Increment the frame count
        currentFrameId++;
    }

    public void WakeChunkAt(WorldPosition worldPosition)
    {
        var chunk = GetOrCreateChunkFromWorldPosition(worldPosition);
        chunk.Wake();
    }

    public void UnloadChunkAt(WorldPosition worldPosition)
    {
        var chunkPos = WorldToChunkPosition(worldPosition);
        if (sandChunks.ContainsKey(chunkPos))
        {
            sandChunks.Remove(chunkPos);
        }
    }
}
