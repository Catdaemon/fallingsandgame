using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

class HealthComponent
{
    private int Current;
    private int Max;

    public HealthComponent(int current, int max)
    {
        Current = current;
        Max = max;
    }

    public void Damage(int amount)
    {
        Current -= amount;
        if (Current < 0)
        {
            Current = 0;
        }
    }

    public void Heal(int amount)
    {
        Current += amount;
        if (Current > Max)
        {
            Current = Max;
        }
    }

    public bool IsDead()
    {
        return Current <= 0;
    }
}
