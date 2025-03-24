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
    private const float RaycastDistance = 1.0f; // Shorter distance for more accurate local ground detection
    private const float ForwardRaycastDistance = 1.5f; // Distance to raycast forward for downhill detection
    private const float MaxClimbableSlopeAngle = 85.0f; // Maximum angle in degrees that can be climbed
    private const int RaycastCount = 5; // Increased number of raycasts for better ground detection
    private readonly Random random = new();

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

    public void Update(GameTime gameTime, float deltaTime)
    {
        // Find entities with the necessary components
        var query = new QueryDescription().WithAll<
            PhysicsBodyComponent,
            InputStateComponent,
            PositionComponent
        >();
        World.Query(
            in query,
            (
                Arch.Core.Entity entity,
                ref PhysicsBodyComponent physicsBody,
                ref InputStateComponent inputState,
                ref PositionComponent positionComponent
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

                // Cast multiple rays to determine the ground normal
                var body = physicsBody.PhysicsBody;
                var position = body.Position;
                var halfWidth = body.FixtureList[0].Shape.Radius;
                var halfHeight = body.FixtureList[0].Shape.Radius;

                // Default to standard up vector
                Vector2 groundNormal = Vector2.UnitY;
                bool validGroundNormal = false;
                float shortestDistance = float.MaxValue;

                // Find direction of movement for forward raycasting
                float moveDirection = inputState.Value.NormalisedMoveVector.X;
                bool isMoving = Math.Abs(moveDirection) > 0.01f;

                // Track if we found downhill terrain ahead
                bool downhillAhead = false;
                float downhillDistance = float.MaxValue;

                // Use multiple raycasts spread across the bottom of the body
                for (int i = 0; i < RaycastCount; i++)
                {
                    // Calculate position along the bottom half of the body, from left to right
                    float xOffset = -halfWidth + (i * (2 * halfWidth) / (RaycastCount - 1));

                    // Add small vertical variation to better detect terrain features
                    float yOffset = halfHeight - 0.1f - (i % 2 == 0 ? 0.1f : 0f);

                    Vector2 start = position + new Vector2(xOffset, yOffset);
                    Vector2 end = start + new Vector2(0, RaycastDistance);

                    if (CastRay(body, start, end, out var hitPos, out var normal))
                    {
                        float distance = Vector2.Distance(start, hitPos);
                        if (distance < shortestDistance)
                        {
                            shortestDistance = distance;
                            groundNormal = normal;
                            validGroundNormal = true;
                        }
                    }

                    // Check for downhill terrain when moving
                    if (isMoving)
                    {
                        // Cast forward and slightly downward to detect downhill slopes
                        float forwardDirection = Math.Sign(moveDirection);
                        Vector2 forwardStart =
                            position + new Vector2(forwardDirection * halfWidth, halfHeight - 0.1f);
                        Vector2 forwardEnd =
                            forwardStart
                            + new Vector2(
                                forwardDirection * ForwardRaycastDistance,
                                RaycastDistance
                            );

                        if (
                            CastRay(
                                body,
                                forwardStart,
                                forwardEnd,
                                out var forwardHit,
                                out var forwardNormal
                            )
                        )
                        {
                            float forwardDistance = Vector2.Distance(forwardStart, forwardHit);
                            // Only consider this a valid downhill if the normal points somewhat upward
                            if (forwardNormal.Y < 0 && forwardDistance < downhillDistance)
                            {
                                downhillAhead = true;
                                downhillDistance = forwardDistance;
                            }
                        }
                    }
                }

                // Update our facing direction based on input aim angle
                positionComponent.FacingDirection = inputState.Value.AimVector;

                var physicsBodyRef = physicsBody.PhysicsBody;

                // Check if swimming
                if (entity.Has<SandPixelReaderComponent>())
                {
                    var pixelReader = entity.Get<SandPixelReaderComponent>();
                    // Check if the material is water or if we're in steam (for partial water effects)
                    if (
                        pixelReader.Material == FallingSandWorld.Material.Water
                        || pixelReader.Material == FallingSandWorld.Material.Steam
                    )
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
                    var moveDirectionSpeed = inputState.Value.NormalisedMoveVector.X * moveSpeed;

                    // Handle jumping
                    if (inputState.Value.Jump)
                    {
                        yVelocity = -6f;
                        xVelocity = moveDirectionSpeed;
                    }
                    else if (validGroundNormal)
                    {
                        // Check if we're on a downhill slope
                        bool isDownhill = groundNormal.X * moveDirectionSpeed < 0;

                        // Calculate the slope angle
                        float slopeAngleDegrees = MathHelper.ToDegrees(
                            (float)Math.Acos(Vector2.Dot(groundNormal, Vector2.UnitY))
                        );
                        bool isTooSteep = slopeAngleDegrees > MaxClimbableSlopeAngle;

                        // Follow the ground normal by projecting movement along the surface
                        Vector2 groundTangent = new Vector2(groundNormal.Y, -groundNormal.X);

                        // Make sure the tangent goes in the right direction based on input
                        if (
                            (groundTangent.X < 0 && moveDirectionSpeed > 0)
                            || (groundTangent.X > 0 && moveDirectionSpeed < 0)
                        )
                        {
                            groundTangent = -groundTangent;
                        }

                        if (isTooSteep && !isDownhill)
                        {
                            // If slope is too steep going uphill, limit movement
                            bool movingUpSlope =
                                (groundNormal.X > 0 && moveDirectionSpeed > 0)
                                || (groundNormal.X < 0 && moveDirectionSpeed < 0);

                            if (movingUpSlope)
                            {
                                // Very limited movement on very steep uphill slopes
                                xVelocity = moveDirectionSpeed * 0.1f;
                                yVelocity = 0;
                            }
                            else
                            {
                                // Going downhill on steep slopes - use ground tangent for movement
                                float magnitude = Math.Abs(moveDirectionSpeed);
                                Vector2 movementVector = groundTangent * magnitude;

                                // Apply the movement with a slight increase for downhill acceleration
                                xVelocity = movementVector.X * 1.2f;
                                yVelocity = movementVector.Y * 1.2f;
                            }
                        }
                        else
                        {
                            // Standard slope following
                            float magnitude = Math.Abs(moveDirectionSpeed);
                            Vector2 movementVector = groundTangent * magnitude;

                            // Apply the movement
                            xVelocity = movementVector.X;
                            yVelocity = movementVector.Y;

                            // Add extra downward force on downhill to keep player on ground
                            if (isDownhill && isMoving)
                            {
                                yVelocity += 1.0f;
                            }
                        }

                        // If we're moving and there's downhill terrain ahead but we're not currently on a downhill,
                        // apply a small downward force to help the player follow the terrain
                        if (
                            isMoving
                            && downhillAhead
                            && !isDownhill
                            && Math.Sign(moveDirectionSpeed)
                                == Math.Sign(inputState.Value.NormalisedMoveVector.X)
                        )
                        {
                            // Calculate how much to pull the player down to follow the terrain
                            // The closer we are to the edge, the stronger the downward force
                            float edgeProximityFactor =
                                1.0f
                                - MathHelper.Clamp(
                                    downhillDistance / ForwardRaycastDistance,
                                    0f,
                                    1f
                                );
                            yVelocity += 2.0f * edgeProximityFactor;
                        }
                    }
                    else
                    {
                        // Fallback when no valid ground normal
                        xVelocity = moveDirectionSpeed;
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
                            jetpack.Fuel -= 1f * deltaTime;
                            yVelocity -= 20f * deltaTime;

                            var spawnParticle = random.Next(0, 2) == 0;
                            if (spawnParticle)
                            {
                                var particleSpawnPosition =
                                    positionComponent.FacingDirection.X > 0
                                        ? new Vector2(-4, 4)
                                        : new Vector2(4, 4);
                                var particleHorizontalSpray = ((float)random.Next(-20, 20)) / 50f;
                                var particleVelocity =
                                    new Vector2(0, Math.Max(yVelocity, 0))
                                    + new Vector2(particleHorizontalSpray, 0);

                                // Spawn a particle
                                World.Create(
                                    new PositionComponent()
                                    {
                                        Position =
                                            Convert.MetersToPixels(physicsBodyRef.Position)
                                            + particleSpawnPosition,
                                        Velocity = particleVelocity,
                                    },
                                    new ParticleComponent()
                                    {
                                        Color = Color.Black,
                                        Size = 1,
                                        Fade = true,
                                        Collide = false,
                                        Gravity = true,
                                    },
                                    new LifetimeComponent(
                                        1000,
                                        (int)gameTime.TotalGameTime.TotalMilliseconds
                                    )
                                );
                            }
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
