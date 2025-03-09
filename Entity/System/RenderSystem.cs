using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Collision;

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
                var position = Convert.MetersToPixels(body.Position);
                // var radius = Convert.MetersToPixels(body.FixtureList[0].Shape.Radius);
                AABB aabb;
                body.FixtureList[0].GetAABB(out aabb, 0);

                var isColliding = body.ContactList != null;

                // Calculate the transform matrix for the physics object
                // Use the camera transform and the physics object's rotation
                var transformWithRotation =
                    Matrix.CreateTranslation(-position.X, -position.Y, 0)
                    * Matrix.CreateRotationZ(body.Rotation)
                    * Matrix.CreateTranslation(position.X, position.Y, 0)
                    * Camera.GetTransformMatrix();

                spriteBatch.Begin(transformMatrix: transformWithRotation);
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(
                        (int)position.X - (int)Convert.MetersToPixels(aabb.Width / 2),
                        (int)position.Y - (int)Convert.MetersToPixels(aabb.Height / 2),
                        (int)Convert.MetersToPixels(aabb.Width),
                        (int)Convert.MetersToPixels(aabb.Height)
                    ),
                    isColliding ? Color.Red : Color.Green
                );
                spriteBatch.End();
            }
        );
    }
}
