using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand;

static class Camera
{
    // Store camera position as floats internally for smoother movement with zoom
    private static Vector2 PositionF = new Vector2(0, 0);
    private static Vector2 MouseScreenPosition = new Vector2(0, 0);
    private static WorldPosition Size = new WorldPosition(0, 0);
    private static float Zoom = 1.0f;

    // Store current GameTime for animation purposes
    public static GameTime GameTime { get; private set; }

    // Returns true if the given position and size are visible on the screen
    // Taking into account the camera position, zoom and size
    public static bool IsVisible(WorldPosition position, WorldPosition size)
    {
        var screenPos = WorldToScreenPosition(position);
        var scaledSize = new Vector2(size.X * Zoom, size.Y * Zoom);

        return screenPos.X + scaledSize.X > 0
            && screenPos.X < Size.X
            && screenPos.Y + scaledSize.Y > 0
            && screenPos.Y < Size.Y;
    }

    public static Vector2 WorldToScreenPosition(WorldPosition worldPosition)
    {
        // Convert world coordinates to screen coordinates
        // For a worldPos to be at the center of the screen when it's at camera position:
        Vector2 screenCenter = new Vector2(Size.X / 2, Size.Y / 2);
        Vector2 worldPos = new Vector2(worldPosition.X, worldPosition.Y);

        // The formula: screen_pos = (world_pos - camera_pos) * zoom + screen_center
        return (worldPos - PositionF) * Zoom + screenCenter;
    }

    public static WorldPosition ScreenToWorldPosition(Vector2 screenPosition)
    {
        // Convert screen coordinates to world coordinates
        // The inverse of WorldToScreenPosition
        Vector2 screenCenter = new Vector2(Size.X / 2, Size.Y / 2);

        // The formula: world_pos = (screen_pos - screen_center) / zoom + camera_pos
        Vector2 worldPosF = (screenPosition - screenCenter) / Zoom + PositionF;

        return new WorldPosition((int)Math.Floor(worldPosF.X), (int)Math.Floor(worldPosF.Y));
    }

    // Overload for Position input
    public static WorldPosition ScreenToWorldPosition(LocalPosition screenPosition)
    {
        return ScreenToWorldPosition(new Vector2(screenPosition.X, screenPosition.Y));
    }

    public static void SetPosition(WorldPosition position)
    {
        PositionF = new Vector2(position.X, position.Y);
    }

    public static void SetPosition(int x, int y)
    {
        PositionF = new Vector2(x, y);
    }

    public static void SetPosition(float x, float y)
    {
        PositionF = new Vector2(x, y);
    }

    public static void SetSize(WorldPosition size)
    {
        Size = size;
    }

    public static WorldPosition GetPosition()
    {
        return new WorldPosition((int)PositionF.X, (int)PositionF.Y);
    }

    public static Vector2 GetPositionF()
    {
        return PositionF;
    }

    public static WorldPosition GetSize()
    {
        return Size;
    }

    public static float GetZoom()
    {
        return Zoom;
    }

    public static void SetZoom(float zoom)
    {
        Zoom = zoom;
    }

    public static void Update(GameTime gameTime)
    {
        GameTime = gameTime;
    }

    public static void InitializeCamera(int width, int height, Viewport viewport)
    {
        Size = new WorldPosition(viewport.Width, viewport.Height);
        SetPosition(width / 2, height / 2);
    }

    /// <summary>
    /// Gets the top-left corner position of the currently visible area in world coordinates
    /// </summary>
    /// <returns>World position of the top-left corner of the visible area</returns>
    public static WorldPosition GetVisibleAreaStart()
    {
        return ScreenToWorldPosition(Vector2.Zero);
    }

    /// <summary>
    /// Gets the bottom-right corner position of the currently visible area in world coordinates
    /// </summary>
    /// <returns>World position of the bottom-right corner of the visible area</returns>
    public static WorldPosition GetVisibleAreaEnd()
    {
        return ScreenToWorldPosition(new Vector2(Size.X, Size.Y));
    }

    /// <summary>
    /// Gets both the start and end positions of the visible area in world coordinates
    /// </summary>
    /// <returns>A tuple containing (start, end) positions of the visible area</returns>
    public static (WorldPosition start, WorldPosition end) GetVisibleArea()
    {
        WorldPosition start = GetVisibleAreaStart();
        WorldPosition end = GetVisibleAreaEnd();
        return (start, end);
    }

    /// <summary>
    /// Gets the dimensions of the visible area in world coordinates
    /// </summary>
    /// <returns>The width and height of the visible area in world units</returns>
    public static WorldPosition GetVisibleAreaSize()
    {
        WorldPosition start = GetVisibleAreaStart();
        WorldPosition end = GetVisibleAreaEnd();
        return new WorldPosition(end.X - start.X, end.Y - start.Y);
    }

    public static Matrix GetTransformMatrix()
    {
        // This needs to match our WorldToScreenPosition logic
        Vector2 screenCenter = new Vector2(Size.X / 2, Size.Y / 2);

        // The transformation happens in this order:
        // 1. Translate by -cameraPos (to make camera pos the origin)
        // 2. Scale by zoom
        // 3. Translate by screenCenter (to center the view)
        return Matrix.CreateTranslation(new Vector3(-PositionF, 0))
            * Matrix.CreateScale(new Vector3(Zoom, Zoom, 1))
            * Matrix.CreateTranslation(new Vector3(screenCenter, 0));
    }

    public static Matrix GetViewMatrix()
    {
        // Convert camera position to meters for physics world
        // Don't negate X this time
        Vector2 cameraPositionInMeters = new Vector2(
            PositionF.X / Constants.PIXELS_TO_METERS, // No negation
            PositionF.Y / Constants.PIXELS_TO_METERS
        );

        // Look at the camera position from a standard distance away
        Vector3 cameraPos = new Vector3(cameraPositionInMeters, 10);
        Vector3 targetPos = new Vector3(cameraPositionInMeters, 0);

        // Create the view matrix with Y axis flipped (keeping Vector3.Down)
        return Matrix.CreateLookAt(cameraPos, targetPos, Vector3.Down);
    }

    public static Matrix GetProjectionMatrix()
    {
        // Calculate visible area width/height in meters
        float visibleWidth = Size.X / (Zoom * Constants.PIXELS_TO_METERS);
        float visibleHeight = Size.Y / (Zoom * Constants.PIXELS_TO_METERS);

        // Create basic orthographic projection
        Matrix projection = Matrix.CreateOrthographic(
            visibleWidth,
            visibleHeight,
            0.1f, // Near plane
            1000f // Far plane
        );

        // Apply horizontal mirroring to the projection matrix
        Matrix mirrorX = Matrix.CreateScale(-1, 1, 1);

        // Return mirrored projection
        return mirrorX * projection;
    }

    public static void SetMousePosition(Vector2 newPosition)
    {
        MouseScreenPosition = newPosition;
    }

    public static WorldPosition GetMouseWorldPosition()
    {
        // Directly convert the raw mouse screen position
        return ScreenToWorldPosition(MouseScreenPosition);
    }
}
