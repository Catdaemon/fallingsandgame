using Microsoft.Xna.Framework;

namespace FallingSand;

public static class Constants
{
    public const int CHUNK_WIDTH = 128;
    public const int CHUNK_HEIGHT = 128;
    public const int OFF_SCREEN_CHUNK_UPDATE_RADIUS = 2;
    public const int OFF_SCREEN_CHUNK_UNLOAD_RADIUS = 12;
    public const int PIXELS_TO_METERS = 32;
    public const int INITIAL_CHUNK_POOL_SIZE = 500;
}

public static class Convert
{
    public static float PixelsToMeters(float pixels)
    {
        return pixels / Constants.PIXELS_TO_METERS;
    }

    public static Vector2 PixelsToMeters(Vector2 pixels)
    {
        return pixels / Constants.PIXELS_TO_METERS;
    }

    public static float MetersToPixels(float meters)
    {
        return meters * Constants.PIXELS_TO_METERS;
    }

    public static Vector2 MetersToPixels(Vector2 meters)
    {
        return meters * Constants.PIXELS_TO_METERS;
    }
}
