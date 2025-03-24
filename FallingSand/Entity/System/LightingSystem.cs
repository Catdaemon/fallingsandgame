using System;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Diagnostics;
using nkast.Aether.Physics2D.Dynamics;
using PhysicsWorld = nkast.Aether.Physics2D.Dynamics.World;
using World = Arch.Core.World;

namespace FallingSand.Entity.System;

class LightingSystem : ISystem
{
    private readonly World World;
    private SpriteBatch spriteBatch;
    private PrimitiveBatch primitiveBatch;
    private readonly PhysicsWorld PhysicsWorld;
    private readonly GraphicsDevice GraphicsDevice;
    private Texture2D pointLightTexture;
    private RenderTarget2D lightTarget;
    private RenderTarget2D sceneTarget;
    private RenderTarget2D shadowTarget;
    private const int TextureSize = 512;
    
    public LightingSystem(World world, PhysicsWorld physicsWorld, GraphicsDevice graphicsDevice)
    {
        World = world;
        GraphicsDevice = graphicsDevice;
        PhysicsWorld = physicsWorld;
    }

    public void InitializeGraphics(GraphicsDevice graphicsDevice, ContentManager contentManager)
    {
        spriteBatch = new SpriteBatch(graphicsDevice);
        primitiveBatch = new PrimitiveBatch(graphicsDevice);

        // Create render targets
        var pp = graphicsDevice.PresentationParameters;
        lightTarget = new RenderTarget2D(graphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
        sceneTarget = new RenderTarget2D(graphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);
        shadowTarget = new RenderTarget2D(graphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight);

        // Create point light texture
        pointLightTexture = new Texture2D(graphicsDevice, TextureSize, TextureSize);
        Color[] colors = new Color[TextureSize * TextureSize];
        Vector2 center = new Vector2(TextureSize / 2f);

        for (int x = 0; x < TextureSize; x++)
        {
            for (int y = 0; y < TextureSize; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float intensity = Math.Max(0, 1 - (distance / (TextureSize / 2f)));
                intensity = intensity * intensity; // Square for more realistic falloff
                colors[x + y * TextureSize] = Color.White * intensity;
            }
        }
        pointLightTexture.SetData(colors);
    }

    public void Update(GameTime gameTime, float deltaTime) { }

    public void Draw(GameTime gameTime, float deltaTime, RenderTarget2D screenTarget)
    {        
        // Fix 1: Ensure render targets are cleared properly
        // Clear the render targets before drawing to them
        GraphicsDevice.SetRenderTarget(sceneTarget);
        GraphicsDevice.Clear(Color.Transparent);

        GraphicsDevice.SetRenderTarget(shadowTarget);
        GraphicsDevice.Clear(Color.White);

        GraphicsDevice.SetRenderTarget(lightTarget);
        GraphicsDevice.Clear(Color.Black);

        // Save original scene to our scene target
        GraphicsDevice.SetRenderTarget(sceneTarget);
        spriteBatch.Begin();
        spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
        spriteBatch.End();
        
        // Clear the screen target to black (for darkness)
        GraphicsDevice.SetRenderTarget(screenTarget);
        GraphicsDevice.Clear(Color.Black);
        
        // Draw the scene with ambient light level
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
        spriteBatch.Draw(sceneTarget, Vector2.Zero, Color.White);
        spriteBatch.End();

        // Generate shadow mask
        GraphicsDevice.SetRenderTarget(shadowTarget);
        GraphicsDevice.Clear(Color.White); // White = no shadow
        
        var proj = Camera.GetProjectionMatrix();
        var view = Camera.GetViewMatrix();
        var world = Matrix.Identity;

        // Fix 5: Validate camera matrices
        if (proj == Matrix.Identity || view == Matrix.Identity)
        {
            throw new InvalidOperationException("Camera matrices are not properly initialized.");
        }

        // Draw physics bodies as black shadows
        primitiveBatch.Begin(ref proj, ref view, ref world);
        foreach (var body in PhysicsWorld.BodyList)
        {
            Transform transform = body.GetTransform();
            foreach (var fixture in body.FixtureList)
            {
                if (fixture.Shape is PolygonShape)
                {
                    DrawShape(fixture, transform, Color.Black);
                }
            }
        }
        primitiveBatch.End();
        
        // Render lights to the light target
        GraphicsDevice.SetRenderTarget(lightTarget);
        GraphicsDevice.Clear(Color.Black);
        
      
        // Plain additive lights without shadows
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, null, null, null, null, Camera.GetTransformMatrix());
        
        // Draw all entity lights
        var withLightQuery = new QueryDescription().WithAll<LightComponent, PositionComponent>();
        World.Query(in withLightQuery, (Arch.Core.Entity entity, ref LightComponent light, ref PositionComponent position) =>
        {
            Vector2 worldPos = new Vector2(position.Position.X, position.Position.Y);
            
            spriteBatch.Draw(
                pointLightTexture,
                worldPos,
                null,
                light.Color * light.Intensity,
                0f,
                new Vector2(pointLightTexture.Width / 2, pointLightTexture.Height / 2),
                light.Size * 2 / TextureSize,
                SpriteEffects.None,
                0
            );
        });
        
        spriteBatch.End();
        
        GraphicsDevice.SetRenderTarget(screenTarget);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        spriteBatch.Draw(lightTarget, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    void DrawShape(Fixture fixture, Transform transform, Color color)
    {
        switch (fixture.Shape.ShapeType)
        {
            case ShapeType.Polygon:
            {
                PolygonShape polygonShape = (PolygonShape)fixture.Shape;
                int count = polygonShape.Vertices.Count;

                // Convert to pixels
                Vector2[] tempVertices = new Vector2[count];
                for (int j = 0; j < count; j++)
                {
                    tempVertices[j] = Transform.Multiply(polygonShape.Vertices[j], ref transform);
                }

                DrawSolidPolygon(tempVertices, count, color);
                break;
            }
            default:
                break;
        }
    }

    public void DrawSolidPolygon(Vector2[] vertices, int count, Color color)
    {
        if (!primitiveBatch.IsReady())
        {
            throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
        }

        if (count == 2)
        {
            return;
        }

        for (int i = 1; i < count - 1; i++)
        {
            primitiveBatch.AddVertex(ref vertices[0], color, PrimitiveType.TriangleList);
            primitiveBatch.AddVertex(ref vertices[i], color, PrimitiveType.TriangleList);
            primitiveBatch.AddVertex(ref vertices[i + 1], color, PrimitiveType.TriangleList);
        }
    }

    public void Dispose()
    {
        pointLightTexture?.Dispose();
        lightTarget?.Dispose();
        sceneTarget?.Dispose();
        shadowTarget?.Dispose();
    }
}
