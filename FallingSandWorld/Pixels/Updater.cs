using System;
using FallingSand;

namespace FallingSandWorld.Pixels;
static partial class PixelMaterialUpdater
{
    public static bool UpdatePixel(Random random, FallingSandWorldChunk chunk, LocalPosition localPosition, FallingSandPixel pixel)
    {
        if (pixel.IsFire)
        {
            return UpdateFire(random, chunk, localPosition, pixel);
        }
        else if (pixel.Data.Material == Material.Smoke)
        {
            return UpdateSmoke(random, chunk, localPosition, pixel);
        }
        else if (pixel.Data.Material == Material.Steam)
        {
            return UpdateSteam(random, chunk, localPosition, pixel);
        }
        else
        {
            return false;
        }   
    }
}