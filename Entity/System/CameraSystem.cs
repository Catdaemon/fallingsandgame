using Arch.Core;
using Arch.Core.Extensions;
using FallingSand;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FallingSand.Entity.System;

class CameraSystem : ISystem
{
    private readonly World World;

    public CameraSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime)
    {
        // Find entities with a Camera Follow component
        var query = new QueryDescription().WithAny<CameraFollowComponent>();
        World.Query(
            in query,
            (Arch.Core.Entity entity, ref PhysicsBodyComponent physicsBody) =>
            {
                if (entity.Has<PositionComponent>())
                {
                    var targetPos = entity.Get<PositionComponent>().Value;
                    Vector2 currentPos = Camera.GetPositionF();
                    float smoothFactor = 2f * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    Vector2 newPos = Vector2.Lerp(
                        currentPos,
                        new Vector2(targetPos.X, targetPos.Y),
                        smoothFactor
                    );
                    Camera.SetPosition(newPos.X, newPos.Y);
                }
            }
        );

        // Update the camera's copy of the mouse position
        var mouse = Mouse.GetState();
        Camera.SetMousePosition(mouse.Position.ToVector2());
    }
}
