using System;
using System.Collections.Generic;
using System.Diagnostics;
using Arch.Core;
using FallingSand.Entity.Component;
using FallingSand.Entity.Sprites;
using FallingSand.Entity.System;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Diagnostics;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand;

public class Game1 : Game
{
    private readonly MgFrameRate FrameCounter = new();

    private GraphicsDeviceManager graphics;
    private SpriteBatch _spriteBatch;
    private SpriteFont spriteFont;

    private readonly FallingSandRenderer.GameWorld gameWorld;
    private FallingSandWorld.FallingSandWorld sandWorld;
    private readonly int worldSizeX = 1024;
    private readonly int worldSizeY = 768;

    private Material paintMaterial = Material.Sand;
    private Color paintColor = new(255, 255, 0);

    private readonly Arch.Core.World ecsWorld;
    private readonly SystemManager systemManager;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld = new();
    private readonly DebugView physicsDebugView;

    public Game1()
    {
        graphics = new(this);
        graphics.PreferredBackBufferHeight = worldSizeY;
        graphics.PreferredBackBufferWidth = worldSizeX;
        graphics.SynchronizeWithVerticalRetrace = false;
        // no fixed time
        IsFixedTimeStep = false;

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        gameWorld = new FallingSandRenderer.GameWorld();
        ecsWorld = Arch.Core.World.Create();
        systemManager = new SystemManager(ecsWorld);
        physicsDebugView = new DebugView(physicsWorld) { Enabled = true };
    }

    protected override void Initialize()
    {
        Camera.InitializeCamera(worldSizeX, worldSizeY, GraphicsDevice.Viewport);

        sandWorld = new FallingSandWorld.FallingSandWorld(
            new WorldPosition(worldSizeX, worldSizeY)
        );

        systemManager.RegisterSystems(physicsWorld, sandWorld);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        FrameCounter.LoadSetUp(this, graphics, _spriteBatch, false, true, false, false, 60, true);

        spriteFont = Content.Load<SpriteFont>("DiagnosticsFont");
        physicsDebugView.LoadContent(GraphicsDevice, Content);

        SpriteManager.Initialize(Content);

        systemManager.InitializeGraphics(GraphicsDevice);

        var seed = "test seed";
        gameWorld.Init(seed, _spriteBatch, GraphicsDevice, physicsWorld, sandWorld, this);

        base.LoadContent();

        // Create the player
        var player = ecsWorld.Create(
            new PositionComponent(),
            new BoundingBoxComponent(),
            new HealthComponent() { Current = 100, Max = 100 },
            new InputReceiverComponent(),
            new InputStateComponent(),
            new CameraFollowComponent(),
            // new CirclePhysicsBodyComponent(8, 8, new WorldPosition(100, 100)),
            new CapsulePhysicsBodyComponent()
            {
                Height = 16,
                Width = 8,
                InitialPosition = new WorldPosition(100, 100),
                CreateSensors = true,
            },
            new SandPixelReaderComponent(),
            new SpriteComponent("Player", "Idle", new Rectangle(0, 0, 16, 16))
        );
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
                    paintColor = new Color(0, 0, 255);
                    break;
                case Material.Water:
                    paintMaterial = Material.Wood;
                    paintColor = new Color(139, 69, 19);
                    break;
                case Material.Wood:
                    paintMaterial = Material.Fire;
                    paintColor = new Color(255, 0, 0);
                    break;
                case Material.Fire:
                    paintMaterial = Material.Sand;
                    paintColor = new Color(255, 255, 0);
                    break;
            }
        }

        // Check if the mouse is used
        var mouseState = Mouse.GetState();
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            // Paint material at cursor location
            var mousePos = new Vector2(mouseState.X, mouseState.Y);
            var worldPos = Camera.ScreenToWorldPosition(mousePos);

            // Draw in a circle around the cursor

            for (int y = -3; y <= 3; y++)
            {
                for (int x = -3; x <= 3; x++)
                {
                    if (x * x + y * y <= 9) // Make it a circle, not a square
                    {
                        var pixelData = new FallingSandWorld.FallingSandPixelData
                        {
                            Material = paintMaterial,
                            Color = paintColor,
                        };
                        sandWorld.SetPixel(
                            new WorldPosition(worldPos.X + x, worldPos.Y + y),
                            pixelData
                        );
                    }
                }
            }
        }

        Camera.Update(gameTime);
        systemManager.Update(gameTime);

        FrameCounter.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Draw the game world to its render targets
        gameWorld.DrawRenderTargets();

        // Clear the screen between drawing the render targets and the rest
        // Otherwise the background will be overwritten
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Draw the render targets to the screen
        gameWorld.Draw(gameTime);

        systemManager.Draw(gameTime);

        physicsDebugView.RenderDebugData(Camera.GetProjectionMatrix(), Camera.GetViewMatrix());

        // _spriteBatch.Begin();
        // FrameCounter.DrawFps(_spriteBatch, spriteFont, new Vector2(5, 5), Color.Yellow);
        // _spriteBatch.End();

        base.Draw(gameTime);
    }
}
