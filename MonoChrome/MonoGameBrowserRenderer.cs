using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using Xilium.CefGlue;

namespace MonoChrome;

public class MonoGameBrowserRenderer : CefRenderHandler
{
    private readonly Texture2D browserTexture;
    private readonly byte[] pixelData;
    private readonly int width;
    private readonly int height;

    public MonoGameBrowserRenderer(GraphicsDevice graphicsDevice)
    {
        width = graphicsDevice.Viewport.Width;
        height = graphicsDevice.Viewport.Height;

        browserTexture = new Texture2D(graphicsDevice, width, height);
        pixelData = new byte[width * height * 4]; // RGBA
    }

    public Texture2D BrowserTexture => browserTexture;

    // Implementation of CefRenderHandler abstract methods
    protected override bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
    {
        rect = new CefRectangle(0, 0, width, height);
        return true;
    }

    protected override void GetViewRect(CefBrowser browser, out CefRectangle rect)
    {
        rect = new CefRectangle(0, 0, width, height);
    }

    protected override void OnPaint(
        CefBrowser browser,
        CefPaintElementType type,
        CefRectangle[] dirtyRects,
        IntPtr buffer,
        int width,
        int height
    )
    {
        // Copy pixel data from CEF
        Marshal.Copy(buffer, pixelData, 0, width * height * 4);

        // Update MonoGame texture - note that CEF uses BGRA format while XNA/MonoGame uses RGBA
        for (int i = 0; i < pixelData.Length; i += 4)
        {
            // Swap B and R channels
            byte temp = pixelData[i];
            pixelData[i] = pixelData[i + 2];
            pixelData[i + 2] = temp;
        }

        browserTexture.SetData(pixelData);
    }

    protected override void OnScrollOffsetChanged(CefBrowser browser, double x, double y)
    {
        // Nothing to do here for basic implementation
    }

    protected override void OnAcceleratedPaint(
        CefBrowser browser,
        CefPaintElementType type,
        CefRectangle[] dirtyRects,
        IntPtr sharedHandle
    )
    {
        // Not supporting accelerated paint in this implementation
    }

    protected override CefAccessibilityHandler GetAccessibilityHandler()
    {
        return null;
    }

    protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
    {
        // Popup handling not implemented
    }

    protected override void OnImeCompositionRangeChanged(
        CefBrowser browser,
        CefRange selectedRange,
        CefRectangle[] characterBounds
    )
    {
        // IME Composition handling not implemented
    }

    protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
    {
        // Set some reasonable defaults
        screenInfo.DeviceScaleFactor = 1.0f;
        return true;
    }
}
