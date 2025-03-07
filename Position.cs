namespace fallingsand.nosync;

public struct LocalPosition
{
    public int X { get; }
    public int Y { get; }

    public LocalPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override int GetHashCode() => (X, Y).GetHashCode();

    public override bool Equals(object obj) => obj is LocalPosition other && Equals(other);

    public bool Equals(LocalPosition other) => X == other.X && Y == other.Y;

    public static bool operator ==(LocalPosition left, LocalPosition right) => left.Equals(right);

    public static bool operator !=(LocalPosition left, LocalPosition right) => !left.Equals(right);
}

public struct WorldPosition
{
    public int X { get; }
    public int Y { get; }

    public WorldPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override int GetHashCode() => (X, Y).GetHashCode();

    public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);

    public bool Equals(WorldPosition other) => X == other.X && Y == other.Y;

    public static bool operator ==(WorldPosition left, WorldPosition right) => left.Equals(right);

    public static bool operator !=(WorldPosition left, WorldPosition right) => !left.Equals(right);
}

public struct ChunkPosition
{
    public int X { get; }
    public int Y { get; }

    public ChunkPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override int GetHashCode() => (X, Y).GetHashCode();

    public override bool Equals(object obj) => obj is ChunkPosition other && Equals(other);

    public bool Equals(ChunkPosition other) => X == other.X && Y == other.Y;

    public static bool operator ==(ChunkPosition left, ChunkPosition right) => left.Equals(right);

    public static bool operator !=(ChunkPosition left, ChunkPosition right) => !left.Equals(right);
}
