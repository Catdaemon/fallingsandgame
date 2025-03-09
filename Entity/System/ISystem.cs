using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.System;

public interface ISystem
{
    public void InitializeGraphics(GraphicsDevice graphicsDevice) { }
    public void Update(GameTime gameTime);
    public void Draw(GameTime gameTime) { }
}
