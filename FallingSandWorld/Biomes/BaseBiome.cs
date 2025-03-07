using System;
using System.Collections.Generic;
using fallingsand.nosync;

namespace FallingSandWorld.Biomes;

abstract class BaseBiome : IBiome
{
    public BiomeType Type { get; }
    public int MinHeight { get; set; }
    public int MaxHeight { get; set; }
    public int MinWidth { get; set; }
    public int MaxWidth { get; set; }

    protected BaseBiome(
        BiomeType type,
        int minHeight,
        int maxHeight,
        int minWidth = 50,
        int maxWidth = 500
    )
    {
        Type = type;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
        MinWidth = minWidth;
        MaxWidth = maxWidth;
    }

    public abstract FallingSandPixelData GeneratePixel(
        WorldPosition position,
        Random random,
        float noiseValue
    );

    public virtual bool IsInBiome(
        WorldPosition position,
        float noiseValue,
        Dictionary<BiomeType, float> biomeWeights
    )
    {
        if (!biomeWeights.TryGetValue(Type, out float weight))
        {
            return false;
        }

        return weight > 0.5f; // Default implementation, can be overridden by specific biomes
    }
}
