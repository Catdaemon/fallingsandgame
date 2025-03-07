using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FallingSand.Entity.System;

class RenderSystem : ISystem
{
    private readonly World World;

    public RenderSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime) { }

    public void Draw(GameTime gameTime) { }
}
