namespace FallingSand.Entity.Component;

class LifetimeComponent
{
    public int LifeTime { get; set; }
    public int CreatedTime { get; set; }

    public LifetimeComponent(int lifeTime, int currentTime)
    {
        LifeTime = lifeTime;
        CreatedTime = currentTime;
    }

    public bool IsExpired(int currentTime)
    {
        return currentTime - CreatedTime > LifeTime;
    }
}
