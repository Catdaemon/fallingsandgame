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

    public void Add(string name, int row, int startColumn, int endColumn)
    {
        Animations[name] = new SpriteSheetAnimation(SpriteSheet, row, startColumn, endColumn);
    }
}
