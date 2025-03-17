using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.Sprites;

static class SpriteManager
{
    private static ContentManager contentManager;
    private static readonly Dictionary<string, SpriteSheet> spriteSheets = [];
    private static readonly Dictionary<string, SpriteAnimations> spriteAnimations = [];

    public static void Initialize(ContentManager contentManager)
    {
        SpriteManager.contentManager = contentManager;

        // Preload
        var playerSpriteSheet = LoadSpriteSheet("Sprites/Player", 9, 8, 8);
        var playerAnimations = new SpriteAnimations(playerSpriteSheet);
        playerAnimations.Add("Idle", 0, 0, 4, true);
        playerAnimations.Add("Run", 0, 4, 8, true);
        playerAnimations.Add("Jump", 6, 3, 5, false);
        spriteAnimations["Player"] = playerAnimations;

        var chestSpriteSheet = LoadSpriteSheet("Sprites/Chest", 2, 5, 2);
        var chestAnimations = new SpriteAnimations(chestSpriteSheet);
        chestAnimations.Add("Idle", 0, 0, 5, true);
        chestAnimations.Add("Open", 1, 0, 3, false);
        spriteAnimations["Chest"] = chestAnimations;
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

    public static SpriteSheetAnimation GetAnimation(string spriteSheetName, string animationName)
    {
        return spriteAnimations[spriteSheetName].Animations[animationName];
    }
}
