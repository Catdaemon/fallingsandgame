namespace FallingSand.Entity.Component;

record RectanglePhysicsBodyComponent
{
    public float Width;
    public float Height;
    public float Density;
    public WorldPosition InitialPosition;
    public bool CreateSensors;
}
