namespace FallingSand.Entity.Component;

class CirclePhysicsBodyComponent
{
    public float Radius;
    public float Density;
    public WorldPosition InitialPosition;

    public CirclePhysicsBodyComponent(float radius, float density, WorldPosition initialPosition)
    {
        Radius = radius;
        Density = density;
        InitialPosition = initialPosition;
    }
}
