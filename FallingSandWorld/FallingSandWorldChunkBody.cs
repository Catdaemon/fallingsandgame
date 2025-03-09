using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FallingSand;
using Microsoft.VisualBasic;
using nkast.Aether.Physics2D.Common;

namespace FallingSandWorld;

class FallingSandWorldChunkBody
{
    public static IEnumerable<FallingSandWorldChunkBody> Generate(FallingSandWorldChunk chunk)
    {
        // 2d array to 1d array
        var chunkPixels = chunk.pixels;

        var x = new nkast.Aether.Physics2D.Common.TextureTools.TextureConverter();

        var verts = PolygonTools.CreatePolygon(data, polygonTexture.Width, true);
    }
}
