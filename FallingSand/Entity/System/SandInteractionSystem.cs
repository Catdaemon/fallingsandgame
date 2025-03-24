using System;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using FallingSand.FallingSandRenderer;
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
    private readonly GameWorld GameWorld;

    public SandInteractionSystemSystem(
        World world,
        FallingSandWorld.FallingSandWorld sandWorld,
        GameWorld gameWorld
    )
    {
        World = world;
        SandWorld = sandWorld;
        GameWorld = gameWorld;
    }

    public void Update(GameTime gameTime, float deltaTime)
    {
        GameWorld.ResetOccupiedChunks();

        var presenceQuery = new QueryDescription().WithAll<PositionComponent>();
        World.Query(
            in presenceQuery,
            (Arch.Core.Entity entity, ref PositionComponent positionComponent) =>
            {
                var position = positionComponent.Position;
                GameWorld.SetChunkOccupiedAt(position);
            }
        );

        var pixelReaderQuery = new QueryDescription().WithAll<
            SandPixelReaderComponent,
            PositionComponent
        >();
        World.Query(
            in pixelReaderQuery,
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
