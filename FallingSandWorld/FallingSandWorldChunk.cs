using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Arch.Core;
using FallingSand;
using FallingSand.Entity.Component;

namespace FallingSandWorld;

class FallingSandWorldChunk
{
    public ChunkPosition ChunkPos;

    public readonly FallingSandWorld parentWorld;
    public readonly FallingSandPixel[,] pixels;
    public ConcurrentBag<LocalPosition> pixelsToDraw = [];
    public bool isAwake = true;

    public FallingSandWorldChunk(FallingSandWorld parentWorld, ChunkPosition chunkPos)
    {
        this.parentWorld = parentWorld;
        ChunkPos = chunkPos;
        pixels = new FallingSandPixel[Constants.CHUNK_WIDTH, Constants.CHUNK_HEIGHT];
        InitializePixels();
    }

    private void InitializePixels()
    {
        for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
        {
            for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
            {
                pixels[x, y] = new FallingSandPixel(this, Material.Empty, new Color(0, 0, 0));
            }
        }
    }

    public void Reset(ChunkPosition newPosition)
    {
        ChunkPos = newPosition;
        isAwake = true;
        // ClearDrawQueue();
        // Clear all pixels
        for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
        {
            for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
            {
                pixels[x, y].Empty();
            }
        }
    }

    public void Wake()
    {
        if (!isAwake)
        {
            // Console.WriteLine($"Waking chunk at {WorldX}, {WorldY}");
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

        // Update each pixel
        // Invert the direction each frame to avoid bias

        // Also invert Y direction each frame
        //     for (
        //         int y = leftFrame ? 0 : Constants.CHUNK_HEIGHT - 1;
        //         leftFrame ? y < Constants.CHUNK_HEIGHT : y >= 0;
        //         y += leftFrame ? 1 : -1
        //     )
        //     {
        //         {


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
                if (pixels[x, y].IsAwake)
                {
                    // Update each pixel
                    var pixel = pixels[x, y];
                    var pixelPosition = new LocalPosition(x, y);
                    pixel.Update(this, pixelPosition);

                    if (pixel.IsAwake)
                    {
                        anyPixelsUpdated = true;
                    }
                }
            }
        }

        // Perform a second pass over awakened pixels
        // This helps fill gaps by allowing immediate response to movements
        if (anyPixelsUpdated)
        {
            for (
                int y = !topFrame ? 0 : Constants.CHUNK_HEIGHT - 1; // Opposite direction of first pass
                !topFrame ? y < Constants.CHUNK_HEIGHT : y >= 0;
                y += !topFrame ? 1 : -1
            )
            {
                for (
                    int x = !leftFrame ? 0 : Constants.CHUNK_WIDTH - 1; // Opposite direction of first pass
                    !leftFrame ? x < Constants.CHUNK_WIDTH : x >= 0;
                    x += !leftFrame ? 1 : -1
                )
                {
                    if (
                        pixels[x, y].IsAwake
                        && pixels[x, y].LastUpdatedFrameId < parentWorld.CurrentFrameId
                    )
                    {
                        var pixel = pixels[x, y];
                        var pixelPosition = new LocalPosition(x, y);
                        pixel.Update(this, pixelPosition);
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
            return new FallingSandPixel(this, Material.Empty, new Color(0, 0, 0));
        }

        return pixels[position.X, position.Y];
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

        pixels[localPosition.X, localPosition.Y].Set(newPixelData, velocity);

        if (!isBulkOperation)
        {
            AddPixelToDrawQueue(localPosition);
            Wake();
        }
    }

    public void SetPixelBatch(FallingSandPixelData[] pixels)
    {
        Sleep();
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                SetPixel(new LocalPosition(x, y), pixels[y * Constants.CHUNK_WIDTH + x], 1, true);
            }
        }
        Wake();
        MarkEntireChunkForRedraw();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EmptyPixel(LocalPosition localPosition)
    {
        if (!IsInBounds(localPosition))
        {
            // Console.WriteLine(
            //     $"Tried to empty pixel outside of chunk: {localPosition.X}, {localPosition.Y}"
            // );
            return;
        }
        pixels[localPosition.X, localPosition.Y].Empty();
        AddPixelToDrawQueue(localPosition);
        Wake();
    }

    public void AddPixelToDrawQueue(LocalPosition position)
    {
        pixelsToDraw.Add(position);
    }

    public void MarkEntireChunkForRedraw()
    {
        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                AddPixelToDrawQueue(new LocalPosition(x, y));
            }
        }
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
}
