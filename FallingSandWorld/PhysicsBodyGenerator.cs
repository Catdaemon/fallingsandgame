using System;
using System.Collections.Generic;
using System.Linq;
using FallingSand;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Common.PolygonManipulation;

namespace FallingSandWorld;

static class PhysicsBodyGenerator
{
    // Maximum number of polygons to generate per chunk
    private const int MaxPolygonsPerChunk = 15;

    // Maximum vertices per polygon for physics simulation
    private const int MaxVerticesPerPolygon = 8;

    public static IEnumerable<Vertices> Generate(FallingSandWorldChunk chunk)
    {
        int[,] data = new int[Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT];
        var nSolidPixels = 0;

        // Convert chunk pixels to grid data
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = chunk.pixels[y * Constants.CHUNK_WIDTH + x];
                if (pixel.Data.Material == Material.Empty || pixel.IsGas || pixel.IsLiquid)
                {
                    data[x, y] = PixelsToPolygons.Empty;
                }
                else
                {
                    data[x, y] = PixelsToPolygons.Solid;
                    nSolidPixels++;
                }
            }
        }

        // Don't bother generating physics for very small amounts of pixels
        if (nSolidPixels < 3)
        {
            return new List<Vertices>();
        }

        try
        {
            // Generate polygons using our simplified approach
            var polygons = PixelsToPolygons.Generate(data).ToList();

            // Final check to ensure no polygon has too many vertices
            List<Vertices> finalPolygons = new List<Vertices>();

            foreach (var poly in polygons)
            {
                if (poly.Count <= MaxVerticesPerPolygon)
                {
                    finalPolygons.Add(poly);
                }
                else
                {
                    // Further simplify if needed
                    Vertices simplified = SimplifyTools.DouglasPeuckerSimplify(poly, 1.5f);
                    if (simplified.Count <= MaxVerticesPerPolygon && simplified.Count >= 3)
                    {
                        finalPolygons.Add(simplified);
                    }
                }
            }

            // Limit the number of polygons
            if (finalPolygons.Count > MaxPolygonsPerChunk)
            {
                finalPolygons = finalPolygons
                    .OrderByDescending(p => Math.Abs(p.GetArea()))
                    .Take(MaxPolygonsPerChunk)
                    .ToList();
            }

            // Scale polygons to physics world
            foreach (var poly in finalPolygons)
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    poly[i] = FallingSand.Convert.PixelsToMeters(poly[i]);
                }
            }

            // Log statistics for debugging
            // int totalVertices = finalPolygons.Sum(p => p.Count);
            // Console.WriteLine($"Generated {finalPolygons.Count} polygons with {totalVertices} total vertices");

            return finalPolygons;
        }
        catch (Exception e)
        {
            // In case of any error, log it and return empty collection
            System.Console.WriteLine($"Error generating physics body: {e}");
            return new List<Vertices>();
        }
    }
}
