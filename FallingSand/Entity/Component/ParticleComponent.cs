using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand.Entity.Component;

struct ParticleComponent
{
    public int LifeTime { get; set; }
    public int CreatedTime { get; set; }
    public float Size { get; set; }
    public Color Color { get; set; }
}
