using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using FallingSandWorld;
using FPSCounter;
using GameWorld;
using Microsoft.VisualBasic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace fallingsand.nosync;

public class Game1 : Game
{
    private readonly FrameCounter _frameCounter = new FrameCounter();

    private SpriteBatch _spriteBatch;

    private readonly GameWorld.GameWorld gameWorld;
    private double lastFpsTime;

    private readonly int worldSizeX = 800;
    private readonly int worldSizeY = 600;

    private Material paintMaterial = Material.Sand;
    private FallingSandWorld.Color paintColor = new(255, 255, 0);

    public Game1()
    {
        var _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = worldSizeX;
        _graphics.PreferredBackBufferHeight = worldSizeY;
        _graphics.SynchronizeWithVerticalRetrace = true;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        Camera.SetPosition(new WorldPosition(0, 0));
        Camera.SetSize(new WorldPosition(worldSizeX, worldSizeY));
        Camera.SetZoom(1.0f);
        gameWorld = new();
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        gameWorld.Init("hello", _spriteBatch, GraphicsDevice);
    }

    private void UpdateCamera()
    {
        Camera.SetMousePosition(Mouse.GetState().Position.ToVector2());

        // Camera movement with arrow keys
        var cameraSpeed = 1;
        if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            Camera.SetPosition(Camera.GetPosition().X - cameraSpeed, Camera.GetPosition().Y);
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            Camera.SetPosition(Camera.GetPosition().X + cameraSpeed, Camera.GetPosition().Y);
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            Camera.SetPosition(Camera.GetPosition().X, Camera.GetPosition().Y - cameraSpeed);
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            Camera.SetPosition(Camera.GetPosition().X, Camera.GetPosition().Y + cameraSpeed);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (
            GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || Keyboard.GetState().IsKeyDown(Keys.Escape)
        )
            Exit();

        UpdateCamera();

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

        gameWorld.Draw();

        base.Draw(gameTime);
    }
}
