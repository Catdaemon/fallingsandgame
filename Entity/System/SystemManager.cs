using System.Collections.Generic;
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

    public SystemManager(World world)
    {
        World = world;
    }

    public void RegisterSystems(PhysicsWorld physicsWorld)
    {
        AddSystem(new InputSystem(World));
        AddSystem(new CameraSystem(World));
        AddSystem(new PhysicsSystem(World, physicsWorld));
        AddSystem(new RenderSystem(World));
    }

    private void AddSystem(ISystem system)
    {
        Systems.Add(system);
    }

    public void Update(GameTime gameTime)
    {
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
