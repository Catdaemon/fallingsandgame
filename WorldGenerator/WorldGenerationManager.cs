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
    private Stack<(
        Tile tile,
        List<TileDefinition> previousState,
        List<TileDefinition> removedPossibilities
    )> DecisionHistory = new();

    public Random Random;

    public WorldGenerationManager() { }

    public void LoadAssets()
    {
        BiomeTiles = AssetLoader.LoadAssets();
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

    // <summary>
    // Generate a world with the given width and height in tiles
    // </summary>
    public GeneratedWorldInstance GenerateWorld(
        string seed,
        string biome,
        int width,
        int height,
        List<Tile> presetTiles
    )
    {
        Random = new Random(seed.GetHashCode());
        UncollapsedTiles.Clear();
        DecisionHistory.Clear(); // Clear backtracking history

        var totalTileCount = width * height;
        var completedTileCount = 0;
        var world = new Tile[width, height];
        DateTime startTime = DateTime.Now;
        TimeSpan timeout = TimeSpan.FromSeconds(30); // Adjust as needed

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

        // Set the preset tiles
        foreach (var tile in presetTiles)
        {
            world[tile.X, tile.Y] = tile;
            completedTileCount++;
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
                if (neighbor.Entropy <= 1)
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
