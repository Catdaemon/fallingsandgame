using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FallingSand;

namespace FallingSandWorld;

class FallingSandWorldChunkPool
{
    private readonly ConcurrentBag<FallingSandWorldChunk> pool = new();
    private readonly FallingSandWorld parentWorld;

    public FallingSandWorldChunkPool(FallingSandWorld parentWorld)
    {
        this.parentWorld = parentWorld;
    }

    public void Initialize(int initialCapacity)
    {
        for (int i = 0; i < initialCapacity; i++)
        {
            // Create chunks with placeholder positions that will be reset later
            var chunk = new FallingSandWorldChunk(parentWorld, new ChunkPosition(0, 0));
            pool.Add(chunk);
        }
    }

    public FallingSandWorldChunk Get(ChunkPosition newChunkPos)
    {
        if (pool.TryTake(out var chunk))
        {
            chunk.Reset(newChunkPos);
            return chunk;
        }

        Console.WriteLine("Chunk pool exhausted, creating new chunk");

        return new FallingSandWorldChunk(parentWorld, newChunkPos);
    }

    public void Return(FallingSandWorldChunk chunk)
    {
        chunk.Sleep();
        pool.Add(chunk);
    }
}
