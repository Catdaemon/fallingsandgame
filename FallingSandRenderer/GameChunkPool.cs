using System;
using System.Collections.Generic;
using FallingSandWorld;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.FallingSandRenderer;

class GameChunkPool
{
    private readonly object lockObject = new();
    private readonly Stack<GameChunk> pool = new();
    private readonly GraphicsDevice graphicsDevice;
    private readonly SpriteBatch spriteBatch;
    private readonly FallingSandWorld.FallingSandWorld sandWorld;
    private readonly FallingSandWorldGenerator generator;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld;

    public GameChunkPool(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        FallingSandWorld.FallingSandWorld sandWorld,
        FallingSandWorldGenerator generator,
        nkast.Aether.Physics2D.Dynamics.World physicsWorld
    )
    {
        this.graphicsDevice = graphicsDevice;
        this.spriteBatch = spriteBatch;
        this.sandWorld = sandWorld;
        this.generator = generator;
        this.physicsWorld = physicsWorld;
    }

    public void Initialize(int initialSize)
    {
        lock (lockObject)
        {
            for (int i = 0; i < initialSize; i++)
            {
                pool.Push(CreateChunk(new WorldPosition(0, 0)));
            }
        }
    }

    private GameChunk CreateChunk(WorldPosition position)
    {
        return new GameChunk(
            graphicsDevice,
            spriteBatch,
            position,
            sandWorld,
            generator,
            physicsWorld
        );
    }

    public GameChunk Get(WorldPosition position)
    {
        lock (lockObject)
        {
            if (pool.Count > 0)
            {
                var chunk = pool.Pop();
                chunk.Reset(position);
                return chunk;
            }

            Console.WriteLine("GameChunk pool exhausted, creating new GameChunk");

            return CreateChunk(position);
        }
    }

    public void Return(GameChunk chunk)
    {
        lock (lockObject)
        {
            pool.Push(chunk);
        }
    }
}
