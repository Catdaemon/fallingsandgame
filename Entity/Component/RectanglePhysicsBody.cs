namespace FallingSand.Entity.Component;

class RectanglePhysicsBodyComponent
{
    public float Width;
    public float Height;
    public float Density;
    public WorldPosition InitialPosition;

    public RectanglePhysicsBodyComponent(
        float width,
        float height,
        float density,
        WorldPosition initialPosition
    )
    {
        Width = width;
        Height = height;
        Density = density;
        InitialPosition = initialPosition;
    }
}
