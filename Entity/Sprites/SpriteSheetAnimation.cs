using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.Sprites;

class SpriteSheetAnimation
{
    public SpriteSheet SpriteSheet;
    public int StartRow;
    public int StartColumn;
    public int Duration;
    public int CurrentFrame;
    public int LastUpdateTime = 0;
    public bool Loop;

    public SpriteSheetAnimation(
        SpriteSheet spriteSheet,
        int startRow,
        int startColumn,
        int duration,
        bool loop
    )
    {
        SpriteSheet = spriteSheet;
        StartRow = startRow;
        StartColumn = startColumn;
        Duration = duration;
        CurrentFrame = 0;
        Loop = loop;
    }

    public SpriteSheetAnimation(SpriteSheet spriteSheet, int row, int column)
        : this(spriteSheet, row, column, 0, loop: false) { }

    public void Update(GameTime gameTime)
    {
        if (Duration == 0)
        {
            return;
        }

        if (
            gameTime.TotalGameTime.TotalMilliseconds - LastUpdateTime
            < 1000 / SpriteSheet.FrameRate
        )
        {
            return;
        }

        if (Loop)
        {
            CurrentFrame = (CurrentFrame + 1) % Duration;
        }
        else
        {
            CurrentFrame = Math.Min(CurrentFrame + 1, Duration - 1);
        }

        LastUpdateTime = (int)gameTime.TotalGameTime.TotalMilliseconds;
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position, Rectangle destinationSize)
    {
        // Calculate the current frame position with wrapping
        int totalFrameOffset = StartColumn + CurrentFrame;
        int currentColumn = totalFrameOffset % SpriteSheet.Columns;
        int rowOffset = totalFrameOffset / SpriteSheet.Columns;
        int currentRow = StartRow + rowOffset;

        // Create the source rectangle from the current position
        Rectangle sourceRectangle = new Rectangle(
            currentColumn * SpriteSheet.FrameWidth,
            currentRow * SpriteSheet.FrameHeight,
            SpriteSheet.FrameWidth,
            SpriteSheet.FrameHeight
        );

        // Draw centered on the position, scaled to fit the destination size
        spriteBatch.Draw(
            texture: SpriteSheet.Texture,
            sourceRectangle: sourceRectangle,
            destinationRectangle: new Rectangle(
                (int)position.X - destinationSize.Width / 2,
                (int)position.Y - destinationSize.Height / 2,
                destinationSize.Width,
                destinationSize.Height
            ),
            color: Color.White
        );
    }

    public void Reset()
    {
        CurrentFrame = 0;
    }
}
