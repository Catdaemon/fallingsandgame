using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xilium.CefGlue;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Common.Events;
using Xilium.CefGlue.Common.Handlers;
using Xilium.CefGlue.Common.Helpers.Logger;
using Xilium.CefGlue.Common.Platform;
using Xilium.CefGlue.Common.Shared;

namespace MonoChrome;

public class BrowserManager : IDisposable
{
    private readonly BrowserOptions options;
    private readonly List<BrowserInstance> instances = [];
    private bool _disposed = false;
    private readonly GraphicsDevice graphicsDevice;
    private readonly GameWindow window;

    public BrowserManager(BrowserOptions options, GraphicsDevice graphicsDevice, GameWindow window)
    {
        this.options = options;
        this.graphicsDevice = graphicsDevice;
        this.window = window;

        if (!CefRuntime.IsInitialized)
        {
            var settings = new CefSettings()
            {
                RootCachePath = options.CachePath,
                WindowlessRenderingEnabled = true, // Enable this for best compatibility
                NoSandbox = true, // Enable this for easier debugging
                LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cef.log"),
                LogSeverity = CefLogSeverity.Verbose, // For debugging
                // MultiThreadedMessageLoop = false, // Important for integration with game loop

                BrowserSubprocessPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "CefGlueBrowserProcess"
                ),
                FrameworkDirPath = AppDomain.CurrentDomain.BaseDirectory, // This is where libcef.dylib is
                ResourcesDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Locale = "en",
                LocalesDirPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Resources",
                    "locales"
                ),
            };

            string localesDir = Path.Combine(settings.ResourcesDirPath, "locales");
            if (!Directory.Exists(localesDir))
            {
                Console.WriteLine($"Creating locales directory: {localesDir}");
                Directory.CreateDirectory(localesDir);
            }

            string enUsPakPath = Path.Combine(localesDir, "en-US.pak");
            if (!File.Exists(enUsPakPath))
            {
                string localePak = Path.Combine(settings.ResourcesDirPath, "locale.pak");
                if (File.Exists(localePak))
                {
                    Console.WriteLine($"Copying locale.pak to {enUsPakPath}");
                    File.Copy(localePak, enUsPakPath, true);
                }
                else
                {
                    Console.WriteLine("WARNING: No locale.pak found to copy!");
                }
            }

            try
            {
                CefRuntime.Load();
                CefRuntime.Initialize(new CefMainArgs([]), settings, null, IntPtr.Zero);
                CefRuntime.DoMessageLoopWork();
                // CefRuntimeLoader.Initialize(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize CefRuntime", ex);
            }
        }
    }

    public BrowserInstance CreateBrowser()
    {
        var renderer = new MonoGameBrowserRenderer(graphicsDevice);
        var texture = renderer.BrowserTexture;

        var client = new MonoGameBrowserClient(renderer);

        var windowInfo = CefWindowInfo.Create();
        windowInfo.SetAsChild(
            window.Handle,
            new CefRectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height)
        );
        // windowInfo.SetAsWindowless(IntPtr.Zero, true);

        var browserSettings = new CefBrowserSettings()
        {
            WindowlessFrameRate = 60,
            BackgroundColor = new CefColor(255, 255, 255, 255), // White background
        };

        var requestContext = CefRequestContext.GetGlobalContext();

        // Create the browser synchronously (you could also do this async)
        var browser = CefBrowserHost.CreateBrowserSync(
            windowInfo,
            client,
            browserSettings,
            "about:blank",
            null,
            requestContext
        );

        var instance = new BrowserInstance(
            browser,
            texture,
            graphicsDevice.Viewport.Width,
            graphicsDevice.Viewport.Height
        );
        instances.Add(instance);
        return instance;
    }

    public void Update()
    {
        CefRuntime.DoMessageLoopWork();
        foreach (var instance in instances)
        {
            instance.Update();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                CefRuntime.Shutdown();

                try
                {
                    var cacheDir = new DirectoryInfo(options.CachePath);
                    if (cacheDir.Exists)
                    {
                        cacheDir.Delete(true);
                    }
                }
                catch (Exception)
                {
                    // do nothing
                }
            }

            // Free unmanaged resources (if any) and set large fields to null

            _disposed = true;
        }
    }
}
