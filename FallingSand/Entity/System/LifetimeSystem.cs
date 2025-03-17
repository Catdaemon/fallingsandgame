using System;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Collision;

namespace FallingSand.Entity.System;

class LifetimeSystem : ISystem
{
    private readonly World World;

    public LifetimeSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime)
    {
        var query = new QueryDescription().WithAll<LifetimeComponent>();
        World.Query(
            in query,
            (Arch.Core.Entity entity, ref LifetimeComponent lifetimeComponent) =>
            {
                if (
                    lifetimeComponent.CreatedTime + lifetimeComponent.LifeTime
                    < gameTime.TotalGameTime.TotalMilliseconds
                )
                {
                    World.Destroy(entity);
                }
            }
        );
    }
}
