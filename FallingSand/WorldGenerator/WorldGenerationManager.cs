using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace FallingSand.WorldGenerator;

class WorldGenerationManager
{
    public const int IMAGE_SIZE = 128;
    private Dictionary<string, List<TileDefinition>> BiomeTiles = [];
    private readonly PriorityQueue<Tile, int> UncollapsedTiles = new();
    private readonly Stack<(
        Tile tile,
        List<TileDefinition> previousState,
        List<TileDefinition> removedPossibilities
    )> DecisionHistory = new();
    public List<Prefab> Prefabs = [];
    public Random Random;

    public WorldGenerationManager() { }

    public void LoadAssets()
    {
        BiomeTiles = AssetLoader.LoadAssets();
        Prefabs = AssetLoader.LoadPrefabs();
    }

    private Tile GetNextTileToCollapse()
    {
        while (UncollapsedTiles.Count > 0)
        {
            var tile = UncollapsedTiles.Dequeue();

            // Only return tiles with entropy > 1
            if (tile.Entropy > 1)
                return tile;
        }
        return null;
    }

    // Modify how you enqueue tiles to properly use the priority queue
    private void EnqueueTile(Tile tile)
    {
        if (tile.Entropy > 1)
        {
            // Use entropy directly as the priority - lower values are dequeued first
            UncollapsedTiles.Enqueue(tile, tile.Entropy);
        }
    }

    public static IEnumerable<(EdgeDirection, Tile)> GetNeighbors(Tile tile, Tile[,] world)
    {
        var neighbors = new List<(EdgeDirection, Tile)>();

        if (tile.X > 0)
        {
            neighbors.Add((EdgeDirection.LEFT, world[tile.X - 1, tile.Y]));
        }
        if (tile.X < world.GetLength(0) - 1)
        {
            neighbors.Add((EdgeDirection.RIGHT, world[tile.X + 1, tile.Y]));
        }
        if (tile.Y > 0)
        {
            neighbors.Add((EdgeDirection.TOP, world[tile.X, tile.Y - 1]));
        }
        if (tile.Y < world.GetLength(1) - 1)
        {
            neighbors.Add((EdgeDirection.BOTTOM, world[tile.X, tile.Y + 1]));
        }

        return neighbors;
    }

    private void ShufflePossibilities(List<TileDefinition> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]); // Swap elements
        }
    }

    // Process prefabs and convert them into preset tiles
    private List<Tile> ProcessPrefabs(int worldWidth, int worldHeight, string biome)
    {
        var presetTiles = new List<Tile>();

        foreach (var prefab in Prefabs)
        {
            // Get prefab location in grid coordinates
            int prefabX = prefab.Location[0];
            int prefabY = prefab.Location[1];
            int prefabWidth = prefab.Size[0];
            int prefabHeight = prefab.Size[1];

            // Check if prefab is within world bounds
            if (
                prefabX < 0
                || prefabY < 0
                || prefabX + prefabWidth > worldWidth
                || prefabY + prefabHeight > worldHeight
            )
            {
                Console.WriteLine(
                    $"Warning: Prefab {prefab.Ref} is outside world bounds and will be partially or completely skipped"
                );
                continue;
            }

            // Calculate how many tiles this prefab will create
            int tileWidth = prefab.PixelData.GetLength(0) / IMAGE_SIZE;
            int tileHeight = prefab.PixelData.GetLength(1) / IMAGE_SIZE;

            // Create tiles from prefab image data
            for (int ty = 0; ty < prefabHeight && ty < tileHeight; ty++)
            {
                for (int tx = 0; tx < prefabWidth && tx < tileWidth; tx++)
                {
                    // Calculate world coordinates for this tile
                    int worldX = prefabX + tx;
                    int worldY = prefabY + ty;

                    if (worldX >= worldWidth || worldY >= worldHeight)
                        continue;

                    // Extract the material data for this specific tile from the prefab
                    Material[] tileData = new Material[IMAGE_SIZE * IMAGE_SIZE];

                    for (int y = 0; y < IMAGE_SIZE; y++)
                    {
                        for (int x = 0; x < IMAGE_SIZE; x++)
                        {
                            int prefabPixelX = tx * IMAGE_SIZE + x;
                            int prefabPixelY = ty * IMAGE_SIZE + y;

                            // Make sure the coordinate is within the prefab image bounds
                            if (
                                prefabPixelX < prefab.PixelData.GetLength(0)
                                && prefabPixelY < prefab.PixelData.GetLength(1)
                            )
                            {
                                tileData[y * IMAGE_SIZE + x] = prefab.PixelData[
                                    prefabPixelX,
                                    prefabPixelY
                                ];
                            }
                            else
                            {
                                tileData[y * IMAGE_SIZE + x] = Material.Empty;
                            }
                        }
                    }

                    // Create a special TileDefinition for this prefab tile
                    var prefabTileDefinition = new TileDefinition
                    {
                        PixelData = tileData,
                        Name = $"{prefab.Ref}_x{worldX}_y{worldY}",
                    };

                    // Calculate edge hashes to ensure proper connections
                    EdgeHasher.CalculateEdgeHashes(prefabTileDefinition);

                    // Create a tile with this single possibility (fully collapsed)
                    var prefabTile = new Tile
                    {
                        X = worldX,
                        Y = worldY,
                        Possibilities = [prefabTileDefinition],
                        IsPrefabTile = true,
                    };

                    presetTiles.Add(prefabTile);
                }
            }
        }

        return presetTiles;
    }

    /// <summary>
    /// Generate a world with the given width and height in tiles
    /// </summary>
    public GeneratedWorldInstance GenerateWorld(
        string seed,
        string biome,
        int width,
        int height,
        List<Tile> presetTiles = null
    )
    {
        Random = new Random(seed.GetHashCode());
        UncollapsedTiles.Clear();
        DecisionHistory.Clear(); // Clear backtracking history

        var totalTileCount = width * height;
        var completedTileCount = 0;
        var world = new Tile[width, height];

        DateTime startTime = DateTime.Now;
        TimeSpan timeout = TimeSpan.FromSeconds(90); // Adjust as needed

        // Initialize the grid with randomized entropy
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = new Tile
                {
                    X = x,
                    Y = y,
                    Possibilities = [.. BiomeTiles[biome]],
                };

                // Shuffle the possibilities list for more randomness
                ShufflePossibilities(tile.Possibilities);

                world[x, y] = tile;

                EnqueueTile(tile);
            }
        }

        // Process prefabs first
        var prefabTiles = ProcessPrefabs(width, height, biome);
        if (prefabTiles.Count > 0)
        {
            Console.WriteLine($"Placed {prefabTiles.Count} prefab tiles");

            // If we have user-specified preset tiles, add them to the prefab tiles
            if (presetTiles != null)
            {
                prefabTiles.AddRange(presetTiles);
            }

            presetTiles = prefabTiles;
        }

        // Set the preset tiles
        if (presetTiles != null)
        {
            foreach (var tile in presetTiles)
            {
                // Skip tiles outside of world bounds
                if (tile.X < 0 || tile.Y < 0 || tile.X >= width || tile.Y >= height)
                    continue;

                world[tile.X, tile.Y] = tile;
                completedTileCount++;

                // Propagate constraints from prefab tiles to neighbors immediately
                foreach (var (direction, neighbor) in GetNeighbors(tile, world))
                {
                    if (neighbor.Entropy <= 1)
                        continue;

                    // Find valid possibilities for this neighbor
                    var validNeighborPossibilities = new List<TileDefinition>();
                    foreach (var neighborPossibility in neighbor.Possibilities)
                    {
                        bool isValid = false;
                        foreach (var currentPossibility in tile.Possibilities)
                        {
                            if (currentPossibility.CanBeNeighborTo(neighborPossibility, direction))
                            {
                                isValid = true;
                                break;
                            }
                        }

                        if (isValid)
                        {
                            validNeighborPossibilities.Add(neighborPossibility);
                        }
                    }

                    // Update neighbor possibilities
                    if (
                        validNeighborPossibilities.Count < neighbor.Possibilities.Count
                        && validNeighborPossibilities.Count > 0
                    )
                    {
                        neighbor.Possibilities = validNeighborPossibilities;

                        // Update the neighbor in the queue
                        if (neighbor.Entropy > 1)
                        {
                            EnqueueTile(neighbor);
                        }

                        if (neighbor.Entropy == 1)
                            completedTileCount++;
                    }
                }
            }
        }

        // Track modified tiles
        HashSet<Tile> modifiedTiles = new HashSet<Tile>();

        // Initial scan for tile with least entropy
        var leastEntropy = GetNextTileToCollapse();
        int iteration = 0;
        int backtrackCount = 0;
        const int maxBacktracks = 100; // Limit backtracking attempts

        while (leastEntropy != null && leastEntropy.Entropy > 1)
        {
            iteration++;
            if (iteration % 1000 == 0)
            {
                Console.WriteLine(
                    $"Iteration {iteration}, Completed: {completedTileCount}/{totalTileCount}, Backtracks: {backtrackCount}"
                );
            }

            // Check for timeout
            if (DateTime.Now - startTime > timeout)
            {
                Console.WriteLine("WFC generation timed out");
                break;
            }

            // Check backtracking limit
            if (backtrackCount > maxBacktracks)
            {
                Console.WriteLine("Exceeded maximum backtracking attempts");
                break;
            }

            // Before collapsing, save the current state for potential backtracking
            var previousState = new List<TileDefinition>(leastEntropy.Possibilities);

            // Collapse the current tile
            int randomIndex = Random.Next(leastEntropy.Possibilities.Count);
            var selectedTile = leastEntropy.Possibilities[randomIndex];

            // Record the decision for backtracking
            var removedPossibilities = previousState.Where(p => p != selectedTile).ToList();
            DecisionHistory.Push((leastEntropy, previousState, removedPossibilities));

            leastEntropy.Possibilities.Clear();
            leastEntropy.Possibilities.Add(selectedTile);
            completedTileCount++;

            bool contradictionFound = false;

            // Process neighbors
            foreach (var (direction, neighbor) in GetNeighbors(leastEntropy, world))
            {
                if (neighbor.Entropy <= 1 || neighbor.IsPrefabTile)
                    continue;

                // Find valid possibilities for this neighbor
                var validNeighborPossibilities = new List<TileDefinition>();
                foreach (var neighborPossibility in neighbor.Possibilities)
                {
                    foreach (var currentPossibility in leastEntropy.Possibilities)
                    {
                        if (currentPossibility.CanBeNeighborTo(neighborPossibility, direction))
                        {
                            validNeighborPossibilities.Add(neighborPossibility);
                            break;
                        }
                    }
                }

                // Update neighbor possibilities
                if (
                    validNeighborPossibilities.Count < neighbor.Possibilities.Count
                    && validNeighborPossibilities.Count > 0
                )
                {
                    neighbor.Possibilities = validNeighborPossibilities;
                    modifiedTiles.Add(neighbor);

                    // Update the neighbor in the queue
                    if (neighbor.Entropy > 1)
                    {
                        EnqueueTile(neighbor);
                    }

                    if (neighbor.Entropy == 1)
                        completedTileCount++;
                }
                else if (validNeighborPossibilities.Count == 0)
                {
                    // Contradiction detected! We need to backtrack
                    Console.WriteLine(
                        $"Contradiction at ({neighbor.X}, {neighbor.Y}), backtracking..."
                    );
                    contradictionFound = true;
                    backtrackCount++;

                    // Break out of the neighbor loop - we need to backtrack
                    break;
                }
            }

            if (contradictionFound)
            {
                // Handle backtracking
                if (DecisionHistory.Count > 0)
                {
                    // Restore the previous state
                    var (lastTile, lastState, _) = DecisionHistory.Pop();

                    // Remove the possibility that led to this contradiction
                    var badPossibility = lastTile.Possibilities.FirstOrDefault();

                    // Set the tile back to its previous state
                    lastTile.Possibilities = lastState.ToList();

                    // Remove the bad possibility
                    if (badPossibility != null)
                    {
                        lastTile.Possibilities.Remove(badPossibility);
                    }

                    // If the tile now has no possibilities, continue backtracking
                    if (lastTile.Possibilities.Count == 0)
                    {
                        // This will trigger another backtrack in the next iteration
                        leastEntropy = GetNextTileToCollapse();
                        continue;
                    }

                    // Re-add to queue with updated priority
                    UncollapsedTiles.Enqueue(lastTile, lastTile.Possibilities.Count);

                    // Use this as our next tile
                    leastEntropy = lastTile;

                    // Adjust completion count
                    completedTileCount--;

                    continue;
                }
                else
                {
                    // No history to backtrack to, fail
                    throw new InvalidOperationException(
                        $"Contradiction with no backtracking history"
                    );
                }
            }

            // Get the next tile with least entropy
            leastEntropy = GetNextTileToCollapse();
        }

        return new GeneratedWorldInstance() { Tiles = world };
    }
}
