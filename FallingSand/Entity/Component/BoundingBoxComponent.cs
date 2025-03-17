using Microsoft.Xna.Framework;

namespace FallingSand.Entity.Component;

record BoundingBoxComponent
{
    public Rectangle Value { get; set; } = Rectangle.Empty;
}
