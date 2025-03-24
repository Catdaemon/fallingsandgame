namespace MonoChrome;

public record BrowserOptions
{
    public string CachePath { get; init; } =
        Path.Combine(
            Path.GetTempPath(),
            "MonoChrome_" + Guid.NewGuid().ToString().Replace("-", null)
        );
}
