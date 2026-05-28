using System;
using Vector = Sharp.Shared.Types.Vector;

namespace Prefix.Poop.Utils;

internal static class VectorExtensions
{
    public static float Distance(this Vector vector, Vector other)
        => (float) Math.Sqrt(vector.DistanceSquared(other));

    public static float DistanceSquared(this Vector vector, Vector other)
    {
        var dx = vector.X - other.X;
        var dy = vector.Y - other.Y;
        var dz = vector.Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}
