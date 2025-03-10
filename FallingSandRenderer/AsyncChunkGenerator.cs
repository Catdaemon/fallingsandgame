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
    private readonly ConcurrentBag<ChunkPosition> ChunksToGenerate = [];
    private readonly GameWorld World;
    private readonly ManualResetEvent WaitHandle = new ManualResetEvent(false);

    public AsyncChunkGenerator(GameWorld world)
    {
        World = world;

        for (int i = 0; i < 1; i++)
        {
            var newThread = new Thread(DoWork) { IsBackground = true };
            Threads.Add(newThread);
        }
    }

    private void DoWork()
    {
        while (IsRunning)
        {
            if (ChunksToGenerate.TryTake(out ChunkPosition chunkPosition))
            {
                var chunk = World.GetOrCreateChunkFromChunkPosition(chunkPosition);
                // This is a noop if the chunk has already been generated
                chunk.Generate();

                // Update the physics polygons for the chunk
                chunk.UpdatePhysicsPolygons();
            }
            else
            {
                WaitHandle.WaitOne(50); // Wait up to 50ms for new work
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
        // Only add if not already in queue
        if (!ChunksToGenerate.Contains(chunkPosition))
        {
            ChunksToGenerate.Add(chunkPosition);
            WaitHandle.Set(); // Signal waiting threads
            WaitHandle.Reset();
        }
    }
}
