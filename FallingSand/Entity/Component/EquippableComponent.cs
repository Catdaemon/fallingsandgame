using System.Collections.Generic;

namespace FallingSand.Entity.Component;

struct EquippableComponent
{
    public Arch.Core.Entity? Parent;
    public bool IsActive = false;

    public EquippableComponent() { }
}
