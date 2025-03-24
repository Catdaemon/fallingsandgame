using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

struct LightComponent
{
    public float Size { get; set; }
    public float Intensity { get; set; }
    public Color Color { get; set; }
    public bool CastShadows { get; set; }

    public LightComponent(float size, float intensity, Color color, bool castShadows = true)
    {
        Size = size;
        Intensity = intensity;
        Color = color;
        CastShadows = castShadows;
    }
}
