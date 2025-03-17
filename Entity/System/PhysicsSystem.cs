using System;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
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
        PhysicsWorld.Gravity = new Vector2(0, 9.8f);
        // PhysicsWorld.Gravity = new Vector2(0, 0f);

        // Ensure that physics bodies are removed when their entities are removed
        ECSWorld.SubscribeComponentRemoved<PhysicsBodyComponent>(OnComponentRemoved);
        ECSWorld.SubscribeEntityDestroyed(OnEntityDestroyed);
    }

    private void OnComponentRemoved(
        in Arch.Core.Entity entity,
        ref PhysicsBodyComponent physicsBody
    )
    {
        PhysicsWorld.Remove(physicsBody.PhysicsBody);
    }

    private void OnEntityDestroyed(in Arch.Core.Entity entity)
    {
        if (entity.Has<PhysicsBodyComponent>())
        {
            PhysicsWorld.Remove(entity.Get<PhysicsBodyComponent>().PhysicsBody);
        }
    }

    private struct CreatePhysicsBody : IForEach
    {
        public nkast.Aether.Physics2D.Dynamics.World PhysicsWorld;

        public static void CreateCollisionSensor(
            Body body,
            float width,
            float height,
            Vector2 offset,
            Action onCollision,
            Action onSeparation
        )
        {
            var sensor = body.CreateRectangle(width, height, 0, offset);
            sensor.IsSensor = true;
            sensor.OnCollision += (fixtureA, fixtureB, contact) =>
            {
                onCollision();
                return true;
            };
            sensor.OnSeparation += (fixtureA, fixtureB, contact) =>
            {
                onSeparation();
            };
        }

        public static void CreateDirectionalCollisionSensors(Body body, Arch.Core.Entity entity)
        {
            var bodyWidth = Util.GetPhysicsBodyWidth(body);
            var bodyHeight = Util.GetPhysicsBodyHeight(body);

            var sensorDistance = 0.0f;
            var sensorScale = 0.5f;

            // Create sensors for each side of the body
            CreateCollisionSensor(
                body,
                bodyWidth * sensorScale,
                sensorDistance,
                new Vector2(0, -bodyHeight * 0.5f - sensorDistance),
                // TODO: does this crash if the ent is removed?
                () => entity.Get<PhysicsBodyComponent>().TopCollisionCount++,
                () => entity.Get<PhysicsBodyComponent>().TopCollisionCount--
            );
            CreateCollisionSensor(
                body,
                sensorDistance,
                bodyHeight * sensorScale,
                new Vector2(-bodyWidth * 0.5f - sensorDistance, 0),
                () => entity.Get<PhysicsBodyComponent>().LeftCollisionCount++,
                () => entity.Get<PhysicsBodyComponent>().LeftCollisionCount--
            );
            CreateCollisionSensor(
                body,
                sensorDistance,
                bodyHeight * sensorScale,
                new Vector2(bodyWidth * 0.5f + sensorDistance, 0),
                () => entity.Get<PhysicsBodyComponent>().RightCollisionCount++,
                () => entity.Get<PhysicsBodyComponent>().RightCollisionCount--
            );

            // The bottom sensor is a bit different, it needs to calculate the ground normal
            var bottomSensor = body.CreateRectangle(
                bodyWidth * sensorScale,
                sensorDistance,
                0,
                new Vector2(0, bodyHeight * 0.5f + sensorDistance)
            );
            bottomSensor.IsSensor = true;
            bottomSensor.OnCollision += (fixtureA, fixtureB, contact) =>
            {
                var component = entity.Get<PhysicsBodyComponent>();
                component.BottomCollisionCount++;

                return true;
            };
            bottomSensor.OnSeparation += (fixtureA, fixtureB, contact) =>
            {
                entity.Get<PhysicsBodyComponent>().BottomCollisionCount--;
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Update(Arch.Core.Entity entity)
        {
            Body createdBody = null;
            if (entity.Has<ParticleComponent, PositionComponent>())
            {
                var particle = entity.Get<ParticleComponent>();
                var position = entity.Get<PositionComponent>();
                createdBody = PhysicsWorld.CreateCircle(
                    Convert.PixelsToMeters(particle.Size),
                    density: 0,
                    Convert.PixelsToMeters(new Vector2(position.Position.X, position.Position.Y)),
                    bodyType: BodyType.Dynamic
                );
                createdBody.IsBullet = true;
                createdBody.LinearVelocity = position.Velocity;
            }
            if (entity.Has<CirclePhysicsBodyComponent>())
            {
                var circle = entity.Get<CirclePhysicsBodyComponent>();
                createdBody = PhysicsWorld.CreateCircle(
                    Convert.PixelsToMeters(circle.Radius),
                    circle.Density,
                    Convert.PixelsToMeters(
                        new Vector2(circle.InitialPosition.X, circle.InitialPosition.Y)
                    ),
                    bodyType: BodyType.Dynamic
                );
                if (circle.CreateSensors)
                {
                    CreateDirectionalCollisionSensors(createdBody, entity);
                }
            }
            if (entity.Has<RectanglePhysicsBodyComponent>())
            {
                var rect = entity.Get<RectanglePhysicsBodyComponent>();
                createdBody = PhysicsWorld.CreateRectangle(
                    Convert.PixelsToMeters(rect.Width),
                    Convert.PixelsToMeters(rect.Height),
                    rect.Density,
                    Convert.PixelsToMeters(
                        new Vector2(rect.InitialPosition.X, rect.InitialPosition.Y)
                    ),
                    rotation: 0,
                    bodyType: BodyType.Dynamic
                );
                if (rect.CreateSensors)
                {
                    CreateDirectionalCollisionSensors(createdBody, entity);
                }
            }
            if (entity.Has<CapsulePhysicsBodyComponent>())
            {
                var capsule = entity.Get<CapsulePhysicsBodyComponent>();
                createdBody = PhysicsWorld.CreateCapsule(
                    Convert.PixelsToMeters(capsule.Height) / 2,
                    Convert.PixelsToMeters(capsule.Width) / 2,
                    capsule.Density,
                    Convert.PixelsToMeters(
                        new Vector2(capsule.InitialPosition.X, capsule.InitialPosition.Y)
                    ),
                    rotation: 0,
                    bodyType: BodyType.Dynamic
                );
                if (capsule.CreateSensors)
                {
                    CreateDirectionalCollisionSensors(createdBody, entity);
                }
            }
            if (createdBody != null)
            {
                entity.Add(new PhysicsBodyComponent { PhysicsBody = createdBody });
            }
        }
    }

    private static readonly QueryDescription createPhysicsBodyQuery = new QueryDescription()
        .WithAny<
            CirclePhysicsBodyComponent,
            RectanglePhysicsBodyComponent,
            CapsulePhysicsBodyComponent,
            ParticleComponent
        >()
        .WithNone<PhysicsBodyComponent>();

    private void CreatePhysicsBodies()
    {
        var queryObject = new CreatePhysicsBody { PhysicsWorld = PhysicsWorld };
        ECSWorld.InlineQuery<CreatePhysicsBody>(in createPhysicsBodyQuery, ref queryObject);
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
                if (physicsBody.PhysicsBody == null)
                {
                    throw new Exception(
                        "Physics body is null in PhysicsBodyComponent - do not instantiate PhysicsBodyComponent outside of PhysicsSystem"
                    );
                }
                // if (entity.Has<InputStateComponent>())
                // {
                //     // Update the physics object based on the input state
                //     var inputState = entity.Get<InputStateComponent>().Value;
                //     var inputVector =
                //         new Vector2(
                //             inputState.Left - inputState.Right,
                //             inputState.Up - inputState.Down
                //         ) * -1f;
                //     physicsBody.PhysicsBody.ApplyLinearImpulse(Convert.PixelsToMeters(inputVector));
                // }
                if (entity.Has<PositionComponent>())
                {
                    var positionComponent = entity.Get<PositionComponent>();
                    // Update the position based on the physics object's position
                    positionComponent.Position = Convert.MetersToPixels(
                        physicsBody.PhysicsBody.WorldCenter
                    );
                    positionComponent.Velocity = Convert.MetersToPixels(
                        physicsBody.PhysicsBody.LinearVelocity
                    );
                    // Update the facing direction if we are moving
                    if (Math.Abs(positionComponent.Velocity.X) > 1f)
                    {
                        positionComponent.FacingDirection.X = positionComponent.Velocity.X;
                    }
                    if (Math.Abs(positionComponent.Velocity.Y) > 1f)
                    {
                        positionComponent.FacingDirection.Y = positionComponent.Velocity.Y;
                    }
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
