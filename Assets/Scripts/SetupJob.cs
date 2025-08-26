using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts
{
    public struct SetupJob : IJobParallelFor
    {
        public NativeArray<Vector3> Velocities;
        public NativeArray<byte> NeighbourCounts;
        public NativeArray<Vector3> NeighbourForces;
        public NativeArray<Vector3> WorldForces;

        public void Execute(int i)
        {
            Velocities[i] = Vector3.zero;
            NeighbourCounts[i] = 0;
            WorldForces[i] = Vector3.zero;
            NeighbourForces[i] = Vector3.zero;
        }
    }
}
