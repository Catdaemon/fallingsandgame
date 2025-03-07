using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.Entity.Component;

class PhysicsBodyComponent
{
    public Body PhysicsBodyRef { get; set; } = null;
}
