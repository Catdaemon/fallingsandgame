using System;
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
    private Vector2 offset;

    public CameraSystem(World world)
    {
        World = world;
    }

    // private static QueryDescription WithFollowCameraQuery =
    //     new QueryDescription().WithAny<CameraFollowComponent>();

    public void Update(GameTime gameTime)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            offset.X -= 200 * delta;
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            offset.X += 200 * delta;
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            offset.Y -= 200 * delta;
        }
        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            offset.Y += 200 * delta;
        }
        if (Keyboard.GetState().IsKeyDown(Keys.OemPlus))
        {
            Camera.SetZoom(Camera.GetZoom() +  0.5f * delta );
        }
        if (Keyboard.GetState().IsKeyDown(Keys.OemMinus))
        {
            Camera.SetZoom(Camera.GetZoom() - 0.1f * delta);
        }

        // Find entities with a Camera Follow component
        var withFollowCameraQuery = new QueryDescription().WithAll<CameraFollowComponent>();
        World.Query(
            in withFollowCameraQuery,
            (Arch.Core.Entity entity, ref CameraFollowComponent _) =>
            {
                if (entity.Has<PositionComponent>())
                {
                    var positionComponent = entity.Get<PositionComponent>();
                    // Move the camera to follow the entity
                    // var velocityOffset = positionComponent.Velocity;
                    // // Cap the velocity offset at 64, keeping in mind it can be negative
                    // var velocityFactor = new Vector2(
                    //     Math.Min(Math.Max(velocityOffset.X, -64), 64),
                    //     Math.Min(Math.Max(velocityOffset.Y, -64), 64)
                    // );

                    var targetPos = positionComponent.Position + offset; //+ velocityFactor;
                    // Use a smooth factor to make the camera movement less jarring
                    // Delta time should be used to make the movement framerate-independent
                    // float smoothFactor = 10f;
                    // Vector2 currentPos = Camera.GetPositionF();
                    // Vector2 newPos = Vector2.SmoothStep(
                    //     currentPos,
                    //     new Vector2(targetPos.X, targetPos.Y),
                    //     smoothFactor * delta
                    // );
                    Camera.SetPosition(targetPos.X, targetPos.Y);
                }
            }
        );

        // Camera.SetPosition(Camera.GetPositionF().X + (50 * delta), 0);

        // Update the camera's copy of the mouse position
        var mouse = Mouse.GetState();
        Camera.SetMousePosition(mouse.Position.ToVector2());
    }
}
