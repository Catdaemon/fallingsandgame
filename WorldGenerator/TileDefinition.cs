using System.Collections.Generic;
using FallingSandWorld;

namespace FallingSand.WorldGenerator;

class TileDefinition
{
    public Material[] PixelData;
    public string[] EdgePatterns = new string[4];
    public string Name;

    public bool CanBeNeighborTo(TileDefinition other, EdgeDirection direction)
    {
        // This method checks if the current tile can be placed next to 'other'
        // in the specified direction.
        //
        // For example:
        // If direction is RIGHT, then we're checking if the current tile can
        // be placed to the right of 'other', which means we check if
        // current.LEFT edge matches other.RIGHT edge

        // Get the opposite edge of the other tile
        EdgeDirection otherDirection = EdgeHasher.GetOppositeEdge(direction);

        // For falling sand, edges need to match exactly
        return EdgeHasher.AreEdgesCompatible(
            EdgePatterns[(int)direction],
            other.EdgePatterns[(int)otherDirection]
        );
    }

    public override string ToString()
    {
        return Name ?? "(Unnamed Tile)";
    }
}
