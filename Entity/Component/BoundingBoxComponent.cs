using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

class BoundingBoxComponent
{
    public Rectangle Value { get; set; } = Rectangle.Empty;
}
