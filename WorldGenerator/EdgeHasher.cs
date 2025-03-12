using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FallingSandWorld;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.WorldGenerator;

enum EdgeDirection
{
    TOP = 0,
    RIGHT = 1,
    BOTTOM = 2,
    LEFT = 3,
}

static class EdgeHasher
{
    private const int IMAGE_SIZE = WorldGenerationManager.IMAGE_SIZE;

    public static EdgeDirection GetOppositeEdge(EdgeDirection direction)
    {
        return direction switch
        {
            EdgeDirection.TOP => EdgeDirection.BOTTOM,
            EdgeDirection.RIGHT => EdgeDirection.LEFT,
            EdgeDirection.BOTTOM => EdgeDirection.TOP,
            EdgeDirection.LEFT => EdgeDirection.RIGHT,
            _ => throw new NotImplementedException("Invalid edge direction"),
        };
    }

    // <summary>
    // Calculate the edge patterns for a given tile
    // </summary>
    // <param name="current">The current tile</param>
    public static void CalculateEdgeHashes(TileDefinition current)
    {
        // Calculate the edge patterns for the current tile
        foreach (EdgeDirection edge in Enum.GetValues<EdgeDirection>())
        {
            // For larger image sizes, we can use a string representation
            var edgePattern = new System.Text.StringBuilder(IMAGE_SIZE);

            for (int j = 0; j < IMAGE_SIZE; j++)
            {
                int x = 0,
                    y = 0;

                // Get coordinates of the edge pixel
                switch (edge)
                {
                    case EdgeDirection.TOP:
                        x = j;
                        y = 0;
                        break;
                    case EdgeDirection.RIGHT:
                        x = IMAGE_SIZE - 1;
                        y = j;
                        break;
                    case EdgeDirection.BOTTOM:
                        x = j;
                        y = IMAGE_SIZE - 1;
                        break;
                    case EdgeDirection.LEFT:
                        x = 0;
                        y = j;
                        break;
                }

                // Append '1' for filled, '0' for empty
                edgePattern.Append(
                    current.PixelData[y * IMAGE_SIZE + x] == Material.Empty ? '0' : '1'
                );
            }

            // Store the actual edge pattern for exact comparison
            current.EdgePatterns[(int)edge] = edgePattern.ToString();
        }
    }

    // <summary>
    // Check if two edges are compatible
    // </summary>
    public static bool AreEdgesCompatible(string edge1, string edge2)
    {
        if (edge1.Length != edge2.Length)
            return false;

        // Opposite edges must contain the same pattern so they connect seamlessly
        for (int i = 0; i < edge1.Length; i++)
        {
            if (edge1[i] != edge2[i])
                return false;
        }

        return true;
    }
}
