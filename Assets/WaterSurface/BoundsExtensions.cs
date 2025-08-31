using UnityEngine;

namespace Assets.WaterSurface
{
    internal static class BoundsExtensions
    {
        /// <summary>
        /// Tries to get the bounds of the intersection volume of two bounds.
        /// </summary>
        /// <param name="bounds">The first bounds.</param>
        /// <param name="other">The second bounds.</param>
        /// <param name="result">The bounds of the intersection volume or default(Bounds) if the bounds don't intersect.</param>
        /// <returns>true, when the two volumes intersect. false otherwise.</returns>
        public static bool TryGetOverlap(this Bounds bounds, Bounds other, out Bounds result)
        {
            Vector3 maxCorner = Vector3.Min(bounds.max, other.max);
            Vector3 minCorner = Vector3.Max(bounds.min, other.min);
            Vector3 size = maxCorner - minCorner;
            if (size.x > 0 && size.y > 0 && size.z > 0)
            {
                Vector3 center = (maxCorner + minCorner) * 0.5f;
                result = new(center, size);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Calculates the volume of the bounds in unit³.
        /// </summary>
        /// <param name="bounds">The bounds to get the volume of.</param>
        /// <returns>The volume of the bounds in unit³.</returns>
        public static float Volume(this Bounds bounds)
        {
            return bounds.size.x * bounds.size.y * bounds.size.z;
        }
    }
}
