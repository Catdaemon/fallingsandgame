using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.System;

public interface ISystem
{
    public void InitializeGraphics(GraphicsDevice graphicsDevice, ContentManager contentManager) { }
    public void Update(GameTime gameTime, float deltaTime);
    public void Draw(GameTime gameTime, float deltaTime, RenderTarget2D screenTarget) { }
}
