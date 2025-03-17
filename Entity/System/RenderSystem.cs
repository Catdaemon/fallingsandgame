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
    private Texture2D pixelTexture;

    public RenderSystem(World world, GraphicsDevice graphicsDevice)
    {
        World = world;
        pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        pixelTexture.SetData([Color.White]);
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
                var flip = position.FacingDirection.X < 0;
                sprite.Animation.Draw(spriteBatch, position.Position, sprite.DestinationSize, flip);
            }
        );
        // Draw particles
        var withParticleQuery = new QueryDescription().WithAll<
            ParticleComponent,
            PositionComponent
        >();
        World.Query(
            in withParticleQuery,
            (
                Arch.Core.Entity entity,
                ref ParticleComponent particle,
                ref PositionComponent position
            ) =>
            {
                spriteBatch.Draw(pixelTexture, position.Position, null, particle.Color);
            }
        );
        spriteBatch.End();
    }
}
