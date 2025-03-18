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
    public static IEnumerable<Vertices> Generate(FallingSandWorldChunk chunk)
    {
        var width = Constants.CHUNK_WIDTH;
        bool[,] data = new bool[Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT];
        var nSolidPixels = 0;

        // Convert chunk pixels to grid data
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = chunk.pixels[y * width + x];
                if (pixel.Data.Material == Material.Empty || pixel.IsGas || pixel.IsLiquid)
                {
                    data[x, y] = false;
                }
                else
                {
                    data[x, y] = true;
                    nSolidPixels++;
                }
            }
        }

        // Don't bother generating physics for very small amounts of pixels
        if (nSolidPixels < 3)
        {
            return [];
        }

        try
        {
            // Get thread-local instance and generate polygons
            var polygons = PixelsToPolygons
                .GetInstance()
                .Generate(ref data, Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT)
                .ToList();

            // Scale polygons to physics world
            foreach (var poly in polygons)
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    poly[i] = FallingSand.Convert.PixelsToMeters(poly[i]);
                }
            }

            return polygons;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error generating physics body: {e}");
            return [];
        }
    }
}
