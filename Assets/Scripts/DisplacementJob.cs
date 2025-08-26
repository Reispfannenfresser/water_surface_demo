using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts
{
    // Displaces Vertices using provided velocity and forces
    public struct DisplacementJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> AdjustedDeltaTime;

        [ReadOnly]
        public NativeArray<float> SpringConstant;

        [ReadOnly]
        public NativeArray<Vector3> InitialPositions;

        [ReadOnly]
        public NativeArray<Vector3> NeighbourForces;

        [ReadOnly]
        public NativeArray<Vector3> WorldForces;

        public NativeArray<Vector3> Positions;

        public NativeArray<Vector3> Velocities;

        public void Execute(int i)
        {
            Vector3 acceleration =
                -SpringConstant[0] * (Positions[i] - InitialPositions[i])
                + NeighbourForces[i]
                + WorldForces[i];
            Vector3 offset =
                AdjustedDeltaTime[0] * Velocities[i]
                + 0.5f * acceleration * AdjustedDeltaTime[0] * AdjustedDeltaTime[0];
            Positions[i] += Vector3.Scale(Vector3.up, offset);
            Velocities[i] += acceleration * AdjustedDeltaTime[0];
        }
    }
}
