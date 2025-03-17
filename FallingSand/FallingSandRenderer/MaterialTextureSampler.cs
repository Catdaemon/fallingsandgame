using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FallingSandWorld;
using Microsoft.Xna.Framework;

namespace FallingSand.FallingSandRenderer;

class MaterialTextureSampler
{
    private readonly Dictionary<Material, TextureSampler> samplers = [];

    private void Load(Material material, string path)
    {
        var sampler = new TextureSampler();
        sampler.Load(path);
        samplers[material] = sampler;
    }

    public void Load()
    {
        var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var path = Path.Combine(root, "Content", "Tiles");

        Load(Material.Sand, Path.Combine(path, "sand.png"));
        Load(Material.Stone, Path.Combine(path, "stone.png"));
    }

    public Color GetPixel(Material material, int x, int y)
    {
        if (material == Material.Empty)
        {
            return Color.Transparent;
        }

        if (samplers.TryGetValue(material, out var sampler))
        {
            return sampler.GetPixel(x, y);
        }

        throw new KeyNotFoundException($"Material {material} not found in samplers");
    }
}
