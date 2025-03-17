namespace FallingSand.Entity.Component;

record CirclePhysicsBodyComponent
{
    public float Radius;
    public float Density;
    public WorldPosition InitialPosition;
    public bool CreateSensors;
}
