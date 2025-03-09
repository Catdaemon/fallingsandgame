using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using Arch.Core;
using FallingSand.Entity.Component;
using FallingSand.Entity.System;
using FallingSandWorld;
using FPSCounter;
using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand;

public class Game1 : Game
{
    private readonly FrameCounter _frameCounter = new FrameCounter();

    private SpriteBatch _spriteBatch;

    private readonly FallingSandRenderer.GameWorld gameWorld;
    private FallingSandWorld.FallingSandWorld sandWorld;
    private double lastFpsTime;

    private readonly int worldSizeX = 1920;
    private readonly int worldSizeY = 1080;

    private Material paintMaterial = Material.Sand;
    private FallingSandWorld.Color paintColor = new(255, 255, 0);

    private readonly Arch.Core.World ecsWorld;
    private readonly SystemManager systemManager;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld = new();

    public Game1()
    {
        var _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = worldSizeX;
        _graphics.PreferredBackBufferHeight = worldSizeY;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;

        gameWorld = new();

        ecsWorld = Arch.Core.World.Create();
        systemManager = new(ecsWorld);
    }

    protected override void Initialize()
    {
        Camera.SetPosition(new WorldPosition(64, 64));
        Camera.SetSize(new WorldPosition(worldSizeX, worldSizeY));
        Camera.SetZoom(2.0f);

        sandWorld = new FallingSandWorld.FallingSandWorld(new WorldPosition(1000, 1000));

        systemManager.RegisterSystems(physicsWorld, sandWorld);

        // Create a player entity
        ecsWorld.Create(
            new PositionComponent(),
            new BoundingBoxComponent(),
            new HealthComponent(100, 100),
            new InputReceiverComponent(),
            new InputStateComponent(),
            new CameraFollowComponent(),
            // new CirclePhysicsBodyComponent(8, 8, new WorldPosition(100, 100)),
            new RectanglePhysicsBodyComponent(16, 16, 10, new WorldPosition(100, 100)),
            new SandPixelReaderComponent()
        );

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        gameWorld.Init("hello", _spriteBatch, GraphicsDevice, physicsWorld, sandWorld);
        systemManager.InitializeGraphics(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (
            GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || Keyboard.GetState().IsKeyDown(Keys.Escape)
        )
            Exit();

        gameWorld.Update(gameTime);

        if (Mouse.GetState().RightButton == ButtonState.Pressed)
        {
            // Cycle through the materials
            switch (paintMaterial)
            {
                case Material.Sand:
                    paintMaterial = Material.Water;
                    paintColor = new FallingSandWorld.Color(0, 0, 255);
                    break;
                case Material.Water:
                    paintMaterial = Material.Wood;
                    paintColor = new FallingSandWorld.Color(139, 69, 19);
                    break;
                case Material.Wood:
                    paintMaterial = Material.Fire;
                    paintColor = new FallingSandWorld.Color(255, 0, 0);
                    break;
                case Material.Fire:
                    paintMaterial = Material.Sand;
                    paintColor = new FallingSandWorld.Color(255, 255, 0);
                    break;
            }
        }

        if (Mouse.GetState().LeftButton == ButtonState.Pressed)
        {
            // Set an 8x8 square of sand pixels at the mouse position
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    gameWorld.sandWorld.SetPixel(
                        new WorldPosition(
                            Camera.GetMouseWorldPosition().X + x,
                            Camera.GetMouseWorldPosition().Y + y
                        ),
                        new FallingSandPixelData() { Material = paintMaterial, Color = paintColor }
                    );
                }
            }
        }

        systemManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _frameCounter.Update(deltaTime);

        if (gameTime.TotalGameTime.TotalMilliseconds - lastFpsTime >= 1000)
        {
            var fps = string.Format("FPS: {0}", _frameCounter.AverageFramesPerSecond);

            Console.WriteLine($"{paintMaterial}: {fps}");

            lastFpsTime = gameTime.TotalGameTime.TotalMilliseconds;
        }

        gameWorld.Draw(gameTime);

        systemManager.Draw(gameTime);

        base.Draw(gameTime);
    }
}
