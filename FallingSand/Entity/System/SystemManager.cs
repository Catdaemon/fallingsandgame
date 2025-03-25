using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using FallingSand.FallingSandRenderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using PhysicsWorld = nkast.Aether.Physics2D.Dynamics.World;
using World = Arch.Core.World;

namespace FallingSand.Entity.System;

class SystemManager
{
    private readonly World World;
    private readonly List<ISystem> Systems = [];
    private RenderTarget2D screenTarget;
    private SpriteBatch spriteBatch;
    private GraphicsDevice graphicsDevice;

    public SystemManager(World world)
    {
        World = world;
    }

    public void RegisterSystems(
        PhysicsWorld physicsWorld,
        FallingSandWorld.FallingSandWorld sandWorld,
        GameWorld gameWorld,
        GraphicsDevice graphicsDevice,
        ContentManager contentManager
    )
    {
        AddSystem(new InputSystem(World));

        AddSystem(new PhysicsSystem(World, physicsWorld));

        AddSystem(new KinematicMovementSystem(World));
        AddSystem(new WeaponSystem(World));
        AddSystem(new BulletSystem(World, sandWorld));
        AddSystem(new CameraSystem(World));
        AddSystem(new SandInteractionSystemSystem(World, sandWorld, gameWorld));
        AddSystem(new LifetimeSystem(World));
        AddSystem(new RenderSystem(World, graphicsDevice));
        AddSystem(new WorldRenderSystem(gameWorld, graphicsDevice));
        AddSystem(new LightingSystem(World, physicsWorld, graphicsDevice));
        AddSystem(new HudSystem(World, graphicsDevice));

        screenTarget = new RenderTarget2D(
            graphicsDevice,
            graphicsDevice.PresentationParameters.BackBufferWidth,
            graphicsDevice.PresentationParameters.BackBufferHeight
        );
        spriteBatch = new SpriteBatch(graphicsDevice);
        this.graphicsDevice = graphicsDevice;

        InitializeGraphics(graphicsDevice, contentManager);

        // foreach (var system in Systems)
        // {
        //     system.Update(new GameTime(), 0);
        // }
    }

    private void AddSystem(ISystem system)
    {
        Systems.Add(system);
    }

    public void Update(GameTime gameTime, float deltaTime)
    {
        foreach (var system in Systems)
        {
            system.Update(gameTime, deltaTime);
        }
    }

    public void Draw(GameTime gameTime, float deltaTime)
    {
        graphicsDevice.SetRenderTarget(screenTarget);
        graphicsDevice.Clear(Color.CornflowerBlue);

        foreach (var system in Systems)
        {
            system.Draw(gameTime, deltaTime, screenTarget);
        }        
    }

    private void InitializeGraphics(GraphicsDevice graphicsDevice, ContentManager contentManager)
    {
        foreach (var system in Systems)
        {
            system.InitializeGraphics(graphicsDevice, contentManager);
        }
    }
}
