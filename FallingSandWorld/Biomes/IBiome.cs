using System;
using System.Collections.Generic;
using fallingsand.nosync;

namespace FallingSandWorld.Biomes;

interface IBiome
{
    BiomeType Type { get; }

    // Height properties (in world units)
    int MinHeight { get; set; }
    int MaxHeight { get; set; }

    // Width properties (in world units)
    int MinWidth { get; set; }
    int MaxWidth { get; set; }

    // Generate a pixel at the given world position
    FallingSandPixelData GeneratePixel(WorldPosition position, Random random, float noiseValue);

    // Check if this position is within this biome based on the noise value and height
    bool IsInBiome(
        WorldPosition position,
        float noiseValue,
        Dictionary<BiomeType, float> biomeWeights
    );
}
