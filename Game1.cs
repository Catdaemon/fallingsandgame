using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using Arch.Core;
using FallingSand.Entity.Component;
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

    private SpriteBatch _spriteBatch;
    private SpriteFont spriteFont;

    private readonly FallingSandRenderer.GameWorld gameWorld;
    private FallingSandWorld.FallingSandWorld sandWorld;
    private readonly int worldSizeX = 800;
    private readonly int worldSizeY = 600;

    private Material paintMaterial = Material.Sand;
    private Color paintColor = new(255, 255, 0);

    private readonly Arch.Core.World ecsWorld;
    private readonly SystemManager systemManager;
    private readonly nkast.Aether.Physics2D.Dynamics.World physicsWorld = new();
    private readonly DebugView physicsDebugView;

    public Game1()
    {
        // var expectedChunkWidth =
        //     (worldSizeX / Constants.CHUNK_WIDTH) + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        // var expectedChunkHeight =
        //     (worldSizeY / Constants.CHUNK_HEIGHT) + Constants.OFF_SCREEN_CHUNK_UPDATE_RADIUS;
        // var expectedChunkCount = expectedChunkWidth * expectedChunkHeight);
        // Constants.INITIAL_CHUNK_POOL_SIZE = expectedChunkCount;


        var _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = worldSizeX;
        _graphics.PreferredBackBufferHeight = worldSizeY;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // vsync
        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;

        gameWorld = new();

        ecsWorld = Arch.Core.World.Create();
        systemManager = new(ecsWorld);

        physicsDebugView = new DebugView(physicsWorld);
        physicsDebugView.AppendFlags(DebugViewFlags.Shape);
        physicsDebugView.AppendFlags(DebugViewFlags.Joint);
        physicsDebugView.AppendFlags(DebugViewFlags.ContactPoints);
        physicsDebugView.AppendFlags(DebugViewFlags.AABB);
        physicsDebugView.AppendFlags(DebugViewFlags.DebugPanel);
        physicsDebugView.AppendFlags(DebugViewFlags.PerformanceGraph);
    }

    protected override void Initialize()
    {
        Camera.SetPosition(new WorldPosition(64, 64));
        Camera.SetSize(new WorldPosition(worldSizeX, worldSizeY));
        Camera.SetZoom(1.0f);

        sandWorld = new FallingSandWorld.FallingSandWorld(new WorldPosition(1000, 1000));

        systemManager.RegisterSystems(physicsWorld, sandWorld);

        // Create a player entity
        ecsWorld.Create(
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
            new SandPixelReaderComponent()
        );

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        spriteFont = Content.Load<SpriteFont>("DiagnosticsFont");
        gameWorld.Init("hello", _spriteBatch, GraphicsDevice, physicsWorld, sandWorld);
        systemManager.InitializeGraphics(GraphicsDevice);
        physicsDebugView.LoadContent(GraphicsDevice, Content);
    }

    private long lastCollectionCount = 0;

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

        FrameCounter.Update(gameTime);

        long currentCount = GC.CollectionCount(2);
        if (currentCount > lastCollectionCount)
        {
            Console.WriteLine("Full GC occurred");
            lastCollectionCount = currentCount;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        // FrameCounter.DrawFps(_spriteBatch, spriteFont, new Vector2(10, 10), Color.White);

        gameWorld.Draw(gameTime);

        systemManager.Draw(gameTime);

        physicsDebugView.RenderDebugData(Camera.GetProjectionMatrix(), Camera.GetViewMatrix());

        base.Draw(gameTime);
    }
}
