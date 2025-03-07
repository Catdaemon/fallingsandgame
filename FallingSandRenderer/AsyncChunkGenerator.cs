using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using FallingSand;

namespace FallingSand.FallingSandRenderer;

class AsyncChunkGenerator
{
    private readonly List<Thread> Threads = [];
    private bool IsRunning = false;
    private readonly ConcurrentBag<ChunkPosition> ChunksToGenerate = [];
    private readonly GameWorld World;

    public AsyncChunkGenerator(GameWorld world)
    {
        World = world;

        for (int i = 0; i < 1; i++)
        {
            Threads.Add(new Thread(DoWork));
        }
    }

    private void DoWork()
    {
        while (IsRunning)
        {
            if (ChunksToGenerate.TryTake(out ChunkPosition chunkPosition))
            {
                var chunk = World.GetOrCreateChunkFromChunkPosition(chunkPosition);
                chunk.Generate();
            }
            else
            {
                Thread.Yield();
            }
        }
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        Threads.ForEach(thread => thread.Start());

        IsRunning = true;
    }

    public void EnqueueChunk(ChunkPosition chunkPosition)
    {
        ChunksToGenerate.Add(chunkPosition);
    }
}
