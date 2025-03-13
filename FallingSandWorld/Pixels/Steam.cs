using System;
using System.Runtime.CompilerServices;
using FallingSand;
using FallingSand.FallingSandRenderer;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Pixels;
static partial class PixelMaterialUpdater
{
    public static bool UpdateSteam(Random random, FallingSandWorldChunk chunk, LocalPosition position, FallingSandPixel pixel)
    {
        var worldPosition = chunk.LocalToWorldPosition(position);
        var abovePosition = new WorldPosition(worldPosition.X, worldPosition.Y + 1);
        var abovePixel = chunk.parentWorld.GetPixel(abovePosition);

        if (abovePixel.Data.Material != Material.Empty && !abovePixel.IsGas)
        {
            if (random.Next(1000) == 1)
            {
                // Condense into water
                chunk.parentWorld.SetPixel(
                    worldPosition,
                    new FallingSandPixelData
                    {
                        Material = Material.Water,
                        Color = new Color(0, 0, 255),
                    }
                );
            }
        }

        return false;
    }
}