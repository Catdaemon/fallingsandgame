
using System.IO;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace FallingSand.FallingSandRenderer;

class TextureSampler
{
    private int width;
    private int height;
    private Color[,] texture;

    public void Load(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open);
        using var bitmap = SKBitmap.Decode(fileStream);
        width = bitmap.Width;
        height = bitmap.Height;

        texture = new Color[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                texture[x, y] = new Color(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
            }
        }
    }

    public Color GetPixel(int x, int y)
    {
        // Wrap coordinates around the texture
        var textureX = x % width;
        var textureY = y % height;
        return texture[textureX, textureY];
    }
}