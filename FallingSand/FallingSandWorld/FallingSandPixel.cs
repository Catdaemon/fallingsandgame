using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FallingSand;
using FallingSandWorld.Pixels;
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

    // Use readonly arrays for better performance when accessing material properties
    private static readonly bool[] _isStatic;
    private static readonly bool[] _isLiquid;
    private static readonly bool[] _isGas;
    private static readonly bool[] _isFire;
    private static readonly byte[] _density;
    private static readonly byte[] _flammability;

    private static readonly ThreadLocal<Random> Random = new(() => new Random());
    private readonly FallingSandWorldChunk ParentChunk;

    // Using static readonly field for adjacent offsets
    public static readonly (int, int)[] AdjacentOffsets = [(-1, 0), (1, 0), (0, -1), (0, 1)];
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
        // Optimize by preallocating arrays with exact size
        int materialCount = Enum.GetValues(typeof(Material)).Length;
        _isStatic = new bool[materialCount];
        _isLiquid = new bool[materialCount];
        _isGas = new bool[materialCount];
        _isFire = new bool[materialCount];
        _density = new byte[materialCount];
        _flammability = new byte[materialCount];

        // Use HashSet for faster lookups when initializing material property arrays
        var staticMaterialsSet = new HashSet<Material>(STATIC_MATERIALS);
        var liquidMaterialsSet = new HashSet<Material>(LIQUID_MATERIALS);
        var gasMaterialsSet = new HashSet<Material>(GAS_MATERIALS);
        var fireMaterialsSet = new HashSet<Material>(FIRE_MATERIALS);

        foreach (Material m in Enum.GetValues(typeof(Material)))
        {
            int index = (int)m;
            _isStatic[index] = staticMaterialsSet.Contains(m);
            _isLiquid[index] = liquidMaterialsSet.Contains(m);
            _isGas[index] = gasMaterialsSet.Contains(m);
            _isFire[index] = fireMaterialsSet.Contains(m);
            _density[index] = MaterialProperties.Densities.GetValueOrDefault<Material, byte>(m, 0);
            _flammability[index] = MaterialProperties.Flammability.GetValueOrDefault<
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

        // Special material behaviour
        var movedInUpdater = PixelMaterialUpdater.UpdatePixel(Random.Value, chunk, position, this);

        // If the pixel did not move, sleep
        if (!moved && !movedInUpdater)
        {
            SleepCounter++;

            if (SleepCounter > SLEEP_AFTER)
            {
                IsAwake = false;
            }
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
            var ourData = Data;
            chunk.parentWorld.SetPixel(position, otherPixel.Data, otherPixel.Velocity);
            chunk.parentWorld.SetPixel(newPosition, ourData, Velocity);
        }
        else
        {
            var ourData = Data;
            chunk.EmptyPixel(chunk.WorldToLocalPosition(position));
            chunk.parentWorld.SetPixel(newPosition, ourData, Velocity);
        }

        return true;
    }

    public static void WakeAdjacentPixels(FallingSandWorldChunk chunk, LocalPosition position)
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
        Data = new FallingSandPixelData { Material = Material.Empty, Color = Color.Transparent };
    }

    public void Set(FallingSandPixelData data, float velocity)
    {
        LastUpdatedFrameId = ParentChunk.parentWorld.CurrentFrameId;
        Data = data;
        Velocity = velocity;
        ComputeProperties();
        Wake();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ComputeProperties()
    {
        int materialIndex = (int)Data.Material;

        IsGas = _isGas[materialIndex];
        Static = _isStatic[materialIndex];
        IsLiquid = _isLiquid[materialIndex];
        IsFire = _isFire[materialIndex];
        Density = _density[materialIndex];
        Flammability = _flammability[materialIndex];
        Lifetime = 0;
        SleepCounter = 0;
    }
    #endregion
}
