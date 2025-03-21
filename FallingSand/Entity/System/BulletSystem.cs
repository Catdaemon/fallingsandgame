using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.System;

class BulletSystem : ISystem
{
    private readonly World World;

    public BulletSystem(World world)
    {
        World = world;
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

    public void Update(GameTime gameTime)
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
