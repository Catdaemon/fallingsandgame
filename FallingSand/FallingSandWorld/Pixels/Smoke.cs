using System;
using FallingSand;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Pixels;
static partial class PixelMaterialUpdater
{
    public static bool UpdateSmoke(Random random, FallingSandWorldChunk chunk, LocalPosition position, FallingSandPixel pixel)
    {
        // Gradually fade our alpha value and become less dense
        var lifeLeft = Math.Max(pixel.Data.Color.A - 5, 0);
        Color newColor = new(pixel.Data.Color, lifeLeft);
        pixel.Density = (byte)(lifeLeft > 100 ? 1 : 0);

        if (lifeLeft == 0)
        {
            chunk.EmptyPixel(position);
        } else {
            chunk.SetPixel(position, new FallingSandPixelData
            {
                Material = Material.Smoke,
                Color = newColor,
            });
        }

        return true;
    }
}