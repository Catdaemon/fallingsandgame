using System;
using System.Collections.Generic;
using System.IO;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace FallingSand.WorldGenerator;

static class AssetLoader
{
    private const int IMAGE_SIZE = WorldGenerationManager.IMAGE_SIZE;
    private static readonly Dictionary<Color, Material> ColorMaterialMap = new()
    {
        { Color.Black, Material.Empty },
        { Color.White, Material.Stone },
    };

    private static TileDefinition ProcessRotatedAsset(SKBitmap bitmap, int rotation, bool flipped)
    {
        Material[] chunkData = new Material[IMAGE_SIZE * IMAGE_SIZE];

        // Rotation can be 0, 1, 2, 3, and flipped can be true/false
        for (int y = 0; y < IMAGE_SIZE; y++)
        {
            for (int x = 0; x < IMAGE_SIZE; x++)
            {
                // First apply rotation
                var rotatedPixelLocation = rotation switch
                {
                    0 => new Vector2(x, y),
                    1 => new Vector2(y, IMAGE_SIZE - x - 1),
                    2 => new Vector2(IMAGE_SIZE - x - 1, IMAGE_SIZE - y - 1),
                    3 => new Vector2(IMAGE_SIZE - y - 1, x),
                    _ => throw new NotImplementedException("Invalid rotation"),
                };

                // Then apply horizontal flip if needed
                if (flipped)
                {
                    rotatedPixelLocation = new Vector2(
                        IMAGE_SIZE - rotatedPixelLocation.X - 1,
                        rotatedPixelLocation.Y
                    );
                }

                SKColor pixel = bitmap.GetPixel(
                    (int)rotatedPixelLocation.X,
                    (int)rotatedPixelLocation.Y
                );

                var color = new Color(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);

                // Look up the material for the color
                if (ColorMaterialMap.TryGetValue(color, out var material))
                {
                    chunkData[y * IMAGE_SIZE + x] = material;
                }
                else
                {
                    throw new NotImplementedException($"Color {color} not implemented");
                }
            }
        }

        var definition = new TileDefinition { PixelData = chunkData };
        EdgeHasher.CalculateEdgeHashes(definition);
        return definition;
    }

    private static IEnumerable<TileDefinition> ParseAsset(string path)
    {
        string baseName = Path.GetFileNameWithoutExtension(path);

        using var fileStream = new FileStream(path, FileMode.Open);
        using var bitmap = SKBitmap.Decode(fileStream);
        int width = bitmap.Width;
        int height = bitmap.Height;

        if (width != IMAGE_SIZE || height != IMAGE_SIZE)
        {
            throw new InvalidDataException($"Invalid image size {width}x{height} ({path})");
        }

        // Process each rotation (0, 90, 180, 270 degrees)
        for (int rotation = 0; rotation < 4; rotation++)
        {
            // Process both normal and flipped versions
            for (int flip = 0; flip < 2; flip++)
            {
                bool flipped = flip == 1;
                var definition = ProcessRotatedAsset(bitmap, rotation, flipped);
                definition.Name = $"{baseName}_rot{rotation}{(flipped ? "_flipped" : "")}";
                yield return definition;
            }
        }
    }

    public static Dictionary<string, List<TileDefinition>> LoadAssets()
    {
        var returnDict = new Dictionary<string, List<TileDefinition>>();
        // Enumerate directories
        var directories = Directory.GetDirectories("Content/Tiles");

        foreach (var directory in directories)
        {
            var files = Directory.GetFiles(directory, "*.png");
            var biomeName = Path.GetFileName(directory);
            var tiles = new List<TileDefinition>();

            foreach (var file in files)
            {
                var rotations = ParseAsset(file);
                tiles.AddRange(rotations);
            }

            // Now we can calculate the possible neighbors for each tile
            // foreach (var tile in tiles)
            // {
            //     EdgeHasher.CalculatePossibleNeighbors(tile, tiles);
            // }

            returnDict.Add(biomeName, tiles);
        }

        return returnDict;
    }
}
