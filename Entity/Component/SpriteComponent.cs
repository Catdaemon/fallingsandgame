using System.Collections.Generic;
using FallingSand.Entity.Sprites;
using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record SpriteComponent
{
    public SpriteSheetAnimation Animation { get; set; }

    private static Dictionary<string, SpriteSheet> spriteSheets = new();

    public SpriteComponent(string spriteSheetName, int rows, int columns, int frameRate)
    {
        if (!spriteSheets.ContainsKey(spriteSheetName))
        {
            spriteSheets[spriteSheetName] = new SpriteSheet(
                spriteSheetName,
                rows,
                columns,
                frameRate
            );
        }

        Animation = new SpriteSheetAnimation(spriteSheets[spriteSheetName]);
    }
}
