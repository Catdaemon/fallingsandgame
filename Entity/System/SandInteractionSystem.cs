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

class SandInteractionSystemSystem : ISystem
{
    private readonly World World;
    private readonly FallingSandWorld.FallingSandWorld SandWorld;

    public SandInteractionSystemSystem(World world, FallingSandWorld.FallingSandWorld sandWorld)
    {
        World = world;
        SandWorld = sandWorld;
    }

    public void Update(GameTime gameTime)
    {
        var query = new QueryDescription().WithAll<SandPixelReaderComponent, PositionComponent>();
        World.Query(
            in query,
            (
                Arch.Core.Entity entity,
                ref SandPixelReaderComponent sandComponent,
                ref PositionComponent positionComponent
            ) =>
            {
                var position = positionComponent.Position;
                var sand = SandWorld.GetPixel(new WorldPosition((int)position.X, (int)position.Y));
                sandComponent.Material = sand.Data.Material;
            }
        );
    }
}
