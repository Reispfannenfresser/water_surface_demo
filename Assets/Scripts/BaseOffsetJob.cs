using Unity.Collections;
using Unity.Jobs;

namespace Assets.Scripts
{
    /// <summary>
    /// Calculates the average local water level of each vertex's direct neighbourhood.
    /// </summary>
    public struct BaseOffsetJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float> Positions;

        [ReadOnly]
        public NativeArray<int> Neighbours;

        [ReadOnly]
        public NativeArray<byte> NeighbourCounts;

        [ReadOnly]
        public NativeArray<byte> MaxNeighbourCount;

        public NativeArray<float> BaseOffsets;

        public void Execute(int i)
        {
            byte neighbourCount = NeighbourCounts[i];
            float baseOffset = Positions[i];
            for (int neighbourIndex = 0; neighbourIndex < neighbourCount; neighbourIndex++)
            {
                // Get Vertex index of neighbour
                int neighbour = Neighbours[i * MaxNeighbourCount[0] + neighbourIndex];

                // Add position of neighbour to baseOffset
                baseOffset += Positions[neighbour];
            }
            BaseOffsets[i] = baseOffset / (neighbourCount + 1);
        }
    }
}
