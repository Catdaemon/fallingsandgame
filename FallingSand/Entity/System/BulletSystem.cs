using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.System;

class BulletSystem : ISystem
{
    private readonly World World;
    private readonly FallingSandWorld.FallingSandWorld SandWorld;

    public BulletSystem(World world, FallingSandWorld.FallingSandWorld sandWorld)
    {
        World = world;
        SandWorld = sandWorld;
    }

    public void OnBulletCollision(
        ref Arch.Core.Entity entity,
        ref PositionComponent position,
        ref BulletComponent bullet
    )
    {
        if (bullet.BulletBehaviours.Contains(BulletBehaviour.Explode))
        {
            // Emit explosion
            SandWorld.ExplodePixels(
                new WorldPosition((int)(position.Position.X), (int)(position.Position.Y)),
                10
            );
        }
        else
        {
            SandWorld.DisruptPixels(
                new WorldPosition((int)(position.Position.X), (int)(position.Position.Y)),
                10
            );
        }

        if (!bullet.BulletBehaviours.Contains(BulletBehaviour.Bounce))
        {
            if (entity.IsAlive())
            {
                World.Destroy(entity);
            }
        }

        bullet.HasCollided = false;
    }

    public void Update(GameTime gameTime, float deltaTime)
    {
        var query = new QueryDescription().WithAll<PositionComponent, BulletComponent>();
        World.Query(
            in query,
            (Arch.Core.Entity entity, ref PositionComponent position, ref BulletComponent bullet) =>
            {
                if (bullet.HasCollided)
                {
                    OnBulletCollision(ref entity, ref position, ref bullet);
                }

                // Update lifetime
                if (
                    bullet.CreationTime + bullet.LifeTime
                    < gameTime.TotalGameTime.TotalMilliseconds
                )
                {
                    // Count as collided with the world
                    OnBulletCollision(ref entity, ref position, ref bullet);
                }
            }
        );
    }
}
