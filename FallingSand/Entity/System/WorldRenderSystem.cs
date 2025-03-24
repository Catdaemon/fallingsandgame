using FallingSand.FallingSandRenderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.System;

class WorldRenderSystem : ISystem
{
    private readonly GameWorld gameWorld;
    private readonly GraphicsDevice graphicsDevice;

    public WorldRenderSystem(GameWorld gameWorld, GraphicsDevice graphicsDevice)
    {
        this.gameWorld = gameWorld;
        this.graphicsDevice = graphicsDevice;
    }

    public void Update(GameTime gameTime, float deltaTime)
    {
        gameWorld.Update(gameTime);
    }

    public void Draw(GameTime gameTime, float deltaTime, RenderTarget2D screenTarget)
    {
        // Set the render target to the screen target
        graphicsDevice.SetRenderTarget(screenTarget);
        gameWorld.Draw(gameTime);
    }
}
