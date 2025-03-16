using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Entity.Sprites;

class SpriteSheet
{
    public Texture2D Texture { get; private set; }
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public int FrameRate { get; private set; }

    public SpriteSheet(Texture2D texture, int rows, int columns, int frameRate)
    {
        Texture = texture;
        Rows = rows;
        Columns = columns;
        FrameWidth = texture.Width / columns;
        FrameHeight = texture.Height / rows;
        FrameRate = frameRate;
    }

    public Rectangle GetSourceRectangle(int row, int column)
    {
        return new Rectangle(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
    }
}
