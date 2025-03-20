using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record RectanglePhysicsBodyComponent
{
    public float Width;
    public float Height;
    public float Density;
    public Vector2 InitialPosition;
    public bool CreateSensors;
}
