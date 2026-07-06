using GhSpaceGass.Core.Models;
using Rhino.Geometry;

namespace GhSpaceGass.Helpers;

public static class SgPoint3DExtensions
{
    public static Point3d ToPoint3d(this SgPoint3D p) => new(p.X, p.Y, p.Z);
}
