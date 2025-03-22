using System.Collections.Generic;
using System.Linq;

namespace FallingSand.WorldGenerator;

class Tile
{
    public int X;
    public int Y;
    public List<TileDefinition> Possibilities;
    public bool IsPrefabTile; // Flag to identify tiles that come from prefabs
    public TileDefinition TileDefinition => Possibilities.FirstOrDefault();
    public int Entropy => Possibilities.Count;
}
