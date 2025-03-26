using System;
using System.Collections.Generic;
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
    private RenderTarget2D individualLightTarget;
    private RenderTarget2D shadowTarget;
    private const int TextureSize = 512;
    private Effect lightingEffect;
    private Effect blurEffect;

    // Custom blend state for multiply effect
    private BlendState multiplyBlend;

    // Shadow projection parameters
    private const float ShadowLength = 2000.0f; // Maximum shadow length in pixels
    private const float AmbientLightLevel = 0.5f; // Base light level (0-1)
    private const float ShadowSoftness = 0.5f; // How soft the shadow edges are (0-1)

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

        // Create custom multiply blend state
        multiplyBlend = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.Zero,
            ColorBlendFunction = BlendFunction.Add,

            AlphaSourceBlend = Blend.DestinationAlpha,
            AlphaDestinationBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add,
        };

        // Create render targets
        var pp = graphicsDevice.PresentationParameters;
        lightTarget = new RenderTarget2D(
            graphicsDevice,
            pp.BackBufferWidth,
            pp.BackBufferHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
        shadowTarget = new RenderTarget2D(
            graphicsDevice,
            pp.BackBufferWidth,
            pp.BackBufferHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );
        individualLightTarget = new RenderTarget2D(
            graphicsDevice,
            pp.BackBufferWidth,
            pp.BackBufferHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        // Load lighting effect
        lightingEffect = contentManager.Load<Effect>("Shaders/LightBlend");
        lightingEffect.Parameters["LightMap"].SetValue(lightTarget);

        // Load blur effect for shadow smoothing
        blurEffect = contentManager.Load<Effect>("Shaders/GaussianBlur");

        // Set resolution parameter for the blur shader
        blurEffect
            .Parameters["Resolution"]
            .SetValue(
                new Vector2(
                    graphicsDevice.PresentationParameters.BackBufferWidth,
                    graphicsDevice.PresentationParameters.BackBufferHeight
                )
            );

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
                intensity = intensity * intensity;
                colors[x + y * TextureSize] = Color.White * intensity;
            }
        }
        pointLightTexture.SetData(colors);
    }

    public void Update(GameTime gameTime, float deltaTime) { }

    public void Draw(GameTime gameTime, float deltaTime, RenderTarget2D screenTarget)
    {
        // Set initial ambient light
        GraphicsDevice.SetRenderTarget(lightTarget);
        GraphicsDevice.Clear(new Color(AmbientLightLevel, AmbientLightLevel, AmbientLightLevel));

        // Draw all entity lights with shadows
        var withLightQuery = new QueryDescription().WithAll<LightComponent, PositionComponent>();
        World.Query(
            in withLightQuery,
            (Arch.Core.Entity entity, ref LightComponent light, ref PositionComponent position) =>
            {
                Vector2 worldPos = new(position.Position.X, position.Position.Y);
                Vector2 screenPos = Camera.WorldToScreenPosition(worldPos);

                // Set up the render target for this light
                GraphicsDevice.SetRenderTarget(individualLightTarget);
                GraphicsDevice.Clear(Color.Black);

                // Draw the light to the individual light target
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    null,
                    null,
                    null,
                    null,
                    Camera.GetTransformMatrix()
                );
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
                spriteBatch.End();

                // Prepare to draw shadow polygons
                GraphicsDevice.SetRenderTarget(shadowTarget);
                GraphicsDevice.Clear(Color.White); // White = no shadow

                // Set up the matrices for primitive batch
                var proj = Matrix.CreateOrthographicOffCenter(
                    0,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height,
                    0,
                    0,
                    1
                );
                var view = Matrix.Identity;
                var world = Matrix.Identity;

                // Begin drawing shadow casters
                primitiveBatch.Begin(ref proj, ref view, ref world);

                // Process each physics body for shadow casting
                foreach (var body in PhysicsWorld.BodyList)
                {
                    Transform transform = body.GetTransform();

                    foreach (var fixture in body.FixtureList)
                    {
                        if (fixture.Shape is PolygonShape polygonShape)
                        {
                            // Process vertices and cast shadows
                            ProjectShadowPolygon(polygonShape, transform, screenPos, light.Size);
                        }
                    }
                }

                primitiveBatch.End();

                // Apply blur to the shadow map to smooth edges between adjacent bodies
                ApplyBlurToShadowMap();

                // Apply the shadow mask to the light
                GraphicsDevice.SetRenderTarget(individualLightTarget);
                spriteBatch.Begin(SpriteSortMode.Immediate, multiplyBlend);
                spriteBatch.Draw(shadowTarget, Vector2.Zero, Color.White);
                spriteBatch.End();

                // Add the finished light to the light target
                GraphicsDevice.SetRenderTarget(lightTarget);
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
                spriteBatch.Draw(individualLightTarget, Vector2.Zero, Color.White);
                spriteBatch.End();
            }
        );

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Debug draw the render targets
        spriteBatch.Begin();
        spriteBatch.Draw(
            screenTarget,
            new Rectangle(0, 0, screenTarget.Width / 2, screenTarget.Height / 2),
            Color.White
        );
        spriteBatch.Draw(
            shadowTarget,
            new Rectangle(
                screenTarget.Width / 2,
                0,
                screenTarget.Width / 2,
                screenTarget.Height / 2
            ),
            Color.White
        );
        spriteBatch.Draw(
            lightTarget,
            new Rectangle(
                0,
                screenTarget.Height / 2,
                screenTarget.Width / 2,
                screenTarget.Height / 2
            ),
            Color.White
        );
        spriteBatch.End();

        spriteBatch.Begin(effect: lightingEffect);
        // bottom right
        spriteBatch.Draw(
            screenTarget,
            new Rectangle(
                screenTarget.Width / 2,
                screenTarget.Height / 2,
                screenTarget.Width / 2,
                screenTarget.Height / 2
            ),
            Color.White
        );
        spriteBatch.End();
    }

    private void ApplyBlurToShadowMap()
    {
        // Skip this if blur effect isn't loaded
        if (blurEffect == null)
            return;

        // Create a temporary target for the blur operations
        var tempTarget = new RenderTarget2D(
            GraphicsDevice,
            shadowTarget.Width,
            shadowTarget.Height,
            false,
            shadowTarget.Format,
            DepthFormat.None
        );

        try
        {
            // Update the resolution parameter (in case of window resize)
            blurEffect
                .Parameters["Resolution"]
                .SetValue(new Vector2(shadowTarget.Width, shadowTarget.Height));

            // Apply horizontal blur
            blurEffect.Parameters["BlurAmount"].SetValue(2.0f); // Adjust this value as needed
            blurEffect.CurrentTechnique = blurEffect.Techniques["HorizontalBlur"];

            // Render horizontal blur to temp target
            GraphicsDevice.SetRenderTarget(tempTarget);
            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                null,
                null,
                null,
                blurEffect
            );
            spriteBatch.Draw(shadowTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            // Apply vertical blur
            blurEffect.CurrentTechnique = blurEffect.Techniques["VerticalBlur"];

            // Render vertical blur back to shadow target
            GraphicsDevice.SetRenderTarget(shadowTarget);
            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                null,
                null,
                null,
                blurEffect
            );
            spriteBatch.Draw(tempTarget, Vector2.Zero, Color.White);
            spriteBatch.End();
        }
        finally
        {
            // Clean up temporary resources
            tempTarget?.Dispose();
        }
    }

    private void ProjectShadowPolygon(
        PolygonShape polygonShape,
        Transform transform,
        Vector2 lightScreenPos,
        float lightSize
    )
    {
        int vertexCount = polygonShape.Vertices.Count;
        if (vertexCount < 3)
            return;

        // Transform polygon vertices to screen space
        List<Vector2> screenVertices = new List<Vector2>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 worldVertex = Transform.Multiply(polygonShape.Vertices[i], ref transform);
            // Convert physics world vertex to screen space
            worldVertex = worldVertex * Constants.PIXELS_TO_METERS;
            Vector2 screenVertex = Camera.WorldToScreenPosition(
                new Vector2(worldVertex.X, worldVertex.Y)
            );
            screenVertices.Add(screenVertex);
        }

        // Process each edge of the polygon for shadow casting
        for (int i = 0; i < vertexCount; i++)
        {
            int j = (i + 1) % vertexCount;

            Vector2 v1 = screenVertices[i];
            Vector2 v2 = screenVertices[j];

            // Calculate edge normal (perpendicular to edge, pointing outward)
            Vector2 edge = v2 - v1;
            Vector2 normal = new Vector2(-edge.Y, edge.X);
            normal.Normalize();

            // Calculate direction from light to edge midpoint
            Vector2 midPoint = (v1 + v2) * 0.5f;
            Vector2 lightToMid = midPoint - lightScreenPos;
            float distanceToLight = lightToMid.Length();

            // If the edge is facing away from the light, cast a shadow
            if (Vector2.Dot(normal, lightToMid) > 0)
            {
                // Project vertices away from light
                Vector2 v1Proj = ProjectVertex(v1, lightScreenPos);
                Vector2 v2Proj = ProjectVertex(v2, lightScreenPos);

                // Calculate shadow intensity based on distance from light
                // Closer to light = darker shadow, farther = more transparent
                float shadowIntensity = MathHelper.Clamp(
                    1.0f - (distanceToLight / (lightSize * TextureSize * 0.75f)) * ShadowSoftness,
                    0.0f,
                    1.0f
                );

                // Color with variable alpha for soft shadows
                Color shadowColor = new Color(0, 0, 0, shadowIntensity);

                // Draw the shadow with variable alpha
                primitiveBatch.AddVertex(ref v1, shadowColor, PrimitiveType.TriangleList);
                primitiveBatch.AddVertex(ref v2, shadowColor, PrimitiveType.TriangleList);
                primitiveBatch.AddVertex(ref v2Proj, shadowColor, PrimitiveType.TriangleList);

                primitiveBatch.AddVertex(ref v1, shadowColor, PrimitiveType.TriangleList);
                primitiveBatch.AddVertex(ref v2Proj, shadowColor, PrimitiveType.TriangleList);
                primitiveBatch.AddVertex(ref v1Proj, shadowColor, PrimitiveType.TriangleList);
            }
        }
    }

    private Vector2 ProjectVertex(Vector2 vertex, Vector2 lightPos)
    {
        // Calculate direction from light to vertex
        Vector2 direction = vertex - lightPos;

        // Handle the case where the vertex is at the light position
        if (direction == Vector2.Zero)
        {
            direction = new Vector2(0, -1); // Default direction if at light center
        }
        else
        {
            direction.Normalize();
        }

        // Project the vertex outward by ShadowLength
        return vertex + direction * ShadowLength;
    }

    public void Dispose()
    {
        pointLightTexture?.Dispose();
        lightTarget?.Dispose();
        shadowTarget?.Dispose();
        individualLightTarget?.Dispose();
        multiplyBlend?.Dispose();
        blurEffect?.Dispose();
    }
}
