using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using fallingsand.nosync;

namespace FallingSandWorld;

class FallingSandWorldChunk
{
    public int WorldX;
    public int WorldY;

    public readonly FallingSandWorld parentWorld;
    public readonly FallingSandPixel[,] pixels;
    public ConcurrentBag<LocalPosition> pixelsToDraw = [];
    public bool isAwake = true;
    private static Random random = new();

    public FallingSandWorldChunk(FallingSandWorld parentWorld, int worldX, int worldY)
    {
        this.parentWorld = parentWorld;
        WorldX = worldX;
        WorldY = worldY;
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

        var leftFrame = parentWorld.currentFrameId % 2 == 0;
        var topFrame = parentWorld.currentFrameId % 4 < 2; // Alternate between top-down and bottom-up

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
                        && pixels[x, y].LastUpdatedFrameId < parentWorld.currentFrameId
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

    public void SetPixel(
        LocalPosition localPosition,
        FallingSandPixelData newPixelData,
        float velocity = 1
    )
    {
        if (
            localPosition.X < 0
            || localPosition.X >= Constants.CHUNK_WIDTH
            || localPosition.Y < 0
            || localPosition.Y >= Constants.CHUNK_HEIGHT
        )
        {
            // Console.WriteLine(
            //     $"Tried to set pixel outside of chunk: {localPosition.X}, {localPosition.Y}"
            // );
            return;
        }
        pixels[localPosition.X, localPosition.Y].Set(newPixelData, velocity);
        AddPixelToDrawQueue(localPosition);
        Wake();
    }

    public void EmptyPixel(LocalPosition localPosition)
    {
        if (
            localPosition.X < 0
            || localPosition.X >= Constants.CHUNK_WIDTH
            || localPosition.Y < 0
            || localPosition.Y >= Constants.CHUNK_HEIGHT
        )
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

    public void ClearDrawQueue()
    {
        pixelsToDraw.Clear();
    }

    // Returns the world position of a pixel in this chunk
    public WorldPosition LocalToWorldPosition(LocalPosition localPosition)
    {
        return new WorldPosition(
            localPosition.X + WorldX * Constants.CHUNK_WIDTH,
            localPosition.Y + WorldY * Constants.CHUNK_HEIGHT
        );
    }

    public LocalPosition WorldToLocalPosition(WorldPosition worldPos)
    {
        return new LocalPosition(
            worldPos.X - (WorldX * Constants.CHUNK_WIDTH),
            worldPos.Y - (WorldY * Constants.CHUNK_HEIGHT)
        );
    }
}
