using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using PhysicsWorld = nkast.Aether.Physics2D.Dynamics.World;
using World = Arch.Core.World;

namespace FallingSand.Entity.System;

class SystemManager
{
    private readonly World World;
    private readonly List<ISystem> Systems = [];
    private ISystem PhysicsSystem;
    private long lastUpdateTime = -1;
    private const int updateInterval = 1000 / 60;

    public SystemManager(World world)
    {
        World = world;
    }

    public void RegisterSystems(
        PhysicsWorld physicsWorld,
        FallingSandWorld.FallingSandWorld sandWorld
    )
    {
        AddSystem(new InputSystem(World));
        AddSystem(new CameraSystem(World));

        PhysicsSystem = new PhysicsSystem(World, physicsWorld);
        AddSystem(PhysicsSystem);

        AddSystem(new SandInteractionSystemSystem(World, sandWorld));

        AddSystem(new RenderSystem(World));
    }

    private void AddSystem(ISystem system)
    {
        Systems.Add(system);
    }

    public void Update(GameTime gameTime)
    {
        // Update systems, except the physics system, at a fixed interval
        if (
            lastUpdateTime != -1
            && gameTime.TotalGameTime.TotalMilliseconds - lastUpdateTime < updateInterval
        )
        {
            // Update the physics system as fast as possible
            // var physicsSystem = Systems.First(system => system is PhysicsSystem);
            // physicsSystem.Update(gameTime);
            PhysicsSystem.Update(gameTime);
            return;
        }

        lastUpdateTime = (long)gameTime.TotalGameTime.TotalMilliseconds;

        foreach (var system in Systems)
        {
            system.Update(gameTime);
        }
    }

    public void Draw(GameTime gameTime)
    {
        foreach (var system in Systems)
        {
            system.Draw(gameTime);
        }
    }

    public void InitializeGraphics(GraphicsDevice graphicsDevice)
    {
        foreach (var system in Systems)
        {
            system.InitializeGraphics(graphicsDevice);
        }
    }
}
