using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.WaterSurface
{
    /// <summary>
    /// Simulates springs using dampened harmonic motion.
    /// </summary>
    internal struct DisplacementJob : IJobParallelFor
    {
        /// <summary>
        /// The time that has passed since the last displacement.
        /// </summary>
        [ReadOnly]
        public NativeArray<float> AdjustedDeltaTime;

        /// <summary>
        /// Exponent for dampened harmonic motion.
        /// </summary>
        [ReadOnly]
        public NativeArray<float> Alpha;

        /// <summary>
        /// The angular frequency of the dampened harmonic motion.
        /// This is different to the angular frequency of simple harmonic motion.
        /// </summary>
        [ReadOnly]
        public NativeArray<float> AngularFrequency;

        /// <summary>
        /// The average local height of each springs direct neighbourhood.
        /// </summary>
        [ReadOnly]
        public NativeArray<float> BaseOffsets;

        /// <summary>
        /// The initial positions of the springs. The contents of this NativeArray will be overriden with the new positions.
        /// </summary>
        public NativeArray<float> Positions;

        /// <summary>
        /// The initial velocities of the springs. The contents of this NativeArray will be overriden with the new velocities.
        /// </summary>
        public NativeArray<float> Velocities;

        public void Execute(int i)
        {
            // dampened harmonic motion with base offset;
            float c1 = Positions[i] - BaseOffsets[i];
            float c2 = Velocities[i] / AngularFrequency[0];
            float amplitude = Mathf.Sqrt(c1 * c1 + c2 * c2);
            float initialPhase = Mathf.Atan2(c2, c1);

            float innerCosPart = AngularFrequency[0] * AdjustedDeltaTime[0] + initialPhase;
            float cosPart = Mathf.Cos(innerCosPart);
            float expPart = Mathf.Exp(-Alpha[0] * AdjustedDeltaTime[0]);

            Positions[i] = amplitude * cosPart * expPart + BaseOffsets[i];

            // Derivative of position over time
            Velocities[i] =
                expPart
                * amplitude
                * (Alpha[0] * cosPart + Mathf.Sin(innerCosPart) * AngularFrequency[0]);
        }
    }
}
