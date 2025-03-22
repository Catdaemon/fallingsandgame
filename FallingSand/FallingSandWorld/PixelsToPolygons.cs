using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Common;

namespace FallingSandWorld;

class PixelsToPolygons
{
    // Debug counters
    public static int TotalPolygons { get; private set; }
    public static int RejectedPolygons { get; private set; }
    public static int BoxPolygons { get; private set; }
    public static int MergedPolygons { get; private set; }

    private readonly List<CellInfo> _cellInfoPool;
    private readonly List<Rectangle> _rectanglePool;
    private readonly List<Rectangle> _tempRectanglePool;
    private readonly List<Vertices> _verticesPool;

    // Thread local instance
    [ThreadStatic]
    private static PixelsToPolygons _threadInstance;

    // Factory method to get thread-local instance
    public static PixelsToPolygons GetInstance()
    {
        if (_threadInstance == null)
        {
            _threadInstance = new PixelsToPolygons();
        }
        return _threadInstance;
    }

    // Enforce factory usage
    private PixelsToPolygons()
    {
        // Initialize pools with appropriate capacities
        _cellInfoPool = new List<CellInfo>(1024);
        _rectanglePool = new List<Rectangle>(256);
        _tempRectanglePool = new List<Rectangle>(256);
        _verticesPool = new List<Vertices>(256);
    }

    private struct Rectangle(int x, int y, int width, int height)
    {
        public int X = x,
            Y = y,
            Width = width,
            Height = height;

        // Check if this rectangle shares an edge with another
        public bool SharesEdgeWith(Rectangle other)
        {
            // Check if rectangles share a horizontal edge
            bool sharesHorizontalEdge =
                (Y == other.Y + other.Height || other.Y == Y + Height)
                && !(X >= other.X + other.Width || other.X >= X + Width);

            // Check if rectangles share a vertical edge
            bool sharesVerticalEdge =
                (X == other.X + other.Width || other.X == X + Width)
                && !(Y >= other.Y + other.Height || other.Y >= Y + Height);

            return sharesHorizontalEdge || sharesVerticalEdge;
        }

        // Try to merge with another rectangle if they share an edge
        public bool TryMerge(Rectangle other, out Rectangle merged)
        {
            merged = this;

            // Calculate boundaries
            int x1 = Math.Min(X, other.X);
            int y1 = Math.Min(Y, other.Y);
            int x2 = Math.Max(X + Width, other.X + other.Width);
            int y2 = Math.Max(Y + Height, other.Y + other.Height);
            int mergedWidth = x2 - x1;
            int mergedHeight = y2 - y1;

            // Check if merging would create a simple rectangle
            int area1 = Width * Height;
            int area2 = other.Width * other.Height;
            int mergedArea = mergedWidth * mergedHeight;

            if (mergedArea == area1 + area2 && SharesEdgeWith(other))
            {
                merged = new Rectangle(x1, y1, mergedWidth, mergedHeight);
                return true;
            }

            return false;
        }
    }

    // Struct to avoid allocating tuples
    private struct CellInfo(int x, int y, int priority) : IComparable<CellInfo>
    {
        public int X = x;
        public int Y = y;
        public int Priority = priority;

        public int CompareTo(CellInfo other)
        {
            // Sort by priority in descending order
            return other.Priority.CompareTo(Priority);
        }
    }

    // Changed to instance method
    public IEnumerable<Vertices> Generate(ref bool[,] binaryGrid, int width, int height)
    {
        // Reset counters
        TotalPolygons = 0;
        RejectedPolygons = 0;
        BoxPolygons = 0;
        MergedPolygons = 0;

        // Clear object pools for reuse
        _cellInfoPool.Clear();
        _rectanglePool.Clear();
        _tempRectanglePool.Clear();
        _verticesPool.Clear();

        // Find all maximal rectangles
        FindMaximalRectangles(binaryGrid, width, height, _rectanglePool);

        // Merge rectangles where possible to reduce the count
        MergeRectangles(_rectanglePool, _tempRectanglePool);

        // Convert rectangles to Vertices for the physics engine
        foreach (var rect in _rectanglePool)
        {
            Vertices polygon = CreateRectangleVertices(rect.X, rect.Y, rect.Width, rect.Height);
            _verticesPool.Add(polygon);
            BoxPolygons++;
            TotalPolygons++;
        }

        return _verticesPool;
    }

    /// <summary>
    /// Find maximal rectangles in the binary grid using a more efficient algorithm
    /// </summary>
    private void FindMaximalRectangles(
        bool[,] grid,
        int width,
        int height,
        List<Rectangle> rectangles
    )
    {
        // Reuse a single processed grid across calls
        bool[,] processed = new bool[width, height];

        // Reset the processed grid
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                processed[x, y] = false;
            }
        }

        // Calculate a heuristic value for each solid cell (potential area)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!grid[x, y])
                    continue;

                // Calculate how far right and down we can go (quick estimate)
                int maxRight = 0;
                int maxDown = 0;

                while (x + maxRight < width && grid[x + maxRight, y])
                    maxRight++;

                while (y + maxDown < height && grid[x, y + maxDown])
                    maxDown++;

                // Use area as priority (higher = process first)
                int priority = maxRight * maxDown;
                _cellInfoPool.Add(new CellInfo(x, y, priority));
            }
        }

        // Sort cells by priority (descending)
        _cellInfoPool.Sort();

        // Process cells in priority order
        foreach (var cell in _cellInfoPool)
        {
            int x = cell.X;
            int y = cell.Y;

            if (processed[x, y] || !grid[x, y])
                continue;

            // Find maximum rectangle from this cell
            int maxWidth = 0;
            int maxHeight = 0;

            // Expand right as far as possible
            while (x + maxWidth < width && grid[x + maxWidth, y] && !processed[x + maxWidth, y])
                maxWidth++;

            // Try different heights and find the one that maximizes area
            int bestHeight = 1;
            int bestArea = maxWidth; // Initial area with height 1

            for (int h = 1; h < height - y; h++)
            {
                // Check if we can expand to this height
                bool canExpandHeight = true;
                for (int dx = 0; dx < maxWidth; dx++)
                {
                    if (y + h >= height || !grid[x + dx, y + h] || processed[x + dx, y + h])
                    {
                        canExpandHeight = false;
                        break;
                    }
                }

                if (!canExpandHeight)
                    break;

                int area = maxWidth * (h + 1);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestHeight = h + 1;
                }
            }

            // We now have the best rectangle starting at (x,y)
            maxHeight = bestHeight;

            // Mark cells as processed
            for (int dy = 0; dy < maxHeight; dy++)
            {
                for (int dx = 0; dx < maxWidth; dx++)
                {
                    processed[x + dx, y + dy] = true;
                }
            }

            // Add the rectangle
            rectangles.Add(new Rectangle(x, y, maxWidth, maxHeight));
        }
    }

    /// <summary>
    /// Try to merge rectangles to reduce the total count
    /// </summary>
    private void MergeRectangles(List<Rectangle> rectangles, List<Rectangle> tempRectangles)
    {
        bool merged;

        // Keep trying to merge until no more merges are possible
        do
        {
            merged = false;

            for (int i = 0; i < rectangles.Count; i++)
            {
                for (int j = i + 1; j < rectangles.Count; j++)
                {
                    if (rectangles[i].TryMerge(rectangles[j], out Rectangle mergedRect))
                    {
                        // Store the merged rectangle
                        tempRectangles.Add(mergedRect);

                        // Add all non-merged rectangles to the temp list
                        for (int k = 0; k < rectangles.Count; k++)
                        {
                            if (k != i && k != j)
                            {
                                tempRectangles.Add(rectangles[k]);
                            }
                        }

                        // Update counters
                        MergedPolygons++;

                        // Swap the lists
                        rectangles.Clear();
                        foreach (var rect in tempRectangles)
                        {
                            rectangles.Add(rect);
                        }
                        tempRectangles.Clear();

                        // Flag that we merged something
                        merged = true;
                        break;
                    }
                }

                if (merged)
                    break;
            }
        } while (merged);
    }

    /// <summary>
    /// Creates a rectangle vertices with the given dimensions (reuses vertices object)
    /// </summary>
    private Vertices CreateRectangleVertices(int x, int y, int width, int height)
    {
        // Create a new Vertices object with exact capacity to avoid resizing
        Vertices rect =
        [
            // Create rectangle vertices in counter-clockwise order
            new Vector2(x, y),
            new Vector2(x + width, y),
            new Vector2(x + width, y + height),
            new Vector2(x, y + height),
        ];

        return rect;
    }
}
