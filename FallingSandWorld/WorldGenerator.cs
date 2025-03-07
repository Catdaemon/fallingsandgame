using System;
using System.Collections.Generic;
using System.Linq;
using FallingSand;
using FallingSandWorld.Biomes;

namespace FallingSandWorld;

public class WorldGeneratorConfig
{
    // Scale values - smaller values = larger features
    public float TerrainScale { get; set; } = 0.001f; // Reduced from 0.01f for larger terrain
    public float BiomeScale { get; set; } = 0.0005f; // Reduced from 0.005f for larger biomes

    // Noise configuration
    public int OctaveCount { get; set; } = 4;
    public float Persistence { get; set; } = 0.5f; // Controls roughness between octaves

    // Biome configuration
    public float BiomeTransitionSize { get; set; } = 0.2f;

    // World height control
    public int BaseHeight { get; set; } = 500; // Base height for surface
    public int HeightVariation { get; set; } = 200; // How much height can vary
}

class FallingSandWorldGenerator
{
    private readonly Random random;
    private readonly NoiseGenerator noiseGenerator;
    private readonly List<IBiome> biomes;
    private readonly WorldGeneratorConfig config;

    public FallingSandWorldGenerator(string inputSeed, WorldGeneratorConfig config = null)
    {
        // Create deterministic seed from string
        int seed = inputSeed.GetHashCode();
        random = new Random(seed);
        noiseGenerator = new NoiseGenerator(random);
        this.config = config ?? new WorldGeneratorConfig();

        // Initialize biomes with scaled heights
        biomes = new List<IBiome>
        {
            new SurfaceBiome(
                this.config.BaseHeight - this.config.HeightVariation / 2,
                this.config.BaseHeight + this.config.HeightVariation / 2
            ),
            new MountainBiome(
                this.config.BaseHeight - this.config.HeightVariation,
                this.config.BaseHeight
            ),
            new CaveBiome(
                this.config.BaseHeight + this.config.HeightVariation / 2,
                this.config.BaseHeight + this.config.HeightVariation * 2
            ),
            // Add more biomes here
        };
    }

    public void AddBiome(IBiome biome)
    {
        biomes.Add(biome);
    }

    public void ConfigureBiome(
        BiomeType biomeType,
        int minHeight,
        int maxHeight,
        int minWidth = 50,
        int maxWidth = 500
    )
    {
        IBiome biome = biomes.FirstOrDefault(b => b.Type == biomeType);
        if (biome != null)
        {
            biome.MinHeight = minHeight;
            biome.MaxHeight = maxHeight;
            biome.MinWidth = minWidth;
            biome.MaxWidth = maxWidth;
        }
    }

    public FallingSandPixelData[] GenerateBatch(WorldPosition start, WorldPosition end)
    {
        int width = end.X - start.X + 1;
        int height = end.Y - start.Y + 1;
        FallingSandPixelData[] batch = new FallingSandPixelData[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                WorldPosition currentPos = new WorldPosition(start.X + x, start.Y + y);
                batch[y * width + x] = GeneratePixelAt(currentPos);
            }
        }

        return batch;
    }

    private FallingSandPixelData GeneratePixelAt(WorldPosition position)
    {
        // Calculate base terrain noise - consistent across chunk boundaries
        float terrainNoise = noiseGenerator.OctaveNoise(
            position.X * config.TerrainScale,
            position.Y * config.TerrainScale,
            config.OctaveCount,
            config.Persistence
        );

        // Calculate biome weights for this position - also consistent across chunks
        Dictionary<BiomeType, float> biomeWeights = CalculateBiomeWeights(position);

        // Find all applicable biomes for this position
        var applicableBiomes = biomes
            .Where(b => b.IsInBiome(position, terrainNoise, biomeWeights))
            .ToList();

        if (applicableBiomes.Count == 0)
        {
            // Default to empty if no biome applies
            return new FallingSandPixelData
            {
                Material = Material.Empty,
                Color = new Color(0, 0, 0),
            };
        }

        // If we have multiple applicable biomes, blend them based on weights
        if (applicableBiomes.Count > 1)
        {
            return BlendBiomes(position, terrainNoise, applicableBiomes, biomeWeights);
        }

        // Otherwise use the single applicable biome
        return applicableBiomes[0].GeneratePixel(position, random, terrainNoise);
    }

    private Dictionary<BiomeType, float> CalculateBiomeWeights(WorldPosition position)
    {
        Dictionary<BiomeType, float> weights = new Dictionary<BiomeType, float>();

        // Calculate base noise for biome distribution
        // Use very low frequency noise for biome distribution to get large continuous regions
        float biomeNoise = noiseGenerator.PerlinNoise(
            position.X * config.BiomeScale,
            position.Y * config.BiomeScale * 0.5f // Stretch biomes horizontally
        );

        // Secondary noise to add variety
        float secondaryNoise =
            noiseGenerator.PerlinNoise(
                position.X * config.BiomeScale * 2.7f,
                position.Y * config.BiomeScale * 1.3f
            ) * 0.3f; // Lower influence

        // Combine primary and secondary noise
        biomeNoise = Math.Clamp(biomeNoise + secondaryNoise, 0f, 1f);

        // Distribute biomes based on noise
        foreach (var biome in biomes)
        {
            // Each biome type gets assigned to a range of the noise value
            switch (biome.Type)
            {
                case BiomeType.Surface:
                    weights[biome.Type] = 1.0f - Math.Abs(biomeNoise - 0.5f) * 2; // Strongest in middle range
                    break;
                case BiomeType.Mountain:
                    weights[biome.Type] = biomeNoise > 0.7f ? (biomeNoise - 0.7f) / 0.3f : 0; // Only in high values
                    break;
                case BiomeType.Cave:
                    // Caves appear underground with perlin noise distribution
                    if (position.Y > config.BaseHeight + 50)
                    {
                        float caveNoise = noiseGenerator.PerlinNoise(
                            position.X * config.TerrainScale * 2,
                            position.Y * config.TerrainScale * 2
                        );
                        weights[biome.Type] = caveNoise > 0.6f ? (caveNoise - 0.6f) / 0.4f : 0;
                    }
                    else
                    {
                        weights[biome.Type] = 0;
                    }
                    break;
                default:
                    weights[biome.Type] = 0;
                    break;
            }
        }

        return weights;
    }

    private FallingSandPixelData BlendBiomes(
        WorldPosition position,
        float terrainNoise,
        List<IBiome> applicableBiomes,
        Dictionary<BiomeType, float> biomeWeights
    )
    {
        // Find the highest weighted biome
        IBiome primaryBiome = applicableBiomes.OrderByDescending(b => biomeWeights[b.Type]).First();

        // Use the primary biome's generation
        return primaryBiome.GeneratePixel(position, random, terrainNoise);

        // Note: For more complex blending, we could blend materials/colors
        // based on their respective weights, but that's more complex
    }
}
