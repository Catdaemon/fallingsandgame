using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using FallingSand;

namespace FallingSandWorld;

class FallingSandWorldChunkBody
{
    public List<Vector2> vertexes = new List<Vector2>();

    public static FallingSandWorldChunkBody Generate(FallingSandWorldChunk chunk)
    {
        var result = new FallingSandWorldChunkBody();

        // Create a binary grid marking solid pixels
        bool[,] solidGrid = new bool[Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT];
        for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
        {
            for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
            {
                var pixel = chunk.pixels[x, y];
                // Consider a pixel solid if it's not empty, liquid, or gas
                solidGrid[x, y] =
                    pixel.data.Material != Material.Empty && !pixel.IsLiquid && !pixel.IsGas;
            }
        }

        // Find contours/boundaries of solid regions
        List<List<Vector2>> polygons = GeneratePolygons(solidGrid);

        // Add all polygon vertices to the result
        foreach (var polygon in polygons)
        {
            // Add vertices of this polygon to the result
            result.vertexes.AddRange(polygon);
        }

        return result;
    }

    private static List<List<Vector2>> GeneratePolygons(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        bool[,] visited = new bool[width + 2, height + 2]; // Padded grid for boundary tracing
        List<List<Vector2>> polygons = new List<List<Vector2>>();

        // Marching squares algorithm implementation to find contours
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Find boundaries between solid and non-solid pixels
                if (grid[x, y] && !visited[x + 1, y + 1])
                {
                    // Found a new contour starting point
                    List<Vector2> polygon = TraceBoundary(grid, visited, x, y);
                    if (polygon.Count >= 3) // Only add valid polygons
                    {
                        // Simplify polygon to reduce vertex count while maintaining shape
                        polygon = SimplifyPolygon(polygon);
                        polygons.Add(polygon);
                    }
                }
            }
        }

        return polygons;
    }

    private static List<Vector2> TraceBoundary(
        bool[,] grid,
        bool[,] visited,
        int startX,
        int startY
    )
    {
        List<Vector2> contour = new List<Vector2>();
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // Direction vectors: right, down, left, up
        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };

        // Start at the top-left corner of the pixel
        contour.Add(new Vector2(startX, startY));
        visited[startX + 1, startY + 1] = true;

        int direction = 0; // Start moving right
        int x = startX;
        int y = startY;

        // Moore-Neighbor tracing algorithm (modified for our use)
        do
        {
            // Try to find next boundary pixel
            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                // Try current direction first, then clockwise
                int newDir = (direction + i) % 4;
                int newX = x + dx[newDir];
                int newY = y + dy[newDir];

                // Check if this neighbor is valid and part of the solid region
                if (
                    newX >= 0
                    && newX < width
                    && newY >= 0
                    && newY < height
                    && grid[newX, newY]
                    && !visited[newX + 1, newY + 1]
                )
                {
                    // Found next boundary pixel
                    x = newX;
                    y = newY;
                    direction = newDir;
                    contour.Add(new Vector2(x, y));
                    visited[x + 1, newY + 1] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // If no unvisited solid neighbor was found, rotate direction
                direction = (direction + 1) % 4;

                // If we've tried all directions and found nothing, break
                if (direction == 0)
                    break;
            }
        } while (!(x == startX && y == startY) && contour.Count < width * height);

        return contour;
    }

    private static List<Vector2> SimplifyPolygon(List<Vector2> polygon)
    {
        // Ramer-Douglas-Peucker algorithm to simplify polygons
        if (polygon.Count <= 3)
            return polygon;

        List<Vector2> result = new List<Vector2>();
        float epsilon = 0.5f; // Simplification tolerance

        // Find point with maximum distance
        int index = 0;
        float maxDist = 0;

        for (int i = 1; i < polygon.Count - 1; i++)
        {
            float dist = PerpendicularDistance(polygon[i], polygon[0], polygon[^1]);
            if (dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }

        // If max distance is greater than epsilon, recursively simplify
        if (maxDist > epsilon)
        {
            var results1 = SimplifyPolygonSection(polygon, 0, index, epsilon);
            var results2 = SimplifyPolygonSection(polygon, index, polygon.Count - 1, epsilon);

            // Build simplified polygon
            result.AddRange(results1);
            result.AddRange(results2.Skip(1)); // Skip first point to avoid duplication
        }
        else
        {
            // Distance is small enough, use just the endpoints
            result.Add(polygon[0]);
            result.Add(polygon[^1]);
        }

        return result;
    }

    private static List<Vector2> SimplifyPolygonSection(
        List<Vector2> points,
        int startIndex,
        int endIndex,
        float epsilon
    )
    {
        // Base case: just two points
        if (endIndex - startIndex <= 1)
        {
            return new List<Vector2> { points[startIndex] };
        }

        // Find the point with maximum distance
        float maxDist = 0;
        int index = startIndex;

        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float dist = PerpendicularDistance(points[i], points[startIndex], points[endIndex]);
            if (dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }

        List<Vector2> result = new List<Vector2>();

        // If max distance is greater than epsilon, recursively simplify
        if (maxDist > epsilon)
        {
            var results1 = SimplifyPolygonSection(points, startIndex, index, epsilon);
            var results2 = SimplifyPolygonSection(points, index, endIndex, epsilon);

            // Build simplified polygon section
            result.AddRange(results1);
            result.AddRange(results2.Skip(1)); // Skip first point to avoid duplication
        }
        else
        {
            // Distance is small enough, use just the endpoints
            result.Add(points[startIndex]);
            result.Add(points[endIndex]);
        }

        return result;
    }

    private static float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        float dx = lineEnd.X - lineStart.X;
        float dy = lineEnd.Y - lineStart.Y;

        // Normalize
        float mag = MathF.Sqrt(dx * dx + dy * dy);
        if (mag > 0)
        {
            dx /= mag;
            dy /= mag;
        }

        // Calculate perpendicular distance
        float pvx = point.X - lineStart.X;
        float pvy = point.Y - lineStart.Y;

        // Dot product with perpendicular vector
        float pvDot = dx * pvx + dy * pvy;

        // Scale line vector
        float dsx = pvDot * dx;
        float dsy = pvDot * dy;

        // Subtract from point vector
        float ax = pvx - dsx;
        float ay = pvy - dsy;

        return MathF.Sqrt(ax * ax + ay * ay);
    }
}
