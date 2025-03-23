using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Arch.Core;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;

namespace FallingSandWorld;

class FallingSandWorldChunk
{
    public ChunkPosition ChunkPos;

    public readonly FallingSandWorld parentWorld;
    private readonly FallingSandPixel[] pixelsArray;
    private readonly Memory2D<FallingSandPixel> pixelsMemory;
    public ConcurrentBag<LocalPosition> pixelsToDraw = [];
    public bool isAwake = true;

    public FallingSandWorldChunk(FallingSandWorld parentWorld, ChunkPosition chunkPos)
    {
        this.parentWorld = parentWorld;
        ChunkPos = chunkPos;
        pixelsArray = new FallingSandPixel[Constants.CHUNK_WIDTH * Constants.CHUNK_HEIGHT];
        pixelsMemory = new Memory2D<FallingSandPixel>(
            pixelsArray,
            Constants.CHUNK_HEIGHT,
            Constants.CHUNK_WIDTH
        );
        InitializePixels();
    }

    private void InitializePixels()
    {
        // Access the pixels as a Span2D only for this method
        Span2D<FallingSandPixel> pixels = pixelsMemory.Span;

        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                pixels[y, x] = new FallingSandPixel(this, Material.Empty, Color.Transparent);
            }
        }
    }

    public void Reset(ChunkPosition newPosition)
    {
        ChunkPos = newPosition;
        isAwake = true;
    }

    public void Wake()
    {
        if (!isAwake)
        {
            isAwake = true;
        }
    }

    public void Sleep()
    {
        isAwake = false;
    }

    public void Update()
    {
        if (!isAwake)
            return;

        var leftFrame = parentWorld.CurrentFrameId % 2 == 0;
        var topFrame = parentWorld.CurrentFrameId % 4 < 2; // Alternate between top-down and bottom-up

        bool anyPixelsUpdated = false;

        // Get a span for this method's scope
        Span2D<FallingSandPixel> pixels = pixelsMemory.Span;

        for (
            int y = topFrame ? 0 : Constants.CHUNK_HEIGHT - 1;
            topFrame ? y < Constants.CHUNK_HEIGHT : y >= 0;
            y += topFrame ? 1 : -1
        )
        {
            for (
                int x = leftFrame ? 0 : Constants.CHUNK_WIDTH - 1;
                leftFrame ? x < Constants.CHUNK_WIDTH : x >= 0;
                x += leftFrame ? 1 : -1
            )
            {
                var pixel = pixels[y, x];
                if (pixel.IsAwake)
                {
                    // Update each pixel
                    var pixelPosition = new LocalPosition(x, y);
                    pixel.Update(this, pixelPosition);

                    if (pixel.IsAwake)
                    {
                        anyPixelsUpdated = true;
                    }
                }
            }
        }

        if (!anyPixelsUpdated)
        {
            Sleep();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FallingSandPixel GetPixel(LocalPosition position)
    {
        if (
            position.X < 0
            || position.X >= Constants.CHUNK_WIDTH
            || position.Y < 0
            || position.Y >= Constants.CHUNK_HEIGHT
        )
        {
            // Return an empty pixel if the requested position is outside of the chunk
            return new FallingSandPixel(this, Material.Empty, Color.Transparent);
        }

        return pixelsMemory.Span[position.Y, position.X];
    }

    public FallingSandPixel GetPixel(int x, int y)
    {
        return GetPixel(new LocalPosition(x, y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInBounds(LocalPosition pos) =>
        pos.X >= 0 && pos.X < Constants.CHUNK_WIDTH && pos.Y >= 0 && pos.Y < Constants.CHUNK_HEIGHT;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(
        LocalPosition localPosition,
        FallingSandPixelData newPixelData,
        float velocity = 1,
        bool isBulkOperation = false
    )
    {
        if (!IsInBounds(localPosition))
        {
            return;
        }

        pixelsMemory.Span[localPosition.Y, localPosition.X].Set(newPixelData, velocity);

        if (!isBulkOperation)
        {
            AddPixelToDrawQueue(localPosition);
            Wake();
            FallingSandPixel.WakeAdjacentPixels(this, localPosition);
        }
    }

    public void SetPixelBatch(FallingSandPixelData[] pixelData, int startIndex)
    {
        // Use Span to avoid bounds checking in the loop
        ReadOnlySpan<FallingSandPixelData> dataSpan = pixelData;
        Span2D<FallingSandPixel> pixels = pixelsMemory.Span;

        // Calculate start coordinates
        int startX = startIndex % Constants.CHUNK_WIDTH;
        int startY = startIndex / Constants.CHUNK_WIDTH;

        for (int i = 0; i < pixelData.Length; i++)
        {
            int x = (startX + i) % Constants.CHUNK_WIDTH;
            int y = startY + ((startX + i) / Constants.CHUNK_WIDTH);

            if (y < Constants.CHUNK_HEIGHT)
            {
                var pixel = pixels[y, x];
                pixel.Data = dataSpan[i];
                pixel.ComputeProperties();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmptyPixel(LocalPosition localPosition)
    {
        if (!IsInBounds(localPosition))
        {
            return;
        }
        pixelsMemory.Span[localPosition.Y, localPosition.X].Empty();
        AddPixelToDrawQueue(localPosition);
        FallingSandPixel.WakeAdjacentPixels(this, localPosition);
        Wake();
    }

    public void AddPixelToDrawQueue(LocalPosition position)
    {
        pixelsToDraw.Add(position);
    }

    public void MarkEntireChunkForRedraw()
    {
        // Instead of adding thousands of individual pixels
        pixelsToDraw.Clear();
        pixelsToDraw.Add(new LocalPosition(-1, -1)); // Special marker to indicate "redraw all"
    }

    public void ClearDrawQueue()
    {
        pixelsToDraw.Clear();
    }

    // Returns the world position of a pixel in this chunk
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorldPosition LocalToWorldPosition(LocalPosition localPosition)
    {
        return new WorldPosition(
            localPosition.X + ChunkPos.X * Constants.CHUNK_WIDTH,
            localPosition.Y + ChunkPos.Y * Constants.CHUNK_HEIGHT
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LocalPosition WorldToLocalPosition(WorldPosition worldPos)
    {
        return new LocalPosition(
            worldPos.X - (ChunkPos.X * Constants.CHUNK_WIDTH),
            worldPos.Y - (ChunkPos.Y * Constants.CHUNK_HEIGHT)
        );
    }

    /// <summary>
    /// Gets a pixel using the 1D array indexing pattern for backward compatibility
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FallingSandPixel GetPixelByIndex(int index)
    {
        int y = index / Constants.CHUNK_WIDTH;
        int x = index % Constants.CHUNK_WIDTH;
        return pixelsMemory.Span[y, x];
    }

    /// <summary>
    /// Gets all pixels in the chunk
    /// </summary>
    /// <returns>Span of pixels</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span2D<FallingSandPixel> GetPixels()
    {
        return pixelsMemory.Span;
    }
}
