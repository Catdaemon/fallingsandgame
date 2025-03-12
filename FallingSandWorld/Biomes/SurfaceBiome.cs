using System;
using System.Collections.Generic;
using FallingSand;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Biomes;

class SurfaceBiome : BaseBiome
{
    public SurfaceBiome(int minHeight = 100, int maxHeight = 300)
        : base(BiomeType.Surface, minHeight, maxHeight) { }

    public override FallingSandPixelData GeneratePixel(
        WorldPosition position,
        Random random,
        float noiseValue
    )
    {
        // Calculate surface level using noise
        int surfaceLevel = (int)(MinHeight + noiseValue * (MaxHeight - MinHeight));

        // Above surface: air
        if (position.Y < surfaceLevel)
        {
            return new FallingSandPixelData { Material = Material.Empty, Color = Color.Black };
        }

        // Surface layer: grass
        if (position.Y < surfaceLevel + 3)
        {
            return new FallingSandPixelData
            {
                Material = Material.Grass,
                Color = new Color(34, 139, 34),
            };
        }

        // Just below surface: dirt (represented by sand for now)
        if (position.Y < surfaceLevel + 15)
        {
            return new FallingSandPixelData
            {
                Material = Material.Sand,
                Color = new Color(139, 69, 19),
            };
        }

        // Deep underground: stone
        return new FallingSandPixelData
        {
            Material = Material.Stone,
            Color = new Color(128, 128, 128),
        };
    }

    public override bool IsInBiome(
        WorldPosition position,
        float noiseValue,
        Dictionary<BiomeType, float> biomeWeights
    )
    {
        // Calculate surface level using noise
        int surfaceLevel = (int)(MinHeight + noiseValue * (MaxHeight - MinHeight));

        // Surface biome extends from top of the world to some depth below the surface
        int depthBelowSurface = 50;
        return position.Y >= 0 && position.Y <= surfaceLevel + depthBelowSurface;
    }
}
