using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FallingSand.WorldGenerator;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.FallingSandRenderer;

class GameChunk
{
    public WorldPosition WorldOrigin;
    private readonly FallingSandWorld.FallingSandWorld SandWorld;

    public FallingSandWorldChunk SandChunk;
    public bool HasGeneratedMap = false;
    public RenderTarget2D RenderTarget;
    public SpriteBatch SpriteBatch;
    private readonly GraphicsDevice GraphicsDevice;
    public readonly Texture2D PixelTexture;
    private readonly GeneratedWorldInstance WorldTiles;
    private readonly MaterialTextureSampler MaterialTextureSampler;
    private bool polysUpdated = false;
    public bool IsCalculatingPhysics = false;
    private readonly ConcurrentBag<Vertices> FallingSandWorldChunkPolys = [];

    // Water shader effect
    private Effect waterShaderEffect;
    private RenderTarget2D waterRenderTarget;
    private float totalTime = 0f;

    // thread-safe list
    private readonly ConcurrentBag<Body> PhysicsBodies = [];
    public bool HasPhysicsBodies => !PhysicsBodies.IsEmpty;
    private readonly World PhysicsWorld;
    private readonly BasicEffect BasicEffect;

    public GameChunk(
        GraphicsDevice graphicsDevice,
        Texture2D pixelTexture,
        SpriteBatch spriteBatch,
        WorldPosition worldOrigin,
        FallingSandWorld.FallingSandWorld world,
        GeneratedWorldInstance worldTiles,
        World physicsWorld,
        MaterialTextureSampler materialTextureSampler,
        Effect waterEffect
    )
    {
        WorldOrigin = worldOrigin;
        SandWorld = world;
        WorldTiles = worldTiles;
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
        PhysicsWorld = physicsWorld;
        MaterialTextureSampler = materialTextureSampler;
        waterShaderEffect = waterEffect;

        RenderTarget = new RenderTarget2D(
            graphicsDevice,
            Constants.CHUNK_WIDTH,
            Constants.CHUNK_HEIGHT,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24, // Use depth buffer
            0,
            RenderTargetUsage.PreserveContents
        );

        waterRenderTarget = new RenderTarget2D(
            graphicsDevice,
            Constants.CHUNK_WIDTH,
            Constants.CHUNK_HEIGHT,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24, // Use depth buffer
            0,
            RenderTargetUsage.PreserveContents
        );

        graphicsDevice.SetRenderTarget(RenderTarget);
        graphicsDevice.Clear(Color.Transparent);
        graphicsDevice.SetRenderTarget(null);

        PixelTexture = pixelTexture;

        BasicEffect = new BasicEffect(GraphicsDevice);
        BasicEffect.VertexColorEnabled = true;
        BasicEffect.Projection = Matrix.CreateOrthographicOffCenter(
            0,
            Constants.CHUNK_WIDTH,
            Constants.CHUNK_HEIGHT,
            0,
            0,
            1
        );
    }

    // Usually called from AsyncChunkGenerator
    public void Generate(bool initialGeneration = false)
    {
        if (HasGeneratedMap)
        {
            return;
        }

        var worldTile = WorldTiles.GetTileAt(
            FallingSandWorld.FallingSandWorld.WorldToChunkPosition(WorldOrigin)
        );

        // Create a local buffer that belongs only to this thread
        FallingSandPixelData[] pixelBuffer = new FallingSandPixelData[
            Constants.CHUNK_WIDTH * Constants.CHUNK_HEIGHT
        ];

        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = worldTile.TileDefinition.PixelData[y * Constants.CHUNK_WIDTH + x];

                pixelBuffer[y * Constants.CHUNK_WIDTH + x] = new FallingSandPixelData
                {
                    Material = pixel,
                    Color = MaterialTextureSampler.GetPixel(pixel, x, y),
                };
            }
        }

        SandChunk = SandWorld.GetOrCreateChunkFromWorldPosition(WorldOrigin);
        SandChunk.Sleep();

        if (initialGeneration)
        {
            // Do not use the thread-safe method here
            SandChunk.SetPixelBatch(pixelBuffer, 0);
            SandChunk.MarkEntireChunkForRedraw();
            SandChunk.Wake();
            HasGeneratedMap = true;
            return;
        }

        const int setPerUpdate = 500;
        for (int i = 0; i < Constants.CHUNK_WIDTH * Constants.CHUNK_HEIGHT; i += setPerUpdate)
        {
            var pixels = pixelBuffer.Skip(i).Take(setPerUpdate).ToArray();
            SandChunk.SetPixelBatch(pixels, i);
            Thread.Sleep(1);
        }

        SandChunk.MarkEntireChunkForRedraw();

        SandChunk.Wake();

        HasGeneratedMap = true;
    }

    public LocalPosition WorldToLocalPosition(WorldPosition worldPosition)
    {
        return new LocalPosition(worldPosition.X - WorldOrigin.X, worldPosition.Y - WorldOrigin.Y);
    }

    public WorldPosition LocalToWorldPosition(LocalPosition localPosition)
    {
        return new WorldPosition(localPosition.X + WorldOrigin.X, localPosition.Y + WorldOrigin.Y);
    }

    private void DrawEntireChunk()
    {
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = SandChunk.pixels[y * Constants.CHUNK_WIDTH + x];
                var pixelData = pixel.Data;

                // Draw normal pixels as usual
                if (pixelData.Material != Material.Water)
                {
                    SpriteBatch.Draw(PixelTexture, new Rectangle(x, y, 1, 1), pixelData.Color);
                }
            }
        }

        // Draw water pixels separately using shader
        DrawWaterPixels();
    }

    private void DrawWaterPixels()
    {
        if (waterShaderEffect == null)
            return;

        // Set the water render target to capture what's been drawn so far
        GraphicsDevice.SetRenderTarget(waterRenderTarget);
        GraphicsDevice.Clear(Color.Transparent);

        // Begin sprite batch with water shader
        waterShaderEffect.Parameters["TotalTime"].SetValue(totalTime);
        waterShaderEffect.Parameters["ScreenTexture"].SetValue(RenderTarget);

        SpriteBatch.Begin(effect: waterShaderEffect);

        // Draw only water pixels
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = SandChunk.pixels[y * Constants.CHUNK_WIDTH + x];
                var pixelData = pixel.Data;

                if (pixelData.Material == Material.Water)
                {
                    // Draw water pixels with the shader
                    SpriteBatch.Draw(PixelTexture, new Rectangle(x, y, 1, 1), pixelData.Color);
                }
            }
        }

        SpriteBatch.End();

        // Switch back to main render target and copy water pixels
        GraphicsDevice.SetRenderTarget(RenderTarget);

        SpriteBatch.Begin(blendState: BlendState.AlphaBlend);
        SpriteBatch.Draw(waterRenderTarget, Vector2.Zero, Color.White);
        SpriteBatch.End();
    }

    public static readonly BlendState overwriteBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.SourceColor,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.Zero,
    };

    // Draw to the render target
    public void Draw(GameTime gameTime)
    {
        if (!HasGeneratedMap || SandChunk == null)
        {
            return;
        }

        // Update total time for water animation
        totalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

        GraphicsDevice.SetRenderTarget(RenderTarget);

        var renderedPixels = 0;
        var hasPixels = SandChunk.pixelsToDraw.Any();
        if (hasPixels)
        {
            // First draw non-water pixels
            SpriteBatch.Begin(blendState: overwriteBlend);

            // Water pixels to draw after regular pixels
            // List<LocalPosition> waterPositions = [];

            while (SandChunk.pixelsToDraw.TryTake(out var position) && renderedPixels < 1000)
            {
                if (position.X == -1 && position.Y == -1)
                {
                    // This indicates that the chunk should be drawn in its entirety
                    DrawEntireChunk();
                }
                else
                {
                    // Get pixel data
                    var pixel = SandChunk.GetPixel(position);
                    var pixelData = pixel.Data;

                    if (pixelData.Material == Material.Water)
                    {
                        // Collect water pixels for later drawing with shader
                        // waterPositions.Add(position);
                    }

                    // TODO: should this be drawn?
                    // Draw regular pixels
                    SpriteBatch.Draw(
                        PixelTexture,
                        new Rectangle(position.X, position.Y, 1, 1),
                        pixelData.Color
                    );
                }

                renderedPixels++;
            }

            SpriteBatch.End();

            // Now draw water pixels using shader
            // if (waterPositions.Count > 0 && waterShaderEffect != null)
            // {
            //     // Set the water render target
            //     GraphicsDevice.SetRenderTarget(waterRenderTarget);
            //     GraphicsDevice.Clear(Color.Transparent);

            //     // Apply water shader
            //     waterShaderEffect.Parameters["TotalTime"].SetValue(totalTime);
            //     waterShaderEffect.Parameters["ScreenTexture"].SetValue(RenderTarget);

            //     SpriteBatch.Begin(effect: waterShaderEffect);

            //     foreach (var position in waterPositions)
            //     {
            //         var pixelData = SandChunk.GetPixel(position).Data;
            //         SpriteBatch.Draw(
            //             PixelTexture,
            //             new Rectangle(position.X, position.Y, 1, 1),
            //             pixelData.Color
            //         );
            //     }

            //     SpriteBatch.End();

            //     // Switch back to main render target and copy water pixels
            //     GraphicsDevice.SetRenderTarget(RenderTarget);

            //     SpriteBatch.Begin(blendState: BlendState.AlphaBlend);
            //     SpriteBatch.Draw(waterRenderTarget, Vector2.Zero, Color.White);
            //     SpriteBatch.End();
            // }
        }

        GraphicsDevice.SetRenderTarget(null);
    }

    public void CreatePhysicsBodies()
    {
        if (!HasGeneratedMap || SandChunk == null)
        {
            return;
        }

        // Only do this work if the physics polygons have been updated
        if (polysUpdated)
        {
            foreach (var body in PhysicsBodies)
            {
                PhysicsWorld.Remove(body);
            }
            PhysicsBodies.Clear();

            foreach (var vertices in FallingSandWorldChunkPolys)
            {
                var newBody = PhysicsWorld.CreatePolygon(
                    vertices: vertices,
                    density: 1,
                    position: Convert.PixelsToMeters(new Vector2(WorldOrigin.X, WorldOrigin.Y))
                );
                newBody.BodyType = BodyType.Static;

                PhysicsBodies.Add(newBody);
            }
            polysUpdated = false;
        }
    }

    public void UpdatePhysicsPolygons()
    {
        // Generate a physics mesh for the chunk
        if (SandChunk == null || !HasGeneratedMap || polysUpdated)
        {
            IsCalculatingPhysics = false;
            return;
        }

        var result = PhysicsBodyGenerator.Generate(SandChunk);
        if (result != null)
        {
            FallingSandWorldChunkPolys.Clear();

            // Copy to the concurrent bag
            foreach (var item in result)
            {
                FallingSandWorldChunkPolys.Add(item);
            }

            polysUpdated = true;
        }

        IsCalculatingPhysics = false;
    }

    public void Reset(WorldPosition newPosition)
    {
        // Set the new position
        WorldOrigin = newPosition;
    }

    public void Unload()
    {
        // Clean up render targets
        if (waterRenderTarget != null && !waterRenderTarget.IsDisposed)
        {
            waterRenderTarget.Dispose();
        }

        // Unload the physics bodies
        foreach (var body in PhysicsBodies)
        {
            PhysicsWorld.Remove(body);
        }
        PhysicsBodies.Clear();
        FallingSandWorldChunkPolys.Clear();

        // Unload the chunk
        SandWorld.UnloadChunkAt(WorldOrigin);
        SandChunk = null;

        HasGeneratedMap = false;
        IsCalculatingPhysics = false;
        WorldOrigin = new WorldPosition(0, 0);
    }
}
