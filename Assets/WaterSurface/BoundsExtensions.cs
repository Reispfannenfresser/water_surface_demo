using UnityEngine;

namespace Assets.WaterSurface
{
    public static class BoundsExtensions
    {
        public static bool TryGetOverlap(this Bounds bounds, Bounds other, out Bounds result)
        {
            Vector3 maxCorner = Vector3.Min(bounds.max, other.max);
            Vector3 minCorner = Vector3.Max(bounds.min, other.min);
            Vector3 size = maxCorner - minCorner;
            Vector3 center = (maxCorner + minCorner) * 0.5f;
            if (size.x > 0 && size.y > 0 && size.z > 0)
            {
                result = new(center, size);
                return true;
            }
            result = default;
            return false;
        }

        public static float Volume(this Bounds bounds)
        {
            return bounds.size.x * bounds.size.y * bounds.size.z;
        }
    }
}
