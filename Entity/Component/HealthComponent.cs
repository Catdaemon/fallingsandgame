using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

class HealthComponent
{
    private int _current;
    private int _max;

    public HealthComponent(int current, int max)
    {
        _current = current;
        _max = max;
    }

    public void Damage(int amount)
    {
        _current -= amount;
        if (_current < 0)
        {
            _current = 0;
        }
    }

    public void Heal(int amount)
    {
        _current += amount;
        if (_current > _max)
        {
            _current = _max;
        }
    }

    public bool IsDead()
    {
        return _current <= 0;
    }
}
