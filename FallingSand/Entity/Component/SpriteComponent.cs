using FallingSand.Entity.Sprites;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record SpriteComponent
{
    public SpriteSheetAnimation Animation { get; set; }
    public readonly Rectangle DestinationSize;
    private readonly string spriteSheetName;

    public SpriteComponent(string spriteSheetName, string animationName, Rectangle destinationSize)
    {
        this.spriteSheetName = spriteSheetName;
        Animation = SpriteManager.GetAnimation(spriteSheetName, animationName);
        DestinationSize = destinationSize;
    }

    public void SetAnimation(string animationName)
    {
        Animation = SpriteManager.GetAnimation(spriteSheetName, animationName);
    }
}
