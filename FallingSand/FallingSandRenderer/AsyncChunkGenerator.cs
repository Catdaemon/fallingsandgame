using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FallingSand;

namespace FallingSand.FallingSandRenderer;

class AsyncChunkGenerator
{
    private readonly List<Thread> Threads = [];
    private bool IsRunning = false;
    private readonly ConcurrentQueue<GameChunk> ChunksToGenerate = new();

    public AsyncChunkGenerator(int numberOfthreads)
    {
        for (int i = 0; i < numberOfthreads; i++)
        {
            var newThread = new Thread(DoWork) { IsBackground = true };
            Threads.Add(newThread);
        }
    }

    private void DoWork()
    {
        while (IsRunning)
        {
            while (ChunksToGenerate.TryDequeue(out var chunkToGenerate))
            {
                chunkToGenerate.Generate();
                Thread.Yield();
            }

            Thread.Sleep(1);
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

    public void Enqueue(GameChunk chunk)
    {
        if (ChunksToGenerate.Contains(chunk))
        {
            return;
        }
        ChunksToGenerate.Enqueue(chunk);
    }
}
