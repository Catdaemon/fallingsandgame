using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record Velocity
{
    public Vector2 Value { get; set; } = Vector2.Zero;
}
