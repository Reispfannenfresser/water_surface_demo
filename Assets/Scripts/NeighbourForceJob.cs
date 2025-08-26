using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;

namespace Assets.Scripts
{
    public struct NeighbourForceJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Vector3> Positions;

        [ReadOnly]
        public NativeArray<Vector3> InitialPositions;

        [ReadOnly]
        public NativeArray<Vector3> Velocities;

        [ReadOnly]
        public NativeArray<int> Neighbours;

        [ReadOnly]
        public NativeArray<byte> NeighbourCounts;

        [ReadOnly]
        public NativeArray<byte> MaxNeighbourCount;

        public NativeArray<Vector3> NeighbourForces;

        public void Execute(int i)
        {
            byte neighbourCount = NeighbourCounts[i];
            Vector3 neighbourForce = Vector3.zero;
            for (int neighbourIndex = 0; neighbourIndex < neighbourCount; neighbourIndex++)
            {
                int neighbour = Neighbours[i * MaxNeighbourCount[0] + neighbourIndex];
                neighbourForce += Positions[neighbour];
                neighbourForce -= InitialPositions[neighbour];
            }

            NeighbourForces[i] = neighbourForce * 0.1f;
        }
    }
}
