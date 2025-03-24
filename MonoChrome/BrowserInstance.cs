using System.ComponentModel;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xilium.CefGlue;

namespace MonoChrome;

public class BrowserInstance
{
    private readonly CefBrowser browser;
    private readonly Texture2D texture;
    private bool focussed = false;
    private int width;
    private int height;

    public BrowserInstance(CefBrowser browser, Texture2D texture, int width, int height)
    {
        this.browser = browser;
        this.texture = texture;
        this.width = width;
        this.height = height;
    }

    public void Focus()
    {
        focussed = true;
    }

    public void Blur()
    {
        focussed = false;
    }

    public void Update()
    {
        if (!focussed)
        {
            return;
        }

        // Send input events
        var host = browser.GetHost();
        var mouseState = Mouse.GetState();
        var keyboardState = Keyboard.GetState();

        // Mouse movement and clicks
        host.SendMouseMoveEvent(
            new CefMouseEvent(mouseState.X, mouseState.Y, CefEventFlags.None),
            false
        );
        host.SendMouseWheelEvent(
            new CefMouseEvent(mouseState.X, mouseState.Y, CefEventFlags.None),
            0,
            mouseState.ScrollWheelValue / 120
        );
        host.SendMouseClickEvent(
            new CefMouseEvent(mouseState.X, mouseState.Y, CefEventFlags.None),
            CefMouseButtonType.Left,
            false,
            1
        );
        host.SendMouseClickEvent(
            new CefMouseEvent(mouseState.X, mouseState.Y, CefEventFlags.None),
            CefMouseButtonType.Left,
            true,
            1
        );

        // Keyboard input
        foreach (var key in keyboardState.GetPressedKeys())
        {
            host.SendKeyEvent(
                new CefKeyEvent
                {
                    EventType = CefKeyEventType.RawKeyDown,
                    WindowsKeyCode = (int)key,
                    NativeKeyCode = (int)key,
                    IsSystemKey = false,
                    Modifiers = CefEventFlags.None,
                }
            );

            host.SendKeyEvent(
                new CefKeyEvent
                {
                    EventType = CefKeyEventType.KeyUp,
                    WindowsKeyCode = (int)key,
                    NativeKeyCode = (int)key,
                    IsSystemKey = false,
                    Modifiers = CefEventFlags.None,
                }
            );
        }
    }

    public Texture2D GetTexture()
    {
        return texture;
    }

    public (int, int) GetDimensions()
    {
        return (width, height);
    }
}
