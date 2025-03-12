using System.Collections.Generic;

namespace FallingSand.WorldGenerator;

class GeneratedWorldInstance
{
    public Tile[,] Tiles;

    public Tile GetTileAt(int x, int y)
    {
        return Tiles[x, y];
    }

    public Tile GetTileAt(ChunkPosition position)
    {
        return Tiles[position.X, position.Y];
    }

    // <summary>
    /// Validates that all tile constraints are satisfied in the generated world
    /// </summary>
    /// <returns>A list of positions where constraint violations occurred</returns>
    public List<(int X, int Y, EdgeDirection Direction)> ValidateConstraints()
    {
        var violations = new List<(int X, int Y, EdgeDirection Direction)>();
        int width = Tiles.GetLength(0);
        int height = Tiles.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var currentTile = Tiles[x, y];

                // Skip tiles that haven't been fully collapsed
                if (currentTile.Entropy > 1)
                    continue;

                var currentDefinition = currentTile.Possibilities[0];

                // Check right neighbor
                if (x < width - 1)
                {
                    var rightNeighbor = Tiles[x + 1, y];
                    if (rightNeighbor.Entropy == 1)
                    {
                        var neighborDefinition = rightNeighbor.Possibilities[0];
                        if (
                            !currentDefinition.CanBeNeighborTo(
                                neighborDefinition,
                                EdgeDirection.RIGHT
                            )
                        )
                        {
                            violations.Add((x, y, EdgeDirection.RIGHT));
                        }
                    }
                }

                // Check bottom neighbor
                if (y < height - 1)
                {
                    var bottomNeighbor = Tiles[x, y + 1];
                    if (bottomNeighbor.Entropy == 1)
                    {
                        var neighborDefinition = bottomNeighbor.Possibilities[0];
                        if (
                            !currentDefinition.CanBeNeighborTo(
                                neighborDefinition,
                                EdgeDirection.BOTTOM
                            )
                        )
                        {
                            violations.Add((x, y, EdgeDirection.BOTTOM));
                        }
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Returns true if all constraints in the world are satisfied
    /// </summary>
    public bool IsValid()
    {
        return ValidateConstraints().Count == 0;
    }
}
