using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.System;

class WeaponSystem : ISystem
{
    private readonly World World;

    public WeaponSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime, float deltaTime)
    {
        // A weapon has a factory component and an equippable component
        var query = new QueryDescription().WithAll<
            PositionComponent,
            EquippableComponent,
            WeaponComponent
        >();
        World.Query(
            in query,
            (
                Arch.Core.Entity entity,
                ref PositionComponent position,
                ref EquippableComponent equippable,
                ref WeaponComponent weapon
            ) =>
            {
                var hasParent =
                    equippable.Parent != null && (equippable.Parent?.IsAlive() ?? false);
                if (equippable.IsActive && !hasParent)
                {
                    // Parent entity was deleted
                    equippable.IsActive = false;
                    return;
                }
                if (!hasParent)
                {
                    // On the ground
                    return;
                }

                // Update weapon position
                if (equippable.Parent.Value.Has<PositionComponent>())
                {
                    position.Position = equippable.Parent.Value.Get<PositionComponent>().Position;
                }
                // Update from inputs
                if (equippable.Parent.Value.Has<InputStateComponent>())
                {
                    var inputState = equippable.Parent.Value.Get<InputStateComponent>();
                    position.Angle = Convert.VectorToAngle(inputState.Value.AimVector);
                    position.FacingDirection = inputState.Value.AimVector;

                    if (inputState.Value.Shoot)
                    {
                        // Fire rate is bullets per second
                        var fireRate = 1000 / weapon.Config.FireRate;
                        var canFire =
                            weapon.LastFireTime + fireRate
                            < gameTime.TotalGameTime.TotalMilliseconds;

                        if (canFire)
                        {
                            weapon.LastFireTime = (float)gameTime.TotalGameTime.TotalMilliseconds;

                            // Fire weapon
                            // Create bullet entity
                            var emissionPoint = position.Position + position.FacingDirection * 2;
                            var bulletVelocity =
                                position.FacingDirection * weapon.Config.BulletSpeed;
                            var bullet = World.Create(
                                new PositionComponent()
                                {
                                    Position = position.Position,
                                    Velocity = bulletVelocity,
                                },
                                new CirclePhysicsBodyComponent()
                                {
                                    Radius = 3f,
                                    Density = 1f,
                                    InitialPosition = emissionPoint,
                                    InitialVelocity = bulletVelocity,
                                },
                                new BulletComponent()
                                {
                                    Damage = weapon.Config.Damage,
                                    Speed = weapon.Config.BulletSpeed,
                                    Source = equippable.Parent ?? entity,
                                    BulletBehaviours = weapon.Config.BulletBehaviours,
                                    LifeTime = 1000f,
                                    CreationTime = (float)gameTime.TotalGameTime.TotalMilliseconds,
                                },
                                new SpriteComponent(
                                    weapon.Config.BulletSpriteName,
                                    "Idle",
                                    new Rectangle(0, 0, 4, 4)
                                )
                            );
                        }
                    }
                }
            }
        );
    }
}
