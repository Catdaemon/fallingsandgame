using System;
using System.Runtime.CompilerServices;
using FallingSand;
using FallingSand.FallingSandRenderer;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Pixels;
static partial class PixelMaterialUpdater
{
    public static bool UpdateFire(Random random, FallingSandWorldChunk chunk, LocalPosition position, FallingSandPixel pixel)
    {
        var worldPosition = chunk.LocalToWorldPosition(position);
        var isEmber = pixel.Data.Material == Material.Ember;
        pixel.Lifetime ++;

        void EmitSmoke()
        {
            if (isEmber) {
                return;
            }

            var abovePosition = new WorldPosition(worldPosition.X, worldPosition.Y - 1);
            if (chunk.parentWorld.GetPixel(abovePosition).Data.Material == Material.Empty)
            {
                chunk.parentWorld.SetPixel(
                    abovePosition,
                    new FallingSandPixelData
                    {
                        Material = Material.Smoke,
                        Color = new Color(64, 64, 64),
                    }
                );
            }
        }

        void EmitEmber()
        {
            if (isEmber) {
                return;
            }

            var belowPosition = new WorldPosition(worldPosition.X, worldPosition.Y + 1);
            if (chunk.parentWorld.GetPixel(belowPosition).Data.Material == Material.Empty)
            {
                chunk.parentWorld.SetPixel(
                    belowPosition,
                    new FallingSandPixelData
                    {
                        Material = Material.Ember,
                        Color = new Color(255, 0, 0),
                    }
                );
            }
        }

        // Randomise our colour to make the fire look more natural
        // chunk.SetPixel(
        //     position,
        //     new FallingSandPixelData
        //     {
        //         Material = Material.Fire,
        //         Color = new Color(255, random.Next(100, 200), random.Next(0, 100)),
        //     }
        // );

        // After lifetime, randomly decide whether to extinguish
        if (pixel.Lifetime > 100 && random.Next(100) == 1)
        {
            chunk.EmptyPixel(position);
        }


        // Randomly emit smoke above if there is space
        if (random.Next(100) < 10)
        {
            EmitSmoke();
        }

        // Randomly emit an ember if there is space below
        if (random.Next(2000) < 1)
        {
            EmitEmber();
        }

        // Remove if we are touching water
        foreach (var (dx, dy) in FallingSandPixel.AdjacentOffsets)
        {
            var otherPixel = chunk.parentWorld.GetPixel(
                new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy)
            );
            if (otherPixel.Data.Material == Material.Water)
            {
                // Remove us
                chunk.EmptyPixel(position);
                // Replace the water with steam
                chunk.parentWorld.SetPixel(
                    new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy),
                    new FallingSandPixelData
                    {
                        Material = Material.Steam,
                        Color = new Color(0, 255, 0),
                    }
                );
                break;
            }
            else if (random.Next(500) < otherPixel.Flammability)
            {
                // Try converting adjacent pixels to fire
                chunk.parentWorld.SetPixel(
                    new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy),
                    new FallingSandPixelData
                    {
                        Material = Material.Fire,
                        Color = new Color(255, 0, 0),
                    }
                );
                EmitSmoke();
            }
        }

        return true;
    }
}