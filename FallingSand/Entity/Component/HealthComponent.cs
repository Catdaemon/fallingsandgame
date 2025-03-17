namespace FallingSand.Entity.Component;

record HealthComponent
{
    public int Current;
    public int Max;
    public bool IsDead => Current <= 0;
}
