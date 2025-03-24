using Arch.Core;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSand;

static partial class Util
{
    public static Arch.Core.Entity CreatePlayer(this World world, Vector2 position)
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
            new SpriteComponent("Player", "Idle", new Rectangle(0, 0, 16, 16)),
            new LightComponent(
                size: 100f,        // Size of the light
                intensity: 1.0f,   // Brightness multiplier
                color: Color.White,// Light color
                castShadows: true  // Whether this light casts shadows
            )
        );

        return player;
    }

    public static Arch.Core.Entity CreateChest(this World world, Vector2 position)
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

    public static Arch.Core.Entity CreateWeapon(
        this World world,
        Vector2 position,
        WeaponType weaponType
    )
    {
        var weaponComponent = new WeaponComponent(weaponType);
        var entity = world.Create(
            new PositionComponent(),
            new RectanglePhysicsBodyComponent
            {
                Width = 8,
                Height = 8,
                InitialPosition = position,
                Density = 1f,
            },
            new EquippableComponent(),
            weaponComponent,
            new SpriteComponent(
                weaponComponent.Config.SpriteName,
                "Idle",
                new Rectangle(0, 0, 16, 16)
            )
        );

        return entity;
    }
}
