using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FallingSand;

namespace FallingSandWorld;

class FallingSandWorldChunkBodyOld
{
    // Store the polygons representing solid areas in the chunk
    public List<List<Vector2>> Polygons { get; private set; } = new List<List<Vector2>>();

    // Maximum number of vertices per contour
    private const int MAX_VERTICES = 32;

    // Maximum number of contours to generate per chunk
    private const int MAX_CONTOURS = 64;

    // Minimum area for a blob to be considered valid (to avoid tiny fragments)
    private const int MIN_BLOB_SIZE = 4;

    // Direction vectors for the 8 neighbors (clockwise from right)
    private static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    /// <summary>
    /// Generates one or more polygon bodies representing solid areas in the chunk
    /// </summary>
    public static IEnumerable<FallingSandWorldChunkBodyOld> Generate(FallingSandWorldChunk chunk)
    {
        // Create a binary grid marking solid vs. non-solid pixels
        bool[,] solidGrid = CreateSolidGrid(chunk);

        // Find connected components in the grid and create convex hulls
        var convexHulls = GenerateConvexHulls(solidGrid);

        // If no valid contours were found but there are solid pixels, create a fallback
        if (convexHulls.Count == 0 && HasAnySolidPixels(solidGrid))
        {
            // convexHulls.Add(CreateFallbackContour(solidGrid));
        }

        // Create a single body with all polygons
        var body = new FallingSandWorldChunkBodyOld { Polygons = convexHulls };
        return new[] { body };
    }

    /// <summary>
    /// Creates a binary grid marking solid vs. non-solid pixels
    /// </summary>
    private static bool[,] CreateSolidGrid(FallingSandWorldChunk chunk)
    {
        bool[,] grid = new bool[Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT];

        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = chunk.GetPixel(x, y);

                // Skip empty, liquids and gases
                if (pixel.Data.Material == Material.Empty || pixel.IsLiquid || pixel.IsGas)
                {
                    grid[x, y] = false;
                }
                else
                {
                    grid[x, y] = true;
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// Finds connected blobs and generates convex hulls for them
    /// </summary>
    private static List<List<Vector2>> GenerateConvexHulls(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // Find all connected components
        List<List<(int x, int y)>> blobs = FindBlobs(grid);

        // Generate convex hulls for each blob
        List<List<Vector2>> hulls = new List<List<Vector2>>();

        foreach (var blob in blobs)
        {
            if (blob.Count < MIN_BLOB_SIZE)
                continue;

            // Get the points for this blob
            List<Vector2> points = blob.Select(p => new Vector2(p.x, p.y)).ToList();

            // Find convex hull for these points
            var hull = ComputeConvexHull(points);
            if (hull.Count >= 3)
            {
                hulls.Add(hull);

                // Limit the number of hulls to avoid performance issues
                if (hulls.Count >= MAX_CONTOURS)
                    break;
            }
        }

        return hulls;
    }

    /// <summary>
    /// Calculate the convex hull using Graham Scan algorithm
    /// </summary>
    private static List<Vector2> ComputeConvexHull(List<Vector2> points)
    {
        if (points.Count <= 3)
            return new List<Vector2>(points);

        // Find the point with the lowest y-coordinate (and leftmost if tied)
        Vector2 anchor = points[0];
        foreach (var point in points)
        {
            if (point.Y < anchor.Y || (point.Y == anchor.Y && point.X < anchor.X))
            {
                anchor = point;
            }
        }

        // Sort points by polar angle with respect to anchor
        var sorted = points
            .OrderBy(p =>
            {
                if (p.X == anchor.X && p.Y == anchor.Y)
                    return float.NegativeInfinity; // Anchor comes first

                return (float)Math.Atan2(p.Y - anchor.Y, p.X - anchor.X);
            })
            .ToList();

        // Initialize hull with first three points
        Stack<Vector2> hull = new Stack<Vector2>();
        hull.Push(sorted[0]);

        if (sorted.Count > 1)
            hull.Push(sorted[1]);

        // Process remaining points
        for (int i = 2; i < sorted.Count; i++)
        {
            Vector2 top = hull.Pop();

            // Keep removing points while the angle is not making a left turn
            while (hull.Count > 0 && !IsLeftTurn(hull.Peek(), top, sorted[i]))
            {
                top = hull.Pop();
            }

            hull.Push(top);
            hull.Push(sorted[i]);
        }

        // Limit the number of vertices in the hull
        var hullList = hull.Reverse().ToList();
        if (hullList.Count > MAX_VERTICES)
        {
            var simplifiedHull = new List<Vector2>();
            double step = (double)hullList.Count / MAX_VERTICES;

            for (int i = 0; i < MAX_VERTICES; i++)
            {
                int index = Math.Min((int)(i * step), hullList.Count - 1);
                simplifiedHull.Add(hullList[index]);
            }

            return simplifiedHull;
        }

        return hullList;
    }

    /// <summary>
    /// Determines if three points make a left turn
    /// </summary>
    private static bool IsLeftTurn(Vector2 a, Vector2 b, Vector2 c)
    {
        // Calculate cross product
        return ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) > 0;
    }

    /// <summary>
    /// Check if the grid has any solid pixels
    /// </summary>
    private static bool HasAnySolidPixels(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y])
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a fallback contour covering the entire chunk
    /// </summary>
    private static List<Vector2> CreateFallbackContour(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        return new List<Vector2>
        {
            new Vector2(0, 0),
            new Vector2(width, 0),
            new Vector2(width, height),
            new Vector2(0, height),
        };
    }

    /// <summary>
    /// Finds all blobs (connected components) in the grid
    /// </summary>
    private static List<List<(int x, int y)>> FindBlobs(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        bool[,] visited = new bool[width, height];
        List<List<(int x, int y)>> blobs = new List<List<(int x, int y)>>();

        // Scan for unvisited solid pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] && !visited[x, y])
                {
                    // Found a new blob, perform flood fill to find all connected pixels
                    var blob = new List<(int x, int y)>();
                    Queue<(int x, int y)> queue = new Queue<(int x, int y)>();

                    queue.Enqueue((x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        blob.Add((cx, cy));

                        // Check all 8 neighbors for more robust connection detection
                        for (int i = 0; i < 8; i++)
                        {
                            int nx = cx + DX[i];
                            int ny = cy + DY[i];

                            if (
                                nx >= 0
                                && nx < width
                                && ny >= 0
                                && ny < height
                                && grid[nx, ny]
                                && !visited[nx, ny]
                            )
                            {
                                visited[nx, ny] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }

                    blobs.Add(blob);
                }
            }
        }

        return blobs;
    }
}
