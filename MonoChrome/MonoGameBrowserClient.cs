using Xilium.CefGlue;

namespace MonoChrome;

public class MonoGameBrowserClient : CefClient
{
    private readonly MonoGameBrowserRenderer _renderer;

    public MonoGameBrowserClient(MonoGameBrowserRenderer renderer)
    {
        _renderer = renderer;
    }

    protected override CefRenderHandler GetRenderHandler()
    {
        return _renderer;
    }

    // Add more handlers as needed (LoadHandler, DisplayHandler, etc.)
}
