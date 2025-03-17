using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record PositionComponent
{
    public Vector2 Position = Vector2.Zero;
    public Vector2 Velocity = Vector2.Zero;
    public float Angle = 0;
    public Vector2 FacingDirection = Vector2.Zero;
}
