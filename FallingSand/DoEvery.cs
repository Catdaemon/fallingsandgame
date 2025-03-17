using System;
using Microsoft.Xna.Framework;

namespace FallingSand;

class DoEvery
{
    private readonly Action action;
    private readonly double interval;
    private double lastRunTime;

    public DoEvery(Action action, double intervalMillis)
    {
        this.action = action;
        this.interval = intervalMillis;
        lastRunTime = 0;
    }

    public void Update(GameTime time)
    {
        if (time.TotalGameTime.TotalMilliseconds - lastRunTime > interval)
        {
            action();
            lastRunTime = time.TotalGameTime.TotalMilliseconds;
        }
    }
}
