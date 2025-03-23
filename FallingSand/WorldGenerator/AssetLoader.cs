using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace FallingSand.WorldGenerator;

public class Prefab
{
    public string Ref { get; set; }
    public int[] Location { get; set; }
    public int[] Size { get; set; }
    public string Image { get; set; }
    public Material[,] PixelData { get; set; }
}

static class AssetLoader
{
    private const int IMAGE_SIZE = WorldGenerationManager.IMAGE_SIZE;
    private static readonly Dictionary<Color, Material> ColorMaterialMap = new()
    {
        { Color.Black, Material.Empty },
        { Color.White, Material.Stone },
        { new Color(0, 255, 0), Material.Grass },
        { new Color(0, 0, 255), Material.Water },
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
        string directory = Path.GetDirectoryName(path);

        // Skip prefab images - they'll be handled separately in the LoadPrefabs method
        if (baseName.StartsWith("prefab_"))
        {
            Console.WriteLine($"Skipping prefab image {path} during regular tile loading");
            yield break;
        }

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

    public static List<Prefab> LoadPrefabs()
    {
        var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var prefabsPath = Path.Combine(root, "Content", "prefabs.json");
        var prefabs = new List<Prefab>();

        if (File.Exists(prefabsPath))
        {
            var jsonString = File.ReadAllText(prefabsPath);
            prefabs = JsonSerializer.Deserialize<List<Prefab>>(
                jsonString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            // Load the image for each prefab
            foreach (var prefab in prefabs)
            {
                // Load the prefab image
                var imagePath = Path.Combine(root, "Content", prefab.Image);
                Console.WriteLine($"Loading prefab image: {imagePath}");

                if (File.Exists(imagePath))
                {
                    using var fileStream = new FileStream(imagePath, FileMode.Open);
                    using var bitmap = SKBitmap.Decode(fileStream);

                    if (bitmap == null)
                    {
                        Console.WriteLine($"Error: Failed to decode prefab image {prefab.Image}");
                        continue;
                    }

                    // Verify the image dimensions match the expected size
                    int expectedWidth = prefab.Size[0] * IMAGE_SIZE;
                    int expectedHeight = prefab.Size[1] * IMAGE_SIZE;

                    Console.WriteLine(
                        $"Prefab image dimensions: {bitmap.Width}x{bitmap.Height}, Expected: {expectedWidth}x{expectedHeight}"
                    );

                    // Allow some flexibility in the image size - it doesn't have to exactly match but should be divisible by IMAGE_SIZE
                    if (bitmap.Width % IMAGE_SIZE != 0 || bitmap.Height % IMAGE_SIZE != 0)
                    {
                        Console.WriteLine(
                            $"Warning: Prefab image {prefab.Image} dimensions ({bitmap.Width}x{bitmap.Height}) are not multiples of tile size ({IMAGE_SIZE})"
                        );
                    }

                    // Adjust the prefab size based on the actual image dimensions if needed
                    int actualTileWidth = bitmap.Width / IMAGE_SIZE;
                    int actualTileHeight = bitmap.Height / IMAGE_SIZE;

                    if (actualTileWidth != prefab.Size[0] || actualTileHeight != prefab.Size[1])
                    {
                        Console.WriteLine(
                            $"Adjusting prefab {prefab.Ref} size from [{prefab.Size[0]}, {prefab.Size[1]}] to [{actualTileWidth}, {actualTileHeight}] based on image dimensions"
                        );
                        prefab.Size[0] = actualTileWidth;
                        prefab.Size[1] = actualTileHeight;
                    }

                    // Convert the image to material data
                    prefab.PixelData = new Material[bitmap.Width, bitmap.Height];
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            SKColor pixel = bitmap.GetPixel(x, y);
                            var color = new Color(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);

                            if (ColorMaterialMap.TryGetValue(color, out var material))
                            {
                                prefab.PixelData[x, y] = material;
                            }
                            else
                            {
                                prefab.PixelData[x, y] = Material.Empty;
                                // Only log a few warnings to avoid console spam
                                if (x % 100 == 0 && y % 100 == 0)
                                {
                                    Console.WriteLine(
                                        $"Warning: Unknown color {color} in prefab {prefab.Ref}"
                                    );
                                }
                            }
                        }
                    }

                    Console.WriteLine(
                        $"Successfully loaded prefab {prefab.Ref} ({prefab.Size[0]}x{prefab.Size[1]} tiles)"
                    );
                }
                else
                {
                    Console.WriteLine($"Warning: Prefab image not found: {imagePath}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Warning: Prefabs file not found at {prefabsPath}");
        }

        return prefabs;
    }

    public static Dictionary<string, List<TileDefinition>> LoadAssets()
    {
        var returnDict = new Dictionary<string, List<TileDefinition>>();
        // Enumerate directories
        var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var path = Path.Combine(root, "Content", "Tiles");
        var directories = Directory.GetDirectories(path);

        foreach (var directory in directories)
        {
            var files = Directory.GetFiles(Path.Combine(root, directory), "*.png");
            var biomeName = Path.GetFileName(directory);
            var tiles = new List<TileDefinition>();

            foreach (var file in files)
            {
                // Skip files that start with "prefab_" - they'll be handled by the LoadPrefabs method
                if (Path.GetFileName(file).StartsWith("prefab_"))
                {
                    Console.WriteLine($"Skipping prefab image during tile loading: {file}");
                    continue;
                }

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
