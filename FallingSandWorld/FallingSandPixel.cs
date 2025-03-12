using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FallingSand;
using Microsoft.Xna.Framework;

namespace FallingSandWorld;

public enum Material
{
    Empty,
    Sand,
    Grass,
    Water,
    Stone,
    Lava,
    Wood,
    Fire,
    Ember,
    Smoke,
    Steam,
    Ice,
    Snow,
    Acid,
    Oil,
}

public static class MaterialProperties
{
    public static readonly IReadOnlyDictionary<Material, byte> Densities = new Dictionary<
        Material,
        byte
    >
    {
        { Material.Empty, 0 },
        { Material.Water, 2 },
        { Material.Sand, 5 },
        { Material.Stone, 5 },
        { Material.Lava, 5 },
        { Material.Wood, 5 },
        { Material.Fire, 5 },
        { Material.Ember, 5 },
        { Material.Smoke, 0 },
        { Material.Steam, 0 },
        { Material.Ice, 5 },
        { Material.Snow, 5 },
        { Material.Acid, 5 },
        { Material.Oil, 5 },
        { Material.Grass, 5 },
    };

    // Flammability is a 1-in-x chance of catching fire
    public static readonly IReadOnlyDictionary<Material, byte> Flammability = new Dictionary<
        Material,
        byte
    >
    {
        { Material.Empty, 0 },
        { Material.Water, 0 },
        { Material.Sand, 0 },
        { Material.Stone, 0 },
        { Material.Lava, 0 },
        { Material.Wood, 50 },
        { Material.Fire, 0 },
        { Material.Ember, 0 },
        { Material.Smoke, 0 },
        { Material.Steam, 0 },
        { Material.Ice, 0 },
        { Material.Snow, 0 },
        { Material.Acid, 0 },
        { Material.Oil, 0 },
        { Material.Grass, 99 },
    };
}

struct FallingSandPixelData
{
    public Material Material;
    public Color Color;
}

class FallingSandPixel
{
    #region Constants
    private static readonly Material[] STATIC_MATERIALS =
    {
        Material.Stone,
        Material.Wood,
        Material.Ice,
        Material.Fire,
    };

    private static readonly Material[] LIQUID_MATERIALS =
    {
        Material.Water,
        Material.Lava,
        Material.Acid,
        Material.Oil,
    };

    private static readonly Material[] GAS_MATERIALS = { Material.Smoke, Material.Steam };

    private static readonly Material[] FIRE_MATERIALS =
    {
        Material.Fire,
        Material.Ember,
        Material.Lava,
    };

    private static readonly bool[] _isStatic = new bool[Enum.GetValues(typeof(Material)).Length];
    private static readonly bool[] _isLiquid = new bool[Enum.GetValues(typeof(Material)).Length];
    private static readonly bool[] _isGas = new bool[Enum.GetValues(typeof(Material)).Length];
    private static readonly bool[] _isFire = new bool[Enum.GetValues(typeof(Material)).Length];
    private static readonly byte[] _density = new byte[Enum.GetValues(typeof(Material)).Length];
    private static readonly byte[] _flammability = new byte[
        Enum.GetValues(typeof(Material)).Length
    ];

    private static readonly ThreadLocal<Random> Random = new(() => new Random());
    private readonly FallingSandWorldChunk ParentChunk;

    private static readonly (int, int)[] AdjacentOffsets = [(-1, 0), (1, 0), (0, -1), (0, 1)];
    public const float GRAVITY = 0.5f;
    public const float MAX_SPEED = 16f;
    public const int SLEEP_AFTER = 10;
    #endregion

    #region Properties
    public FallingSandPixelData Data;
    public long LastUpdatedFrameId = -1;
    public bool IsAwake = true;
    public byte SleepCounter = 0;
    public bool Static = false;
    public bool IsLiquid = false;
    public bool IsFire = false;
    public byte Density = 0;
    public byte Flammability = 0;
    public bool IsGas;
    public uint Lifetime = 0;
    public float Velocity = 0f;
    #endregion

    static FallingSandPixel()
    {
        // Initialize lookup arrays
        foreach (Material m in Enum.GetValues(typeof(Material)))
        {
            _isStatic[(int)m] = STATIC_MATERIALS.Contains(m);
            _isLiquid[(int)m] = LIQUID_MATERIALS.Contains(m);
            _isGas[(int)m] = GAS_MATERIALS.Contains(m);
            _isFire[(int)m] = FIRE_MATERIALS.Contains(m);
            _density[(int)m] = MaterialProperties.Densities.GetValueOrDefault<Material, byte>(m, 0);
            _flammability[(int)m] = MaterialProperties.Flammability.GetValueOrDefault<
                Material,
                byte
            >(m, 0);
        }
    }

    #region Constructors
    public FallingSandPixel(FallingSandWorldChunk parentChunk, Material material, Color color)
    {
        ParentChunk = parentChunk;
        Data = new() { Material = material, Color = color };
        ComputeProperties();
    }
    #endregion

    #region Methods
    public void Update(FallingSandWorldChunk chunk, LocalPosition position)
    {
        if (LastUpdatedFrameId == chunk.parentWorld.CurrentFrameId)
        {
            return;
        }

        LastUpdatedFrameId = chunk.parentWorld.CurrentFrameId;

        if (!IsAwake)
        {
            return;
        }

        if (Data.Material == Material.Empty)
        {
            IsAwake = false;
            return;
        }

        bool moved = false;

        // Move vertically
        if (!Static)
        {
            bool movedVertically = MoveVertical(chunk, position);
            if (movedVertically)
            {
                moved = true;
            }

            if (!moved)
            {
                Velocity = 0;
            }
        }

        // Special gas behaviour
        if (IsGas)
        {
            Lifetime++;

            if (Lifetime > 1000)
            {
                chunk.EmptyPixel(position);
            }
        }

        // Special fire behaviour
        if (IsFire)
        {
            moved = true;
            Lifetime++;

            // After lifetime, randomly decide whether to extinguish
            if (Lifetime > 100 && Random.Value.Next(100) < 10)
            {
                chunk.EmptyPixel(position);
            }

            // Randomise our colour to make the fire look more natural
            // TODO: this probably belongs in a pixel shader
            var worldPosition = chunk.LocalToWorldPosition(position);
            // chunk.parentWorld.SetPixel(
            //     worldPosition,
            //     new FallingSandPixelData
            //     {
            //         Material = Material.Fire,
            //         Color = new Color(255, random.Next(100, 200), random.Next(0, 100)),
            //     }
            // );

            // Randomly emit smoke above if there is space
            if (Random.Value.Next(100) < 5)
            {
                var abovePosition = new WorldPosition(worldPosition.X, worldPosition.Y - 1);
                if (chunk.parentWorld.GetPixel(abovePosition).Data.Material == Material.Empty)
                {
                    chunk.parentWorld.SetPixel(
                        abovePosition,
                        new FallingSandPixelData
                        {
                            Material = Material.Smoke,
                            Color = new Color(0, 255, 0),
                        }
                    );
                }
            }

            // Randomly emit an ember if there is space below
            if (Random.Value.Next(2000) < 1)
            {
                var belowPosition = new WorldPosition(worldPosition.X, worldPosition.Y + 1);
                if (chunk.parentWorld.GetPixel(belowPosition).Data.Material == Material.Empty)
                {
                    chunk.parentWorld.SetPixel(
                        belowPosition,
                        new FallingSandPixelData
                        {
                            Material = Material.Ember,
                            Color = new Color(255, 0, 0),
                        }
                    );
                }
            }

            // Remove if we are touching water
            foreach (var (dx, dy) in AdjacentOffsets)
            {
                var pixel = chunk.parentWorld.GetPixel(
                    new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy)
                );
                if (pixel.Data.Material == Material.Water)
                {
                    // Remove us
                    chunk.EmptyPixel(position);
                    // Replace the water with steam
                    chunk.parentWorld.SetPixel(
                        new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy),
                        new FallingSandPixelData
                        {
                            Material = Material.Steam,
                            Color = new Color(0, 255, 0),
                        }
                    );
                    break;
                }
                else if (Random.Value.Next(100) < pixel.Flammability)
                {
                    // Try converting adjacent pixels to fire
                    chunk.parentWorld.SetPixel(
                        new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy),
                        new FallingSandPixelData
                        {
                            Material = Material.Fire,
                            Color = new Color(255, 0, 0),
                        }
                    );
                }
            }
        }

        // If the pixel did not move, sleep
        if (!moved)
        {
            SleepCounter++;

            if (SleepCounter > SLEEP_AFTER)
            {
                IsAwake = false;
            }
        }

        if (moved)
        {
            WakeAdjacentPixels(chunk, position);
        }
    }

    private (bool swapPlaces, WorldPosition? newPosition) TryMoveTo(
        FallingSandWorldChunk chunk,
        WorldPosition targetPosition
    )
    {
        var targetPixel = chunk.parentWorld.GetPixel(targetPosition);
        if (targetPixel.Data.Material == Material.Empty)
        {
            return (false, targetPosition);
        }

        if (targetPixel.Static)
        {
            return (false, null);
        }

        // If our density is higher, swap places
        if (Density > targetPixel.Density)
        {
            return (true, targetPosition);
        }

        return (false, null);
    }

    private bool MoveVertical(FallingSandWorldChunk chunk, LocalPosition position)
    {
        var previousWorldPosition = chunk.LocalToWorldPosition(position);
        var newWorldPosition = chunk.LocalToWorldPosition(position);
        var direction = IsGas ? -1 : 1;

        Velocity += GRAVITY;

        // Loop velocity times to allow for fast movement for movement in the primary direction
        WorldPosition? moveDownPosition = null;
        bool moveDownSwap = false;
        for (int i = 1; i <= (int)Velocity; i++)
        {
            var (swap, downMoveAttempt) = TryMoveTo(
                chunk,
                new WorldPosition(newWorldPosition.X, newWorldPosition.Y + (direction * i))
            );
            if (downMoveAttempt != null)
            {
                moveDownPosition = downMoveAttempt;
                moveDownSwap = swap;
            }
            else
            {
                // Movement was blocked, so stop
                break;
            }
        }

        if (moveDownPosition != null)
        {
            return CommitMove(chunk, previousWorldPosition, moveDownPosition.Value, moveDownSwap);
        }

        if (IsGas)
        {
            // If we are a gas, randomly try to move to the side first
            bool shouldMoveSideways = Random.Value.Next(20) == 0;
            if (shouldMoveSideways)
            {
                var (swap, sideMove) = TryMoveTo(
                    chunk,
                    new WorldPosition(
                        newWorldPosition.X + (Random.Value.Next(2) == 0 ? -1 : 1),
                        newWorldPosition.Y
                    )
                );
                if (sideMove != null)
                {
                    return CommitMove(chunk, previousWorldPosition, sideMove.Value, swap);
                }
            }
        }

        var initialDirection = Random.Value.Next(2) == 0 ? -1 : 1;

        // Try moving diagonally
        for (int i = 0; i < 2; i++)
        {
            var (swap, diagonalMove) = TryMoveTo(
                chunk,
                new WorldPosition(
                    newWorldPosition.X + initialDirection,
                    newWorldPosition.Y + direction
                )
            );
            if (diagonalMove != null)
            {
                return CommitMove(chunk, previousWorldPosition, diagonalMove.Value, swap);
            }

            initialDirection *= -1;
        }

        // If we are a liquid, try moving sideways
        if (IsLiquid || IsGas)
        {
            for (int i = 0; i < 2; i++)
            {
                var (swap, sideMove) = TryMoveTo(
                    chunk,
                    new WorldPosition(newWorldPosition.X + initialDirection, newWorldPosition.Y)
                );
                if (sideMove != null)
                {
                    return CommitMove(chunk, previousWorldPosition, sideMove.Value, swap);
                }

                initialDirection *= -1;
            }
        }

        return false;
    }

    private bool CommitMove(
        FallingSandWorldChunk chunk,
        WorldPosition position,
        WorldPosition newPosition,
        bool swap
    )
    {
        if (newPosition.X == position.X && newPosition.Y == position.Y)
        {
            // We did not move, nothing to do
            return false;
        }

        if (swap)
        {
            // Swap the two pixels
            var otherPixel = chunk.parentWorld.GetPixel(newPosition);
            chunk.parentWorld.SetPixel(newPosition, Data, Velocity);
            chunk.parentWorld.SetPixel(position, otherPixel.Data, otherPixel.Velocity);
        }
        else
        {
            // Set the new pixel at the world level to enable cross-chunk moves
            chunk.parentWorld.SetPixel(newPosition, Data, Velocity);
            // Empty the current pixel
            chunk.EmptyPixel(chunk.WorldToLocalPosition(position));
        }

        return true;
    }

    private static void WakeAdjacentPixels(FallingSandWorldChunk chunk, LocalPosition position)
    {
        foreach (var (dx, dy) in AdjacentOffsets)
        {
            var worldPosition = chunk.LocalToWorldPosition(position);
            var adjacentPixel = chunk.parentWorld.GetPixel(
                new WorldPosition(worldPosition.X + dx, worldPosition.Y + dy)
            );
            adjacentPixel.Wake();

            // Make sure their chunk is awake
            chunk.parentWorld.WakeChunkAt(worldPosition);
        }
    }

    public void Wake()
    {
        IsAwake = true;
    }

    public void Empty()
    {
        Data = new FallingSandPixelData { Material = Material.Empty, Color = Color.Black };
    }

    public void Set(FallingSandPixelData data, float velocity)
    {
        LastUpdatedFrameId = ParentChunk.parentWorld.CurrentFrameId;
        Data = data;
        Velocity = velocity;
        ComputeProperties();
        Wake();
    }

    public void ComputeProperties()
    {
        int materialIndex = (int)Data.Material;

        IsGas = _isGas[materialIndex];
        Static = _isStatic[materialIndex];
        IsLiquid = _isLiquid[materialIndex];
        IsFire = _isFire[materialIndex];
        Density = _density[materialIndex];
        Flammability = _flammability[materialIndex];

        if (!IsFire)
        {
            Lifetime = 0;
        }

        SleepCounter = 0;
    }
    #endregion
}
