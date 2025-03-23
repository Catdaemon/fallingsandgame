using System;
using System.Runtime.CompilerServices;
using FallingSand;
using FallingSand.FallingSandRenderer;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Pixels;

static partial class PixelMaterialUpdater
{
    public static bool UpdateSteam(
        Random random,
        FallingSandWorldChunk chunk,
        LocalPosition position,
        FallingSandPixel pixel
    )
    {
        var worldPosition = chunk.LocalToWorldPosition(position);
        var abovePosition = new WorldPosition(worldPosition.X, worldPosition.Y + 1);
        var abovePixel = chunk.parentWorld.GetPixel(abovePosition);

        // Make steam rise by decreasing Y position (moving upward)
        if (random.Next(10) < 6) // 60% chance to rise
        {
            var upPosition = new WorldPosition(worldPosition.X, worldPosition.Y - 1);
            if (chunk.parentWorld.GetPixel(upPosition).Data.Material == Material.Empty)
            {
                chunk.parentWorld.SetPixel(upPosition, pixel.Data);

                chunk.parentWorld.EmptyPixel(worldPosition);
                return true;
            }
        }

        // Random horizontal drift
        if (random.Next(10) < 3) // 30% chance to drift
        {
            int direction = random.Next(2) == 0 ? -1 : 1;
            var sidePosition = new WorldPosition(worldPosition.X + direction, worldPosition.Y);
            if (chunk.parentWorld.GetPixel(sidePosition).Data.Material == Material.Empty)
            {
                chunk.parentWorld.SetPixel(sidePosition, pixel.Data);

                chunk.parentWorld.EmptyPixel(worldPosition);
                return true;
            }
        }

        // Steam lifetime and condensation behavior
        pixel.Lifetime++;

        if (abovePixel.Data.Material != Material.Empty && !abovePixel.IsGas)
        {
            if (random.Next(200) == 1) // Increased chance for more water formation
            {
                // Condense into water
                chunk.parentWorld.SetPixel(
                    worldPosition,
                    new FallingSandPixelData
                    {
                        Material = Material.Water,
                        Color = new Color(0, 0, 255), // Pure blue for water
                    }
                );
                return true;
            }
        }

        // Steam gradually disappears over time (decreased lifetime to create more water)
        if (pixel.Lifetime > 300 && random.Next(100) == 1)
        {
            chunk.EmptyPixel(position);
            return true;
        }

        return false;
    }
}
