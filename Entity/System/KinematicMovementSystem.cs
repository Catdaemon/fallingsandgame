using System;
using Arch.Core;
using Arch.Core.Extensions;
using FallingSand.Entity.Component;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Collision;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using World = Arch.Core.World;

namespace FallingSand.Entity.System;

class KinematicMovementSystem : ISystem
{
    private readonly World World;

    public KinematicMovementSystem(World world)
    {
        World = world;
    }

    public void Update(GameTime gameTime)
    {
        // Find entities with the necessary components
        var query = new QueryDescription().WithAll<PhysicsBodyComponent, InputStateComponent>();
        World.Query(
            in query,
            (
                Arch.Core.Entity entity,
                ref PhysicsBodyComponent physicsBody,
                ref InputStateComponent inputState
            ) =>
            {
                physicsBody.PhysicsBody.FixedRotation = true;
                var isGrounded = physicsBody.IsCollidingBottom;
                var isSwimming = false;
                var isWallSliding =
                    !isGrounded && (physicsBody.IsCollidingLeft || physicsBody.IsCollidingRight);
                var groundNormal = physicsBody.GroundNormal;
                var physicsBodyRef = physicsBody.PhysicsBody;

                // Check if swimming
                if (entity.Has<SandPixelReaderComponent>())
                {
                    var pixelReader = entity.Get<SandPixelReaderComponent>();
                    // TODO: lookup for is liquid
                    if (pixelReader.Material == FallingSandWorld.Material.Water)
                    {
                        isSwimming = true;
                    }
                }

                float xVelocity;
                float yVelocity = physicsBodyRef.LinearVelocity.Y;

                if (!isSwimming && isGrounded)
                {
                    xVelocity = inputState.Value.NormalisedMoveVector.X * 4f;
                    if (inputState.Value.Jump)
                    {
                        yVelocity = -6f;
                    }
                }
                else if (isSwimming)
                {
                    xVelocity = inputState.Value.NormalisedMoveVector.X * 1.5f;
                    yVelocity = inputState.Value.NormalisedMoveVector.Y * 1.5f;
                    // Slowly sink if not moving
                    if (inputState.Value.Up < 0.1 && inputState.Value.Down < 0.1)
                    {
                        yVelocity = 0.5f;
                    }
                }
                else
                {
                    xVelocity = inputState.Value.NormalisedMoveVector.X * 2f;

                    if (isWallSliding && yVelocity > 0)
                    {
                        var isMovingAgainstWall =
                            (physicsBody.IsCollidingLeft && inputState.Value.Left > 0.1)
                            || (physicsBody.IsCollidingRight && inputState.Value.Right > 0.1);
                        if (isMovingAgainstWall)
                        {
                            xVelocity = 0;
                        }
                        else
                        {
                            xVelocity = inputState.Value.NormalisedMoveVector.X * 2f;
                        }
                    }
                    // Keep existing vertical velocity for air movement
                }

                physicsBodyRef.LinearVelocity = new Vector2(xVelocity, yVelocity);
            }
        );
    }
}
