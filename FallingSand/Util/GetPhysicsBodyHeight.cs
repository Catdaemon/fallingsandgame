using System;
using nkast.Aether.Physics2D.Collision;
using nkast.Aether.Physics2D.Dynamics;

namespace FallingSand;

static partial class Util
{
    public static float GetPhysicsBodyHeight(Body body)
    {
        // Start with extreme values
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        // Check each fixture
        foreach (Fixture fixture in body.FixtureList)
        {
            // For each child of the fixture
            for (int i = 0; i < fixture.Shape.ChildCount; i++)
            {
                fixture.GetAABB(out AABB fixtureAABB, i);

                // Update min and max
                minY = Math.Min(minY, fixtureAABB.LowerBound.Y);
                maxY = Math.Max(maxY, fixtureAABB.UpperBound.Y);
            }
        }

        return maxY - minY;
    }

    public static float GetPhysicsBodyWidth(Body body)
    {
        // Start with extreme values
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        // Check each fixture
        foreach (Fixture fixture in body.FixtureList)
        {
            // For each child of the fixture
            for (int i = 0; i < fixture.Shape.ChildCount; i++)
            {
                fixture.GetAABB(out AABB fixtureAABB, i);

                // Update min and max
                minX = Math.Min(minX, fixtureAABB.LowerBound.X);
                maxX = Math.Max(maxX, fixtureAABB.UpperBound.X);
            }
        }

        return maxX - minX;
    }
}
