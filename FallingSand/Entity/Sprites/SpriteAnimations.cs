using System.Collections.Generic;

namespace FallingSand.Entity.Sprites;

class SpriteAnimations
{
    public SpriteSheet SpriteSheet;
    public Dictionary<string, SpriteSheetAnimation> Animations = new();

    public SpriteAnimations(SpriteSheet spriteSheet)
    {
        SpriteSheet = spriteSheet;
    }

    public void Add(string name, int startRow, int startColumn, int frameCount, bool loop)
    {
        Animations[name] = new SpriteSheetAnimation(
            SpriteSheet,
            startRow,
            startColumn,
            frameCount,
            loop
        );
    }
}
