using System;
using System.Collections.Generic;
using FallingSand;
using Microsoft.Xna.Framework;

namespace FallingSandWorld.Biomes;

class CaveBiome : BaseBiome
{
    public CaveBiome(int minHeight = 200, int maxHeight = 500)
        : base(BiomeType.Cave, minHeight, maxHeight) { }

    public override FallingSandPixelData GeneratePixel(
        WorldPosition position,
        Random random,
        float noiseValue
    )
    {
        // Caves are empty spaces surrounded by stone
        // Use a different noise function for caves to create more "bubbly" formations
        float caveNoise = (float)Math.Sin(position.X * 0.05) * (float)Math.Cos(position.Y * 0.05);
        caveNoise += noiseValue * 0.5f;

        if (caveNoise > 0.3f)
        {
            return new FallingSandPixelData { Material = Material.Empty, Color = Color.Black };
        }

        // Occasional water pools in caves
        if (caveNoise > 0.2f && caveNoise <= 0.3f && random.Next(10) == 0)
        {
            return new FallingSandPixelData
            {
                Material = Material.Water,
                Color = new Color(0, 0, 255),
            };
        }

        // Rest is stone
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
        if (!biomeWeights.TryGetValue(Type, out float weight))
        {
            return false;
        }

        return weight > 0.4f && position.Y >= MinHeight && position.Y <= MaxHeight;
    }
}
