using System;
using System.Runtime.CompilerServices;
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
    private const float RaycastDistance = 1f; // Distance to cast for ground detection

    public KinematicMovementSystem(World world)
    {
        World = world;
    }

    private bool CastRay(
        Body body,
        Vector2 start,
        Vector2 end,
        out Vector2 hitPos,
        out Vector2 normal
    )
    {
        bool hit = false;
        Vector2 hitPoint = Vector2.Zero;
        Vector2 hitNormal = Vector2.UnitY;

        body.World.RayCast(
            (fixture, point, normalVector, fraction) =>
            {
                if (fixture.Body == body)
                {
                    return -1;
                }
                hit = true;
                hitPoint = point;
                hitNormal = normalVector;
                return 0;
            },
            start,
            end
        );

        hitPos = hitPoint;
        normal = hitNormal;
        return hit;
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

                if (physicsBody.IsCollidingBottom)
                {
                    physicsBody.LeftGroundTime = 0;
                }
                else
                {
                    physicsBody.LeftGroundTime += gameTime.ElapsedGameTime.TotalMilliseconds;
                }
                var isCoyoteTime = physicsBody.LeftGroundTime < 100;

                var isGrounded = physicsBody.IsCollidingBottom || isCoyoteTime;
                var isSwimming = false;
                var isWallSliding =
                    !isGrounded && (physicsBody.IsCollidingLeft || physicsBody.IsCollidingRight);

                // Cast two rays to determine the ground normal
                var body = physicsBody.PhysicsBody;
                var position = body.Position;
                var halfWidth = body.FixtureList[0].Shape.Radius;
                var halfHeight = body.FixtureList[0].Shape.Radius;

                // Create raycast start positions slightly offset from the bottom corners of the body
                var startLeft = position + new Vector2(-halfWidth + 0.05f, halfHeight - 0.05f);
                var startRight = position + new Vector2(halfWidth - 0.05f, halfHeight - 0.05f);
                var endLeft = startLeft + new Vector2(0, RaycastDistance);
                var endRight = startRight + new Vector2(0, RaycastDistance);

                // Get ground hits and normals
                bool hitLeftSuccess = CastRay(
                    body,
                    startLeft,
                    endLeft,
                    out var hitLeft,
                    out var leftNormal
                );
                bool hitRightSuccess = CastRay(
                    body,
                    startRight,
                    endRight,
                    out var hitRight,
                    out var rightNormal
                );

                // Default to standard up vector
                Vector2 groundNormal = Vector2.UnitY;
                bool validGroundNormal = false;

                // Calculate ground normal based on available hits
                if (hitLeftSuccess && hitRightSuccess)
                {
                    // If both rays hit, calculate the ground tangent and then the normal
                    Vector2 groundTangent = hitRight - hitLeft;

                    if (groundTangent.LengthSquared() > 0.0001f) // Ensure we have a valid tangent
                    {
                        groundTangent.Normalize();
                        // Ground normal is perpendicular to tangent (rotate 90 degrees counterclockwise)
                        groundNormal = new Vector2(-groundTangent.Y, groundTangent.X);
                        validGroundNormal = true;
                    }
                }
                else if (hitLeftSuccess)
                {
                    groundNormal = leftNormal;
                    validGroundNormal = true;
                }
                else if (hitRightSuccess)
                {
                    groundNormal = rightNormal;
                    validGroundNormal = true;
                }

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
                    // Base movement speed
                    var moveSpeed = 4f;
                    var moveDirection = inputState.Value.NormalisedMoveVector.X * moveSpeed;

                    // Handle jumping
                    if (inputState.Value.Jump)
                    {
                        yVelocity = -6f;
                        xVelocity = moveDirection; // Standard horizontal movement while jumping
                    }
                    else if (validGroundNormal)
                    {
                        // Follow the ground normal by projecting movement along the surface
                        // First, get the ground tangent (perpendicular to normal)
                        Vector2 groundTangent = new Vector2(groundNormal.Y, -groundNormal.X);

                        // Make sure the tangent goes in the right direction based on input
                        if (
                            (groundTangent.X < 0 && moveDirection > 0)
                            || (groundTangent.X > 0 && moveDirection < 0)
                        )
                        {
                            groundTangent = -groundTangent;
                        }

                        // Project movement along the ground tangent
                        float magnitude = Math.Abs(moveDirection);
                        Vector2 movementVector = groundTangent * magnitude;

                        // Apply the movement
                        xVelocity = movementVector.X;
                        yVelocity = movementVector.Y;
                    }
                    else
                    {
                        // Fallback when no valid ground normal
                        xVelocity = moveDirection;
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

                // Jetpack logic
                if (entity.Has<JetpackComponent>())
                {
                    var jetpack = entity.Get<JetpackComponent>();

                    if (!isGrounded && !isSwimming)
                    {
                        if (inputState.Value.Jump && jetpack.Fuel > 0)
                        {
                            jetpack.Fuel -= 0.1f;
                            yVelocity -= 0.005f;
                        }
                    }
                    if (isGrounded)
                    {
                        jetpack.Refill();
                    }
                }

                physicsBodyRef.LinearVelocity = new Vector2(xVelocity, yVelocity);

                // Set the animation if applicable
                if (entity.Has<SpriteComponent>())
                {
                    var sprite = entity.Get<SpriteComponent>();
                    if (isGrounded)
                    {
                        if (Math.Abs(xVelocity) > 0.1)
                        {
                            sprite.SetAnimation("Run");
                        }
                        else
                        {
                            sprite.SetAnimation("Idle");
                        }
                    }
                    else
                    {
                        sprite.SetAnimation("Jump");
                    }
                }
            }
        );
    }
}
