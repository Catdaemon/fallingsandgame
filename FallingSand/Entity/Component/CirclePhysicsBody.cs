using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record CirclePhysicsBodyComponent
{
    public float Radius;
    public float Density;
    public Vector2 InitialPosition;
    public Vector2 InitialVelocity;
    public bool CreateSensors;
}
