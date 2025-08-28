using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts
{
    public struct SetupJob : IJobParallelFor
    {
        public NativeArray<float> Velocities;
        public NativeArray<byte> NeighbourCounts;
        public NativeArray<float> BaseOffsets;
        public NativeArray<float> WorldForces;

        public void Execute(int i)
        {
            Velocities[i] = 0;
            NeighbourCounts[i] = 0;
            WorldForces[i] = 0;
            BaseOffsets[i] = 0;
        }
    }
}
