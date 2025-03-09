using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FallingSand.Entity.System;

class RenderSystem : ISystem
{
    private readonly World World;
    private SpriteBatch spriteBatch;
    private Texture2D pixelTexture;

    public RenderSystem(World world)
    {
        World = world;
    }

    public void InitializeGraphics(GraphicsDevice graphicsDevice)
    {
        spriteBatch = new SpriteBatch(graphicsDevice);
        pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(GameTime gameTime) { }

    public void Draw(GameTime gameTime)
    {
        // Debug draw physics objects
        var query = new QueryDescription().WithAny<PhysicsBodyComponent>();
        World.Query(
            in query,
            (Arch.Core.Entity entity, ref PhysicsBodyComponent physicsBody) =>
            {
                var body = physicsBody.PhysicsBodyRef;
                var position = body.Position;
                var radius = body.FixtureList[0].Shape.Radius;

                spriteBatch.Begin(transformMatrix: Camera.GetTransformMatrix());
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(
                        (int)(position.X - radius),
                        (int)(position.Y - radius),
                        (int)(radius * 2),
                        (int)(radius * 2)
                    ),
                    Color.Red
                );
                spriteBatch.End();
            }
        );
    }
}
