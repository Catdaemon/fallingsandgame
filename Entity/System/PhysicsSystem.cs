using System;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.Entity.System;

class PhysicsSystem : ISystem
{
    private readonly nkast.Aether.Physics2D.Dynamics.World PhysicsWorld;
    private readonly Arch.Core.World ECSWorld;

    public PhysicsSystem(
        Arch.Core.World ecsWorld,
        nkast.Aether.Physics2D.Dynamics.World physicsWorld
    )
    {
        ECSWorld = ecsWorld;
        PhysicsWorld = physicsWorld;
        // PhysicsWorld.Gravity = new Vector2(0, 9.8f);
        PhysicsWorld.Gravity = new Vector2(0, 0);
    }

    private void CreatePhysicsBodies()
    {
        // Find entities with a physics component but no created physics body
        var circleQuery = new QueryDescription()
            .WithAll<CirclePhysicsBodyComponent>()
            .WithNone<PhysicsBodyComponent>();
        var rectangleQuery = new QueryDescription()
            .WithAll<RectanglePhysicsBodyComponent>()
            .WithNone<PhysicsBodyComponent>();

        ECSWorld.Query(
            in circleQuery,
            (Arch.Core.Entity entity, ref CirclePhysicsBodyComponent circle) =>
            {
                // Create a physics body for the entity
                var bodyRef = PhysicsWorld.CreateCircle(
                    Convert.PixelsToMeters(circle.Radius),
                    circle.Density,
                    Convert.PixelsToMeters(
                        new Vector2(circle.InitialPosition.X, circle.InitialPosition.Y)
                    ),
                    BodyType.Dynamic
                );
                entity.Add(new PhysicsBodyComponent(bodyRef));
            }
        );

        ECSWorld.Query(
            in rectangleQuery,
            (Arch.Core.Entity entity, ref RectanglePhysicsBodyComponent rect) =>
            {
                var bodyRef = PhysicsWorld.CreateRectangle(
                    Convert.PixelsToMeters(rect.Width),
                    Convert.PixelsToMeters(rect.Height),
                    rect.Density,
                    Convert.PixelsToMeters(
                        new Vector2(rect.InitialPosition.X, rect.InitialPosition.Y)
                    ),
                    rotation: 0,
                    BodyType.Dynamic
                );
                entity.Add(new PhysicsBodyComponent(bodyRef));
            }
        );
    }

    public void Update(GameTime gameTime)
    {
        CreatePhysicsBodies();

        // Find entities with a PhysicsBody component
        var query = new QueryDescription().WithAny<PhysicsBodyComponent>();
        ECSWorld.Query(
            in query,
            (Arch.Core.Entity entity, ref PhysicsBodyComponent physicsBody) =>
            {
                if (entity.Has<InputStateComponent>())
                {
                    // Update the physics object based on the input state
                    var inputState = entity.Get<InputStateComponent>().Value;
                    var inputVector =
                        new Vector2(
                            inputState.Left - inputState.Right,
                            inputState.Up - inputState.Down
                        ) * -10f;
                    physicsBody.PhysicsBodyRef.ApplyLinearImpulse(
                        Convert.PixelsToMeters(inputVector)
                    );
                }
                if (entity.Has<PositionComponent>())
                {
                    var positionComponent = entity.Get<PositionComponent>();
                    // Update the position based on the physics object's position
                    positionComponent.Position = Convert.MetersToPixels(
                        physicsBody.PhysicsBodyRef.WorldCenter
                    );
                    positionComponent.Velocity = Convert.MetersToPixels(
                        physicsBody.PhysicsBodyRef.LinearVelocity
                    );
                }
                if (entity.Has<BoundingBoxComponent>())
                {
                    // Update the bounding box based on the physics object
                    // entity.Get<BoundingBoxComponent>().Value = physicsBody.PhysicsBodyRef.FixtureList[0].Shape.ComputeAABB();
                }
            }
        );
        PhysicsWorld.Step(Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f));
    }
}
