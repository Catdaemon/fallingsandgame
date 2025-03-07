using System;
using System.Collections.Generic;
using fallingsand.nosync;

namespace FallingSandWorld.Biomes;

class MountainBiome : BaseBiome
{
    public MountainBiome(int minHeight = 50, int maxHeight = 200)
        : base(BiomeType.Mountain, minHeight, maxHeight) { }

    public override FallingSandPixelData GeneratePixel(
        WorldPosition position,
        Random random,
        float noiseValue
    )
    {
        // Calculate mountain height using noise (steeper than regular surface)
        int mountainHeight = (int)(MinHeight + Math.Pow(noiseValue, 0.5) * (MaxHeight - MinHeight));

        // Above mountain: air
        if (position.Y < mountainHeight)
        {
            return new FallingSandPixelData
            {
                Material = Material.Empty,
                Color = new Color(0, 0, 0),
            };
        }

        // Mountain peak: snow
        if (
            position.Y < mountainHeight + 5
            && mountainHeight > MinHeight + (MaxHeight - MinHeight) * 0.7
        )
        {
            return new FallingSandPixelData
            {
                Material = Material.Snow,
                Color = new Color(255, 255, 255),
            };
        }

        // Mountain body: stone
        return new FallingSandPixelData
        {
            Material = Material.Stone,
            Color = new Color(100, 100, 100),
        };
    }

    public override bool IsInBiome(
        WorldPosition position,
        float noiseValue,
        Dictionary<BiomeType, float> biomeWeights
    )
    {
        if (!biomeWeights.TryGetValue(Type, out float weight))
        {
            return false;
        }

        return weight > 0.7f && position.Y >= 0; // Mountains need higher weight to appear
    }
}
