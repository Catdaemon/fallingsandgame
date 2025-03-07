using Microsoft.Xna.Framework;

namespace FallingSand.Entity.System;

public interface ISystem
{
    public void Update(GameTime gameTime);
    public void Draw(GameTime gameTime) { }
}
