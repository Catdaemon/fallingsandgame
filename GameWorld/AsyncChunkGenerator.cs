using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using fallingsand.nosync;

namespace GameWorld;

class AsyncChunkGenerator
{
    private readonly List<Thread> threads = [];
    private bool isRunning = false;
    private readonly ConcurrentBag<ChunkPosition> chunksToGenerate = [];
    private readonly GameWorld world;

    public AsyncChunkGenerator(GameWorld world)
    {
        this.world = world;

        for (int i = 0; i < 1; i++)
        {
            threads.Add(new Thread(DoWork));
        }
    }

    private void DoWork()
    {
        while (isRunning)
        {
            if (chunksToGenerate.TryTake(out ChunkPosition chunkPosition))
            {
                var chunk = world.GetOrCreateChunkFromChunkPosition(chunkPosition);
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
        if (isRunning)
        {
            return;
        }

        threads.ForEach(thread => thread.Start());

        isRunning = true;
    }

    public void EnqueueChunk(ChunkPosition chunkPosition)
    {
        chunksToGenerate.Add(chunkPosition);
    }
}
