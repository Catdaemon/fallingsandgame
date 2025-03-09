using System;
using System.Collections.Generic;
using System.Linq;
using FallingSand;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Common.ConvexHull;
using nkast.Aether.Physics2D.Common.Decomposition;
using nkast.Aether.Physics2D.Common.PolygonManipulation;

namespace FallingSandWorld;

static class PixelsToPolygons
{
    public const int Empty = 0;
    public const int Solid = 1;

    // Minimum area to be considered a valid polygon
    private const float MinPolygonArea = 4.0f;

    // Maximum number of polygons to prevent performance issues
    private const int MaxPolygons = 20;

    // Maximum vertices per polygon - physics engines prefer simpler polygons
    private const int MaxVerticesPerPolygon = 8;

    // Reduction tolerance for polygon simplification
    private const float SimplificationTolerance = 1.0f;

    // The four neighbor directions (up, right, down, left)
    private static readonly int[] DX = { 0, 1, 0, -1 };
    private static readonly int[] DY = { -1, 0, 1, 0 };

    public static IEnumerable<Vertices> Generate(int[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // Create a lower resolution grid to reduce complexity
        int[,] downsampledGrid = DownsampleGrid(grid, width, height, 3);
        int downsampledWidth = downsampledGrid.GetLength(0);
        int downsampledHeight = downsampledGrid.GetLength(1);

        // Track visited cells
        bool[,] visited = new bool[downsampledWidth, downsampledHeight];

        // List of connected solid regions
        List<List<Point>> solidRegions = new List<List<Point>>();

        // Find all connected solid regions in the downsampled grid
        for (int y = 0; y < downsampledHeight; y++)
        {
            for (int x = 0; x < downsampledWidth; x++)
            {
                // Skip if already visited or not solid
                if (visited[x, y] || downsampledGrid[x, y] == Empty)
                    continue;

                // Found a new solid pixel, do a flood fill to find all connected solid pixels
                List<Point> region = new List<Point>();
                FloodFill(
                    downsampledGrid,
                    visited,
                    x,
                    y,
                    downsampledWidth,
                    downsampledHeight,
                    region
                );

                // Only add regions with more than 1 pixel (discard single pixels)
                if (region.Count > 1)
                {
                    solidRegions.Add(region);
                }
            }
        }

        // Generate polygons from solid regions
        List<Vertices> allPolygons = new List<Vertices>();

        // Process regions in order of size (largest first)
        foreach (var region in solidRegions.OrderByDescending(r => r.Count))
        {
            Vertices rawPolygon = CreatePolygonFromRegion(region);

            if (rawPolygon == null || rawPolygon.Count < 3)
                continue;

            // First simplify to remove unnecessary vertices
            Vertices simplified = AggressivelySimplifyPolygon(rawPolygon);

            if (simplified.Count < 3)
                continue;

            // Scale back to original resolution
            for (int i = 0; i < simplified.Count; i++)
            {
                simplified[i] = simplified[i] * 3; // Multiply by our downsampling factor
            }

            // If polygon is still too complex, decompose it
            if (simplified.Count > MaxVerticesPerPolygon)
            {
                // Use Bayazit decomposition to create smaller convex polygons
                var decomposed = Triangulate.ConvexPartition(
                    simplified,
                    TriangulationAlgorithm.Bayazit
                );

                foreach (var poly in decomposed)
                {
                    if (poly.Count >= 3 && Math.Abs(poly.GetArea()) >= MinPolygonArea)
                    {
                        allPolygons.Add(poly);

                        // Check if we've reached the maximum polygon count
                        if (allPolygons.Count >= MaxPolygons)
                            break;
                    }
                }
            }
            else
            {
                // Polygon is already simple enough
                if (Math.Abs(simplified.GetArea()) >= MinPolygonArea)
                {
                    allPolygons.Add(simplified);
                }
            }

            // Check if we've reached the maximum polygon count
            if (allPolygons.Count >= MaxPolygons)
                break;
        }

        return allPolygons;
    }

    private static int[,] DownsampleGrid(int[,] grid, int width, int height, int factor)
    {
        int newWidth = width / factor;
        int newHeight = height / factor;

        // Ensure we have at least 1 cell in each dimension
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);

        int[,] result = new int[newWidth, newHeight];

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                // A cell in the downsampled grid is solid if any original cell is solid
                bool anySolid = false;

                for (int dy = 0; dy < factor && (y * factor + dy) < height; dy++)
                {
                    for (int dx = 0; dx < factor && (x * factor + dx) < width; dx++)
                    {
                        if (grid[x * factor + dx, y * factor + dy] == Solid)
                        {
                            anySolid = true;
                            break;
                        }
                    }
                    if (anySolid)
                        break;
                }

                result[x, y] = anySolid ? Solid : Empty;
            }
        }

        return result;
    }

    private static void FloodFill(
        int[,] grid,
        bool[,] visited,
        int startX,
        int startY,
        int width,
        int height,
        List<Point> region
    )
    {
        // Use a queue for breadth-first traversal
        Queue<Point> queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        visited[startX, startY] = true;
        region.Add(new Point(startX, startY));

        while (queue.Count > 0)
        {
            Point current = queue.Dequeue();

            // Check all 4 neighbors
            for (int i = 0; i < 4; i++)
            {
                int nx = current.X + DX[i];
                int ny = current.Y + DY[i];

                // Check bounds and if it's solid and unvisited
                if (
                    nx >= 0
                    && nx < width
                    && ny >= 0
                    && ny < height
                    && grid[nx, ny] == Solid
                    && !visited[nx, ny]
                )
                {
                    visited[nx, ny] = true;
                    queue.Enqueue(new Point(nx, ny));
                    region.Add(new Point(nx, ny));
                }
            }
        }
    }

    private static Vertices CreatePolygonFromRegion(List<Point> region)
    {
        try
        {
            // Use the convex hull algorithm from the physics library
            Vertices polygon = new Vertices();

            // Add all points to the vertices list
            foreach (var point in region)
            {
                polygon.Add(new Vector2(point.X, point.Y));
            }

            // Create a convex hull of the region
            return GiftWrap.GetConvexHull(polygon);
        }
        catch (Exception)
        {
            // Fallback to simpler method if gift wrapping fails
            return CreateSimpleRectangleFromRegion(region);
        }
    }

    private static Vertices CreateSimpleRectangleFromRegion(List<Point> region)
    {
        // Create a simple rectangular polygon based on region bounds
        int minX = region.Min(p => p.X);
        int minY = region.Min(p => p.Y);
        int maxX = region.Max(p => p.X);
        int maxY = region.Max(p => p.Y);

        Vertices rectangle = new Vertices(4);
        rectangle.Add(new Vector2(minX, minY));
        rectangle.Add(new Vector2(maxX, minY));
        rectangle.Add(new Vector2(maxX, maxY));
        rectangle.Add(new Vector2(minX, maxY));

        return rectangle;
    }

    private static Vertices AggressivelySimplifyPolygon(Vertices vertices)
    {
        if (vertices == null || vertices.Count <= 3)
            return vertices;

        try
        {
            // Apply multiple simplification techniques
            Vertices simplified = SimplifyTools.CollinearSimplify(vertices);
            simplified = SimplifyTools.DouglasPeuckerSimplify(simplified, SimplificationTolerance);
            simplified = SimplifyTools.ReduceByDistance(simplified, SimplificationTolerance);

            // If still too complex, use more aggressive simplification
            if (simplified.Count > MaxVerticesPerPolygon)
            {
                float tolerance = SimplificationTolerance;
                while (simplified.Count > MaxVerticesPerPolygon && tolerance < 5.0f)
                {
                    tolerance += 0.5f;
                    simplified = SimplifyTools.DouglasPeuckerSimplify(vertices, tolerance);
                    if (simplified.Count < 3)
                        return vertices; // Don't over-simplify
                }
            }

            return simplified.Count >= 3 ? simplified : vertices;
        }
        catch
        {
            return vertices;
        }
    }
}
