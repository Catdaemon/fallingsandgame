using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.Entity.Component;

record PhysicsBodyComponent
{
    public int BottomCollisionCount { get; set; }
    public bool IsCollidingBottom => BottomCollisionCount > 0;
    public int LeftCollisionCount { get; set; }
    public bool IsCollidingLeft => LeftCollisionCount > 0;
    public int RightCollisionCount { get; set; }
    public bool IsCollidingRight => RightCollisionCount > 0;
    public int TopCollisionCount { get; set; }
    public bool IsCollidingTop => TopCollisionCount > 0;

    public Vector2 GroundNormal { get; set; } = Vector2.UnitY; // Default to straight up

    public Body PhysicsBody { get; set; }
}
