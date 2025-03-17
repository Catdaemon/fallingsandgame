using FallingSand.Entity.Sprites;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record SpriteComponent
{
    public SpriteSheetAnimation Animation { get; set; }
    public readonly Rectangle DestinationSize;
    private readonly string spriteSheetName;
    public Color Tint = Color.White;

    public SpriteComponent(
        string spriteSheetName,
        string animationName,
        Rectangle destinationSize,
        Color? tint = null
    )
    {
        this.spriteSheetName = spriteSheetName;
        Animation = SpriteManager.GetAnimation(spriteSheetName, animationName);
        DestinationSize = destinationSize;

        if (tint.HasValue)
        {
            Tint = tint.Value;
        }
    }

    public void SetAnimation(string animationName)
    {
        Animation = SpriteManager.GetAnimation(spriteSheetName, animationName);
    }
}
