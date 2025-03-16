using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.Sprites;

class SpriteSheetAnimation
{
    public SpriteSheet SpriteSheet;
    public int Row;
    public int StartColumn;
    public int EndColumn;
    public int CurrentFrame;

    public SpriteSheetAnimation(SpriteSheet spriteSheet, int row, int startColumn, int endColumn)
    {
        SpriteSheet = spriteSheet;
        StartColumn = startColumn;
        EndColumn = endColumn;
        Row = row;
    }

    public SpriteSheetAnimation(SpriteSheet spriteSheet, int row, int column)
        : this(spriteSheet, row, column, column) { }

    public void Update(GameTime gameTime)
    {
        // Calculate the total frames in the animation
        var totalFrames = (EndColumn - StartColumn + 1) * SpriteSheet.Columns;

        // Calculate the frame time in milliseconds
        var frameTime = 1000 / SpriteSheet.FrameRate;

        // Calculate the current frame based on the elapsed time
        var elapsed = (float)gameTime.TotalGameTime.TotalMilliseconds;
        CurrentFrame = (int)(elapsed / frameTime) % totalFrames;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position)
    {
        var sourceRectangle = SpriteSheet.GetSourceRectangle(Row, StartColumn + CurrentFrame);
        spriteBatch.Draw(SpriteSheet.Texture, position, sourceRectangle, Color.White);
    }
}
