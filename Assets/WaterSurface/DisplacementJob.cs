using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.WaterSurface
{
    // Displaces Vertices using provided velocity and forces
    public struct DisplacementJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> AdjustedDeltaTime;

        [ReadOnly]
        public NativeArray<float> Alpha;

        [ReadOnly]
        public NativeArray<float> AngularFrequency;

        [ReadOnly]
        public NativeArray<float> BaseOffsets;

        [ReadOnly]
        public NativeArray<float> WorldForces;

        public NativeArray<float> Positions;

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
