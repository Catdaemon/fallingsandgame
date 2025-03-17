using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Common.PolygonManipulation;

namespace FallingSandWorld;

static class PixelsToPolygons
{
    public static IEnumerable<Vertices> Generate(ref bool[,] binaryGrid, int width, int height)
    {
        // Run marching squares to get contours
        List<List<Vector2>> contours = MarchingSquares(binaryGrid, width, height);

        // Convert contours to Vertices objects
        List<Vertices> polygons = [];
        foreach (var contour in contours)
        {
            if (contour.Count >= 3) // Need at least 3 vertices to form a polygon
            {
                Vertices vertices = [.. contour];

                // Ensure the vertices are in counter-clockwise order for physics
                if (!vertices.IsCounterClockWise())
                {
                    vertices.Reverse();
                }

                // Simplify the polygon to reduce vertex count
                vertices = SimplifyTools.ReduceByDistance(vertices, 0.5f);

                // Add valid polygon to result
                if (vertices.Count >= 3)
                {
                    polygons.Add(vertices);
                }
            }
        }

        return polygons;
    }

    private static List<List<Vector2>> MarchingSquares(bool[,] grid, int width, int height)
    {
        // Maps edges indices (encoded as x1,y1,x2,y2) to the contours they belong to
        Dictionary<string, int> edgeToContourMap = [];
        List<List<Vector2>> contours = [];

        // Process each cell (2x2 grid of pixels)
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                // Get the 4 corners of the current cell
                bool topLeft = grid[x, y];
                bool topRight = grid[x + 1, y];
                bool bottomRight = grid[x + 1, y + 1];
                bool bottomLeft = grid[x, y + 1];

                // Determine the case (0-15) based on which corners are solid
                int caseIndex = 0;
                if (topLeft)
                    caseIndex |= 1;
                if (topRight)
                    caseIndex |= 2;
                if (bottomRight)
                    caseIndex |= 4;
                if (bottomLeft)
                    caseIndex |= 8;

                // Skip empty or full cells
                if (caseIndex == 0 || caseIndex == 15)
                    continue;

                // Generate edges based on the case
                List<Edge> edges = GetEdgesForCase(caseIndex, x, y);

                foreach (var edge in edges)
                {
                    // Create a unique key for this edge
                    string edgeKey = GetEdgeKey(edge.Start, edge.End);

                    if (edgeToContourMap.TryGetValue(edgeKey, out int contourIndex))
                    {
                        // This edge already exists in a contour, remove it (handles internal edges)
                        edgeToContourMap.Remove(edgeKey);
                    }
                    else
                    {
                        // Add this edge to a new or existing contour
                        contourIndex = AddEdgeToContours(contours, edge);
                        edgeToContourMap[edgeKey] = contourIndex;
                    }
                }
            }
        }

        // Clean up and close any open contours
        for (int i = 0; i < contours.Count; i++)
        {
            if (
                contours[i].Count > 0
                && !Equals(contours[i][0], contours[i][contours[i].Count - 1])
            )
            {
                contours[i].Add(contours[i][0]); // Close the loop
            }
        }

        return contours;
    }

    private static string GetEdgeKey(Vector2 p1, Vector2 p2)
    {
        // Create a consistent key regardless of direction
        return Equals(p1, p2) ? $"{p1.X},{p1.Y}"
            : (p1.X < p2.X || (p1.X == p2.X && p1.Y < p2.Y)) ? $"{p1.X},{p1.Y},{p2.X},{p2.Y}"
            : $"{p2.X},{p2.Y},{p1.X},{p1.Y}";
    }

    private static int AddEdgeToContours(List<List<Vector2>> contours, Edge edge)
    {
        // Try to add to existing contour
        for (int i = 0; i < contours.Count; i++)
        {
            var contour = contours[i];
            if (contour.Count > 0)
            {
                Vector2 start = contour[0];
                Vector2 end = contour[contour.Count - 1];

                if (Equals(edge.Start, end))
                {
                    contour.Add(edge.End);
                    return i;
                }
                else if (Equals(edge.End, start))
                {
                    contour.Insert(0, edge.Start);
                    return i;
                }
            }
        }

        // Start new contour
        var newContour = new List<Vector2> { edge.Start, edge.End };
        contours.Add(newContour);
        return contours.Count - 1;
    }

    private static List<Edge> GetEdgesForCase(int caseIndex, int x, int y)
    {
        List<Edge> edges = [];
        float cellSize = 1.0f;

        // The midpoints of the cell edges
        Vector2 top = new Vector2(x + 0.5f, y) * cellSize;
        Vector2 right = new Vector2(x + 1, y + 0.5f) * cellSize;
        Vector2 bottom = new Vector2(x + 0.5f, y + 1) * cellSize;
        Vector2 left = new Vector2(x, y + 0.5f) * cellSize;

        // Determine the edges based on the case
        switch (caseIndex)
        {
            case 1: // TopLeft
                edges.Add(new Edge(left, top));
                break;
            case 2: // TopRight
                edges.Add(new Edge(top, right));
                break;
            case 3: // TopLeft + TopRight
                edges.Add(new Edge(left, right));
                break;
            case 4: // BottomRight
                edges.Add(new Edge(right, bottom));
                break;
            case 5: // TopLeft + BottomRight (saddle point)
                edges.Add(new Edge(left, top));
                edges.Add(new Edge(right, bottom));
                break;
            case 6: // TopRight + BottomRight
                edges.Add(new Edge(top, bottom));
                break;
            case 7: // TopLeft + TopRight + BottomRight
                edges.Add(new Edge(left, bottom));
                break;
            case 8: // BottomLeft
                edges.Add(new Edge(bottom, left));
                break;
            case 9: // TopLeft + BottomLeft
                edges.Add(new Edge(top, bottom));
                break;
            case 10: // TopRight + BottomLeft (saddle point)
                edges.Add(new Edge(top, right));
                edges.Add(new Edge(bottom, left));
                break;
            case 11: // TopLeft + TopRight + BottomLeft
                edges.Add(new Edge(right, bottom));
                break;
            case 12: // BottomRight + BottomLeft
                edges.Add(new Edge(right, left));
                break;
            case 13: // TopLeft + BottomRight + BottomLeft
                edges.Add(new Edge(top, right));
                break;
            case 14: // TopRight + BottomRight + BottomLeft
                edges.Add(new Edge(top, left));
                break;
            // Case 0 and 15 are skipped (all empty or all filled)
        }

        return edges;
    }

    private struct Edge
    {
        public Vector2 Start;
        public Vector2 End;

        public Edge(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }
    }
}
