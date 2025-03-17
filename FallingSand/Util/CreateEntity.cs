using Arch.Core;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSand;

static partial class Util
{
    public static Arch.Core.Entity CreatePlayer(this World world, WorldPosition position)
    {
        var player = world.Create(
            new PositionComponent(),
            new BoundingBoxComponent(),
            new HealthComponent() { Current = 100, Max = 100 },
            new InputReceiverComponent(),
            new InputStateComponent(),
            new CameraFollowComponent(),
            new CapsulePhysicsBodyComponent()
            {
                Height = 16,
                Width = 8,
                InitialPosition = position,
                CreateSensors = true,
                Density = 1f,
            },
            new SandPixelReaderComponent(),
            new JetpackComponent(100),
            new SpriteComponent("Player", "Idle", new Rectangle(0, 0, 16, 16))
        );

        return player;
    }

    public static Arch.Core.Entity CreateChest(this World world, WorldPosition position)
    {
        var entity = world.Create(
            new PositionComponent(),
            new RectanglePhysicsBodyComponent
            {
                Width = 32,
                Height = 16,
                InitialPosition = position,
                Density = 100f,
            },
            new SpriteComponent("Chest", "Idle", new Rectangle(0, 0, 32, 32))
        );

        return entity;
    }
}
