using System.Collections.Generic;
using Arch.Core;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.System;

class SystemManager
{
    private readonly World World;
    private readonly List<ISystem> Systems = [];

    public SystemManager(World world)
    {
        World = world;
    }

    public void RegisterSystems()
    {
        AddSystem(new InputSystem(World));
        AddSystem(new CameraSystem(World));
        AddSystem(new PhysicsSystem(World));
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
}
