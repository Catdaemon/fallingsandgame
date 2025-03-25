using System;
using System.Collections.Generic;
using System.Diagnostics;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using FallingSand.Entity.Sprites;
using FallingSand.Entity.System;
using FallingSand.FallingSandRenderer;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Diagnostics;

namespace FallingSand;

public class Game1 : Game
{
    private readonly MgFrameRate FrameCounter = new();

    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch _spriteBatch;
    private SpriteFont spriteFont;

    private readonly GameWorld gameWorld;
    private FallingSandWorld.FallingSandWorld sandWorld;
    private readonly int worldSizeX = 1024;
    private readonly int worldSizeY = 768;

    private Material paintMaterial = Material.Sand;
    private Color paintColor = new(255, 255, 0);

    private readonly Arch.Core.World ecsWorld;
    private readonly SystemManager systemManager;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld = new();
    private readonly DebugView physicsDebugView;
    private Texture2D backgroundTempTexture;

    public float gameSpeed = 1.0f;

    public Game1()
    {
        graphics = new(this)
        {
            PreferredBackBufferHeight = worldSizeY,
            PreferredBackBufferWidth = worldSizeX,
            SynchronizeWithVerticalRetrace = false,
        };
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

        systemManager.RegisterSystems(physicsWorld, sandWorld, gameWorld, GraphicsDevice, Content);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        FrameCounter.LoadSetUp(this, graphics, _spriteBatch, false, true, false, false, 60, true);

        backgroundTempTexture = Content.Load<Texture2D>("bg_temp");

        spriteFont = Content.Load<SpriteFont>("DiagnosticsFont");
        physicsDebugView.LoadContent(GraphicsDevice, Content);

        SpriteManager.Initialize(Content);

        var seed = "test seed";
        gameWorld.Init(seed, _spriteBatch, GraphicsDevice, physicsWorld, sandWorld, this);

        base.LoadContent();

        var light = ecsWorld.Create(
            new PositionComponent() { Position = new Vector2(100, 100) },
            new LightComponent()
            {
                Size = 500f,
                Intensity = 1.0f,
                Color = Color.White,
                CastShadows = true,
            }
        );

        var player = ecsWorld.CreatePlayer(new Vector2(400, 100));
        ecsWorld.CreateChest(new Vector2(200, 100));
        var weapon = ecsWorld.CreateWeapon(
            new Vector2(Convert.PixelsToMeters(300), Convert.PixelsToMeters(-100)),
            WeaponType.Pistol
        );
        weapon.Get<EquippableComponent>().IsActive = true;
        weapon.Get<EquippableComponent>().Parent = player;
    }

    protected override void Update(GameTime gameTime)
    {
        var deltaTime =
            Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f) * gameSpeed;

        if (
            GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || Keyboard.GetState().IsKeyDown(Keys.Escape)
        )
            Exit();

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
        // var mouseState = Mouse.GetState();
        // if (mouseState.LeftButton == ButtonState.Pressed)
        // {
        //     // Paint material at cursor location
        //     var mousePos = new Vector2(mouseState.X, mouseState.Y);
        //     var worldPos = Camera.ScreenToWorldPosition(mousePos);

        //     // Draw in a circle around the cursor

        //     for (int y = -3; y <= 3; y++)
        //     {
        //         for (int x = -3; x <= 3; x++)
        //         {
        //             if (x * x + y * y <= 9) // Make it a circle, not a square
        //             {
        //                 var pixelData = new FallingSandWorld.FallingSandPixelData
        //                 {
        //                     Material = paintMaterial,
        //                     Color = paintColor,
        //                 };
        //                 sandWorld.SetPixel(
        //                     new WorldPosition(worldPos.X + x, worldPos.Y + y),
        //                     pixelData
        //                 );
        //             }
        //         }
        //     }
        // }

        systemManager.Update(gameTime, deltaTime);

        FrameCounter.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        var deltaTime =
            Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f) * gameSpeed;

        // Draw the game world to its render targets
        gameWorld.DrawRenderTargets();

        // Clear the screen between drawing the render targets and the rest
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Draw our temp bg image
        _spriteBatch.Begin();
        _spriteBatch.Draw(
            backgroundTempTexture,
            new Rectangle(0, 0, worldSizeX, worldSizeY),
            Color.White
        );
        _spriteBatch.End();

        systemManager.Draw(gameTime, deltaTime);

        // Draw debug view
        // try
        // {
        //     physicsDebugView.RenderDebugData(Camera.GetProjectionMatrix(), Camera.GetViewMatrix());
        // }
        // catch (IndexOutOfRangeException ex)
        // {
        //     Console.WriteLine($"Physics debug rendering error: {ex.Message}");
        // }

        // Draw FPS counter on top
        _spriteBatch.Begin();
        FrameCounter.DrawFps(_spriteBatch, spriteFont, new Vector2(5, 5), Color.Yellow);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
