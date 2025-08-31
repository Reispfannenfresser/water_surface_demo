using Unity.Collections;
using Unity.Jobs;

namespace Assets.WaterSurface
{
    /// <summary>
    /// Calculates the average local height of each spring's direct neighbourhood.
    /// </summary>
    internal struct BaseOffsetJob : IJobParallelFor
    {
        /// <summary>
        /// The positions of the springs.
        /// </summary>
        [ReadOnly]
        public NativeArray<float> Positions;

        /// <summary>
        /// The velocities of the springs.
        /// </summary>
        [ReadOnly]
        public NativeArray<int> SpringGridSize;

        /// <summary>
        /// The contents of this NativeArray will be overriden with the calculated average local height of each springs direct neighbourhood.
        /// </summary>
        public NativeArray<float> BaseOffsets;

        /// <summary>
        /// The influence each nearby spring has.
        /// </summary>
        private static readonly float[,] _kernel =
        {
            { 1, 2, 4, 2, 1 },
            { 2, 4, 8, 4, 2 },
            { 4, 8, 16, 8, 4 },
            { 2, 4, 8, 4, 2 },
            { 1, 2, 4, 2, 1 },
        };

        public void Execute(int i)
        {
            float waterVolume = 0;
            float waterArea = 0;

            int row = i / SpringGridSize[0];
            int column = i % SpringGridSize[0];

            for (int rowOffset = -2; rowOffset <= 2; rowOffset++)
            {
                int neighbourRow = row + rowOffset;
                if (neighbourRow < 0 || neighbourRow >= SpringGridSize[1])
                {
                    continue;
                }
                for (int columnOffset = -2; columnOffset <= 2; columnOffset++)
                {
                    int neighbourColumn = column + columnOffset;
                    if (neighbourColumn < 0 || neighbourColumn >= SpringGridSize[0])
                    {
                        continue;
                    }

                    int neighbourIndex = neighbourRow * SpringGridSize[0] + neighbourColumn;
                    float area = _kernel[rowOffset + 2, columnOffset + 2];
                    waterVolume += Positions[neighbourIndex] * area;
                    waterArea += area;
                }
            }

            BaseOffsets[i] = waterVolume / waterArea;
        }
    }
}
