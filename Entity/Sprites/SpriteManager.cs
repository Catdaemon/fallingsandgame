using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.Sprites;

static class SpriteManager
{
    private static ContentManager contentManager;
    private static Dictionary<string, SpriteSheet> spriteSheets = [];

    public static void Initialize(ContentManager contentManager)
    {
        SpriteManager.contentManager = contentManager;

        // Preload
        var playerSpriteSheet = LoadSpriteSheet("Sprites/Player", 6, 21, 30);
        var playerAnimations = new SpriteAnimations(playerSpriteSheet);
        playerAnimations.Add("IdleRight", 0, 0, 1);
    }

    public static SpriteSheet LoadSpriteSheet(string name, int rows, int columns, int frameRate)
    {
        if (!spriteSheets.ContainsKey(name))
        {
            spriteSheets[name] = new SpriteSheet(
                contentManager.Load<Texture2D>(name),
                rows,
                columns,
                frameRate
            );
        }

        return spriteSheets[name];
    }

    public static SpriteSheet GetSpriteSheet(string name)
    {
        return spriteSheets[name];
    }
}
