using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Collision;

namespace FallingSand.Entity.System;

class HUDSystem : ISystem
{
    private readonly World World;
    private SpriteBatch spriteBatch;

    public HUDSystem(World world, GraphicsDevice graphicsDevice)
    {
        World = world;
    }

    public void InitializeGraphics(GraphicsDevice graphicsDevice)
    {
        spriteBatch = new SpriteBatch(graphicsDevice);
    }

    public void Update(GameTime gameTime) { }

    public void Draw(GameTime gameTime) { }
}
