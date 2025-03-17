namespace FallingSand.Entity.Component;

struct JetpackComponent
{
    public float Fuel;
    public int MaxFuel;

    public JetpackComponent(int maxFuel)
    {
        Fuel = maxFuel;
        MaxFuel = maxFuel;
    }

    public void Refill()
    {
        Fuel = MaxFuel;
    }
}
