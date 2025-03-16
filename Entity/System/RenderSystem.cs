using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using nkast.Aether.Physics2D.Collision;

namespace FallingSand.Entity.System;

class RenderSystem : ISystem
{
    private readonly World World;
    private SpriteBatch spriteBatch;

    public RenderSystem(World world)
    {
        World = world;
    }

    public void InitializeGraphics(GraphicsDevice graphicsDevice)
    {
        spriteBatch = new SpriteBatch(graphicsDevice);
    }

    public void Update(GameTime gameTime)
    {
        // Update all sprites
        var withSpriteQuery = new QueryDescription().WithAny<SpriteComponent>();
        World.Query(
            in withSpriteQuery,
            (Arch.Core.Entity entity, ref SpriteComponent sprite) =>
            {
                sprite.Animation.Update(gameTime);
            }
        );
    }

    public void Draw(GameTime gameTime)
    {
        spriteBatch.Begin(transformMatrix: Camera.GetTransformMatrix());
        // Draw sprites
        var withSpriteQuery = new QueryDescription().WithAll<SpriteComponent, PositionComponent>();
        World.Query(
            in withSpriteQuery,
            (Arch.Core.Entity entity, ref SpriteComponent sprite, ref PositionComponent position) =>
            {
                sprite.Animation.Draw(spriteBatch, position.Position, sprite.DestinationSize);
            }
        );
        spriteBatch.End();
    }
}
