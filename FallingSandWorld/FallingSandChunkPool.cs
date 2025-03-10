using System;
using System.Collections.Generic;
using FallingSand;

namespace FallingSandWorld;

class FallingSandWorldChunkPool
{
    private readonly object poolLock = new();
    private readonly Stack<FallingSandWorldChunk> pool = new();
    private readonly FallingSandWorld parentWorld;

    public FallingSandWorldChunkPool(FallingSandWorld parentWorld)
    {
        this.parentWorld = parentWorld;
    }

    public void Initialize(int initialCapacity)
    {
        lock (poolLock)
        {
            for (int i = 0; i < initialCapacity; i++)
            {
                // Create chunks with placeholder positions that will be reset later
                var chunk = new FallingSandWorldChunk(parentWorld, new ChunkPosition(0, 0));
                pool.Push(chunk);
            }
        }
    }

    public FallingSandWorldChunk Get(ChunkPosition newChunkPos)
    {
        lock (poolLock)
        {
            if (pool.Count > 0)
            {
                var chunk = pool.Pop();
                chunk.Reset(newChunkPos);
                return chunk;
            }

            Console.WriteLine("Chunk pool exhausted, creating new chunk");

            return new FallingSandWorldChunk(parentWorld, newChunkPos);
        }
    }

    public void Return(FallingSandWorldChunk chunk)
    {
        lock (poolLock)
        {
            chunk.Sleep();
            pool.Push(chunk);
        }
    }
}
