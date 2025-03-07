using System.Collections.Generic;
using FallingSand;
using FallingSandWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.FallingSandRenderer;

class GameChunk
{
    public WorldPosition worldOrigin;
    private readonly FallingSandWorld.FallingSandWorld sandWorld;
    public FallingSandWorldChunk sandChunk;
    public bool hasGeneratedMap = false;
    public RenderTarget2D renderTarget;
    public SpriteBatch spriteBatch;
    private readonly GraphicsDevice graphicsDevice;
    private readonly Texture2D pixelTexture;
    private readonly FallingSandWorldGenerator generator;

    public GameChunk(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        WorldPosition worldOrigin,
        FallingSandWorld.FallingSandWorld world,
        FallingSandWorldGenerator generator
    )
    {
        this.worldOrigin = worldOrigin;
        this.sandWorld = world;
        this.generator = generator;
        this.graphicsDevice = graphicsDevice;
        this.spriteBatch = spriteBatch;

        renderTarget = new(
            graphicsDevice,
            Constants.CHUNK_WIDTH,
            Constants.CHUNK_HEIGHT,
            false,
            graphicsDevice.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents
        );

        pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        pixelTexture.SetData([Microsoft.Xna.Framework.Color.White]);
    }

    public void Generate()
    {
        if (hasGeneratedMap)
        {
            return;
        }

        var batch = generator.GenerateBatch(
            new WorldPosition(worldOrigin.X, worldOrigin.Y),
            new WorldPosition(
                worldOrigin.X + Constants.CHUNK_WIDTH,
                worldOrigin.Y + Constants.CHUNK_HEIGHT
            )
        );

        // Create a local buffer that belongs only to this thread
        FallingSandPixelData[] pixelBuffer = new FallingSandPixelData[
            Constants.CHUNK_WIDTH * Constants.CHUNK_HEIGHT
        ];

        for (int y = 0; y < Constants.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.CHUNK_WIDTH; x++)
            {
                var pixel = batch[y * Constants.CHUNK_WIDTH + x];

                pixelBuffer[y * Constants.CHUNK_WIDTH + x] = new FallingSandPixelData
                {
                    Material = pixel.Material,
                    Color = pixel.Color,
                };
            }
        }

        sandWorld.SetPixelBatch(worldOrigin, pixelBuffer, Constants.CHUNK_WIDTH);

        sandChunk = sandWorld.GetOrCreateChunkFromWorldPosition(worldOrigin);

        sandChunk.Wake();

        hasGeneratedMap = true;
    }

    public LocalPosition WorldToLocalPosition(WorldPosition worldPosition)
    {
        return new LocalPosition(worldPosition.X - worldOrigin.X, worldPosition.Y - worldOrigin.Y);
    }

    public WorldPosition LocalToWorldPosition(LocalPosition localPosition)
    {
        return new WorldPosition(localPosition.X + worldOrigin.X, localPosition.Y + worldOrigin.Y);
    }

    public FallingSandPixel GetPixel(LocalPosition localPosition)
    {
        if (!hasGeneratedMap)
        {
            return null;
        }
        return sandChunk.GetPixel(localPosition);
    }

    // Draw to the render target
    public void Draw()
    {
        if (!hasGeneratedMap)
        {
            return;
        }

        // Pull the pixels out of the queue into this thread to reduce contention
        var pixelsToRender = new List<LocalPosition>();

        // Pull 1000 pixels at a time to reduce fps drops
        while (sandChunk.pixelsToDraw.TryTake(out var position))
        {
            pixelsToRender.Add(position);
        }

        if (pixelsToRender.Count > 0)
        {
            graphicsDevice.SetRenderTarget(renderTarget);
            spriteBatch.Begin();

            foreach (var position in pixelsToRender)
            {
                // Get pixel data
                var pixelData = sandChunk.GetPixel(position).data;

                // Draw to render target in the correct position
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle(position.X, position.Y, 1, 1),
                    new Microsoft.Xna.Framework.Color(
                        pixelData.Color.R,
                        pixelData.Color.G,
                        pixelData.Color.B
                    )
                );
            }
            spriteBatch.End();
            graphicsDevice.SetRenderTarget(null);
        }

        // Draw outline
        // var outlineColor = new Microsoft.Xna.Framework.Color(255, 0, 0);
        // if (sandChunk.isAwake)
        // {
        //     outlineColor = new Microsoft.Xna.Framework.Color(0, 255, 0);
        // }

        // spriteBatch.Begin();
        // spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, Constants.CHUNK_WIDTH, 1), outlineColor);
        // spriteBatch.Draw(
        //     pixelTexture,
        //     new Rectangle(0, 0, 1, Constants.CHUNK_HEIGHT),
        //     outlineColor
        // );
        // spriteBatch.Draw(
        //     pixelTexture,
        //     new Rectangle(Constants.CHUNK_WIDTH - 1, 0, 1, Constants.CHUNK_HEIGHT),
        //     outlineColor
        // );
        // spriteBatch.Draw(
        //     pixelTexture,
        //     new Rectangle(0, Constants.CHUNK_HEIGHT - 1, Constants.CHUNK_WIDTH, 1),
        //     outlineColor
        // );
        // spriteBatch.End();
    }

    public void Update()
    {
        // Update entities etc. here
    }

    public void Unload()
    {
        renderTarget.Dispose();
        pixelTexture.Dispose();
    }
}
