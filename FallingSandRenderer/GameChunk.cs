using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FallingSand;
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
    private readonly Texture2D PixelTexture;
    private readonly FallingSandWorldGenerator WorldGenerator;

    public double lastUpdateTime = 0;
    public const int UPDATE_INTERVAL = 100;

    private readonly object polysUpdatedLock = new();
    private bool polysUpdated = false;
    private readonly ConcurrentBag<Vertices> FallingSandWorldChunkPolys = [];

    // thread-safe list
    private readonly ConcurrentBag<Body> PhysicsBodies = new();
    private readonly World PhysicsWorld;
    private readonly BasicEffect BasicEffect;

    public GameChunk(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        WorldPosition worldOrigin,
        FallingSandWorld.FallingSandWorld world,
        FallingSandWorldGenerator generator,
        World physicsWorld
    )
    {
        WorldOrigin = worldOrigin;
        SandWorld = world;
        WorldGenerator = generator;
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
        PhysicsWorld = physicsWorld;

        RenderTarget = new RenderTarget2D(
            graphicsDevice,
            Constants.CHUNK_WIDTH,
            Constants.CHUNK_HEIGHT,
            false,
            graphicsDevice.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
        ;

        PixelTexture = new Texture2D(graphicsDevice, 1, 1);
        PixelTexture.SetData([Microsoft.Xna.Framework.Color.White]);

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

    public void Generate()
    {
        if (HasGeneratedMap)
        {
            return;
        }

        var batch = WorldGenerator.GenerateBatch(
            new WorldPosition(WorldOrigin.X, WorldOrigin.Y),
            new WorldPosition(
                WorldOrigin.X + Constants.CHUNK_WIDTH,
                WorldOrigin.Y + Constants.CHUNK_HEIGHT
            )
        );

        // Create a local buffer that belongs only to this thread
        FallingSandPixelData[] pixelBuffer = new FallingSandPixelData[
            Constants.CHUNK_WIDTH * Constants.CHUNK_HEIGHT
        ];

        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = batch[y * Constants.CHUNK_WIDTH + x];

                pixelBuffer[y * Constants.CHUNK_WIDTH + x] = new FallingSandPixelData
                {
                    Material = pixel.Material,
                    Color = pixel.Color,
                };
            }
        }

        SandWorld.SetPixelBatch(WorldOrigin, pixelBuffer, Constants.CHUNK_WIDTH);

        SandChunk = SandWorld.GetOrCreateChunkFromWorldPosition(WorldOrigin);
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

    public void DebugDrawPhysicsPolys()
    {
        (VertexPositionColor[] vertices, int[] indices) CreateFilledPolygon(
            List<Microsoft.Xna.Framework.Vector2> points,
            Microsoft.Xna.Framework.Color color
        )
        {
            if (points.Count < 3)
                return (null, null);

            // Create vertices (one for each point plus one for the center)
            var _vertices = new VertexPositionColor[points.Count + 1];

            // Calculate center point by averaging all vertices
            Vector2 center = Vector2.Zero;
            foreach (Vector2 point in points)
            {
                center += point;
            }
            center /= points.Count;

            // Center vertex
            _vertices[0] = new VertexPositionColor(new Vector3(center.X, center.Y, 0), color);

            // Outer vertices
            for (int i = 0; i < points.Count; i++)
            {
                _vertices[i + 1] = new VertexPositionColor(
                    new Vector3(points[i].X, points[i].Y, 0),
                    color
                );
            }

            // Create indices for a triangle fan (converted to triangle list)
            var _indices = new int[points.Count * 3];
            for (int i = 0; i < points.Count; i++)
            {
                _indices[i * 3] = 0; // Center vertex
                _indices[i * 3 + 1] = 1 + i;
                _indices[i * 3 + 2] = 1 + ((i + 1) % points.Count);
            }

            return (_vertices, _indices);
        }

        if (FallingSandWorldChunkPolys != null)
        {
            foreach (var group in FallingSandWorldChunkPolys)
            {
                // Scale the vertices to the physics world
                var scaledGroup = group.Select(v => Convert.MetersToPixels(v)).ToList();
                var (vertices, indices) = CreateFilledPolygon(
                    scaledGroup,
                    new Microsoft.Xna.Framework.Color(0, 255, 0, 50) // Added semi-transparency
                );

                if (vertices != null && indices != null)
                {
                    foreach (EffectPass pass in BasicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        // Draw the polygon
                        GraphicsDevice.DrawUserIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            vertices,
                            0, // vertex buffer offset
                            vertices.Length, // number of vertices
                            indices,
                            0, // index buffer offset
                            indices.Length / 3 // number of primitives (triangles)
                        );
                    }
                }
            }
        }
    }

    // Draw to the render target
    public void Draw()
    {
        if (!HasGeneratedMap)
        {
            return;
        }

        GraphicsDevice.SetRenderTarget(RenderTarget);

        // Pull the pixels out of the queue into this thread to reduce contention
        var pixelsToRender = new List<LocalPosition>();

        // Pull 1000 pixels at a time to reduce fps drops
        while (SandChunk.pixelsToDraw.TryTake(out var position) && pixelsToRender.Count < 1000)
        {
            pixelsToRender.Add(position);
        }

        if (pixelsToRender.Count > 0)
        {
            SpriteBatch.Begin();

            foreach (var position in pixelsToRender)
            {
                // Get pixel data
                var pixelData = SandChunk.GetPixel(position).Data;

                // Draw to render target in the correct position
                SpriteBatch.Draw(
                    PixelTexture,
                    new Rectangle(position.X, position.Y, 1, 1),
                    new Microsoft.Xna.Framework.Color(
                        pixelData.Color.R,
                        pixelData.Color.G,
                        pixelData.Color.B
                    )
                );
            }

            SpriteBatch.End();
        }

        // Draw outline
        var outlineColor = new Microsoft.Xna.Framework.Color(255, 0, 0);
        if (SandChunk.isAwake)
        {
            outlineColor = new Microsoft.Xna.Framework.Color(0, 255, 0);
        }

        SpriteBatch.Begin();
        SpriteBatch.Draw(PixelTexture, new Rectangle(0, 0, Constants.CHUNK_WIDTH, 1), outlineColor);
        SpriteBatch.Draw(
            PixelTexture,
            new Rectangle(0, 0, 1, Constants.CHUNK_HEIGHT),
            outlineColor
        );
        SpriteBatch.Draw(
            PixelTexture,
            new Rectangle(Constants.CHUNK_WIDTH - 1, 0, 1, Constants.CHUNK_HEIGHT),
            outlineColor
        );
        SpriteBatch.Draw(
            PixelTexture,
            new Rectangle(0, Constants.CHUNK_HEIGHT - 1, Constants.CHUNK_WIDTH, 1),
            outlineColor
        );
        SpriteBatch.End();

        // DebugDrawPhysicsPolys();

        GraphicsDevice.SetRenderTarget(null);
    }

    public void Update(GameTime gameTime)
    {
        if (!HasGeneratedMap)
        {
            return;
        }

        // Look up the chunk
        if (!SandChunk.isAwake)
        {
            return;
        }

        // Don't hold the lock while updating the physics polygons
        bool _polysUpdated = false;
        lock (polysUpdatedLock)
        {
            _polysUpdated = polysUpdated;
        }
        // Only do this work if the physics polygons have been updated
        if (_polysUpdated)
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
            lock (polysUpdatedLock)
            {
                polysUpdated = false;
            }
        }
    }

    public void UpdatePhysicsPolygons()
    {
        // Generate a physics mesh for the chunk
        if (!HasGeneratedMap)
        {
            return;
        }

        lock (polysUpdatedLock)
        {
            if (polysUpdated)
            {
                // Already updated since last sync
                return;
            }
        }

        var sandChunk = SandWorld.GetOrCreateChunkFromWorldPosition(WorldOrigin);

        var result = PhysicsBodyGenerator.Generate(sandChunk);
        if (result != null)
        {
            FallingSandWorldChunkPolys.Clear();

            // Copy to the concurrent bag
            foreach (var item in result)
            {
                FallingSandWorldChunkPolys.Add(item);
            }

            lock (polysUpdatedLock)
            {
                polysUpdated = true;
            }
        }
    }

    public void Reset(WorldPosition newPosition)
    {
        // Set the new position
        WorldOrigin = newPosition;
        lastUpdateTime = 0;
    }

    public void Unload()
    {
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
        WorldOrigin = new WorldPosition(0, 0);
    }
}
