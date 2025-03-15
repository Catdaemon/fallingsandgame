using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FallingSand.WorldGenerator;
using FallingSandWorld;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.FallingSandRenderer;

class GameChunkPool
{
    private readonly ConcurrentBag<GameChunk> pool = new();
    private readonly GraphicsDevice graphicsDevice;
    private readonly Texture2D pixelTexture;
    private readonly SpriteBatch spriteBatch;
    private readonly FallingSandWorld.FallingSandWorld sandWorld;
    private readonly GeneratedWorldInstance worldTiles;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld;
    private readonly MaterialTextureSampler materialTextureSampler;
    private readonly Effect waterShaderEffect;

    public GameChunkPool(
        GraphicsDevice graphicsDevice,
        Texture2D pixelTexture,
        SpriteBatch spriteBatch,
        FallingSandWorld.FallingSandWorld sandWorld,
        GeneratedWorldInstance worldTiles,
        nkast.Aether.Physics2D.Dynamics.World physicsWorld,
        MaterialTextureSampler materialTextureSampler,
        Effect waterShaderEffect
    )
    {
        this.graphicsDevice = graphicsDevice;
        this.spriteBatch = spriteBatch;
        this.sandWorld = sandWorld;
        this.worldTiles = worldTiles;
        this.physicsWorld = physicsWorld;
        this.pixelTexture = pixelTexture;
        this.materialTextureSampler = materialTextureSampler;
        this.waterShaderEffect = waterShaderEffect;
    }

    public void Initialize(int initialSize)
    {
        for (int i = 0; i < initialSize; i++)
        {
            pool.Add(CreateChunk(new WorldPosition(0, 0)));
        }
    }

    private GameChunk CreateChunk(WorldPosition position)
    {
        return new GameChunk(
            graphicsDevice,
            pixelTexture,
            spriteBatch,
            position,
            sandWorld,
            worldTiles,
            physicsWorld,
            materialTextureSampler,
            waterShaderEffect
        );
    }

    public GameChunk Get(WorldPosition position)
    {
        if (pool.TryTake(out var chunk))
        {
            chunk.Reset(position);
            return chunk;
        }

        Console.WriteLine("GameChunk pool exhausted, creating new GameChunk");

        return CreateChunk(position);
    }

    public void Return(GameChunk chunk)
    {
        pool.Add(chunk);
    }
}
