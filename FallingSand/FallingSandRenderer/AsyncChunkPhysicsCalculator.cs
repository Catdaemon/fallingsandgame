using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FallingSand;
using FallingSandWorld;

namespace FallingSand.FallingSandRenderer;

class AsyncChunkPhysicsCalculator
{
    private readonly List<Thread> Threads = [];
    private bool IsRunning = false;
    private readonly ConcurrentQueue<GameChunk> ChunksToUpdate = new();

    public AsyncChunkPhysicsCalculator(int numberOfthreads)
    {
        for (int i = 0; i < numberOfthreads; i++)
        {
            var newThread = new Thread(DoWork) { IsBackground = true };
            Threads.Add(newThread);
        }
    }

    private static void UpdatePhysicsPolygons(GameChunk chunk)
    {
        // Generate a physics mesh for the chunk
        if (chunk.SandChunk == null || !chunk.HasGeneratedMap || chunk.polysUpdated)
        {
            chunk.IsCalculatingPhysics = false;
            return;
        }

        var result = PhysicsBodyGenerator.GetInstance().Generate(chunk.SandChunk);
        if (result != null)
        {
            chunk.FallingSandWorldChunkPolys.Clear();

            // Copy to the concurrent bag
            foreach (var item in result)
            {
                chunk.FallingSandWorldChunkPolys.Add(item);
            }

            chunk.polysUpdated = true;
        }

        chunk.IsCalculatingPhysics = false;
    }

    private void DoWork()
    {
        while (IsRunning)
        {
            while (ChunksToUpdate.TryDequeue(out var chunkToUpdate))
            {
                // Update the physics polygons for the chunk
                UpdatePhysicsPolygons(chunkToUpdate);
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
        if (ChunksToUpdate.Contains(chunk))
        {
            return;
        }
        ChunksToUpdate.Enqueue(chunk);
    }
}
