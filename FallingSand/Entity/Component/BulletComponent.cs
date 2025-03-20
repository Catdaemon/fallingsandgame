using System.Collections.Generic;

namespace FallingSand.Entity.Component;

enum BulletBehaviour
{
    Gravity,
    Bounce,
    Explode,
    Sticky,
}

struct BulletComponent
{
    public IEnumerable<BulletBehaviour> BulletBehaviours;
    public Arch.Core.Entity Source;
    public float Damage;
    public float Speed;
    public float LifeTime;
    public float CreationTime;
    public bool HasCollided;
    public Arch.Core.Entity CollidedWithEntity;
}
