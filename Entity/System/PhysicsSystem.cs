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
        PhysicsWorld.Gravity = new nkast.Aether.Physics2D.Common.Vector2(0, 10f);
    }

    public void CreatePhysicsBodies()
    {
        // Find entities with a physics component but no created physics body
        var query = new QueryDescription().WithAny<CirclePhysicsBodyComponent>();
        ECSWorld.Query(
            in query,
            (Arch.Core.Entity entity, ref CirclePhysicsBodyComponent circle) =>
            {
                if (!entity.Has<PhysicsBodyComponent>())
                {
                    // Create a physics body for the entity
                    if (circle != null)
                    {
                        var bodyRef = PhysicsWorld.CreateCircle(
                            circle.Radius,
                            circle.Density,
                            new nkast.Aether.Physics2D.Common.Vector2(
                                circle.InitialPosition.X,
                                circle.InitialPosition.Y
                            ),
                            BodyType.Dynamic
                        );
                        entity.Add(new PhysicsBodyComponent(bodyRef));
                    }
                }
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
                        new nkast.Aether.Physics2D.Common.Vector2(
                            inputState.Left - inputState.Right,
                            inputState.Up - inputState.Down
                        ) * -100f;
                    physicsBody.PhysicsBodyRef.ApplyForce(inputVector);
                }
                if (entity.Has<PositionComponent>())
                {
                    // Update the position based on the physics object's position
                    entity.Get<PositionComponent>().Value = new Microsoft.Xna.Framework.Vector2(
                        physicsBody.PhysicsBodyRef.WorldCenter.X,
                        physicsBody.PhysicsBodyRef.WorldCenter.Y
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
