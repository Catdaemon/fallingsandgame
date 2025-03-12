using System.Collections.Generic;
using System.Linq;

namespace FallingSand.WorldGenerator;

class Tile
{
    public int X;
    public int Y;
    public List<TileDefinition> Possibilities;
    public TileDefinition TileDefinition => Possibilities.FirstOrDefault();
    public int Entropy => Possibilities.Count;
}
