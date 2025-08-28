using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Assets.Scripts.WaterSurface
{
    /// <summary>
    /// Simulates a water surface that responds to Rigidbodies
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterSurface : MonoBehaviour
    {
        /// <summary>
        /// The size of the grid to simulate
        /// </summary>
        [Header("Size")]
        [field: SerializeField]
        private Vector2Int _size = Vector2Int.one * 10;

        /// <summary>
        /// The depth at which Rigidbodies still influence the surface
        /// </summary>
        [field: SerializeField]
        protected float Depth { get; private set; } = 1f;

        /// <summary>
        /// The number of batches used for parallel for jobs.
        /// </summary>
        [Header("Simulation Settings")]
        [SerializeField]
        [Min(1)]
        protected int InnerLoopBatchCount = 64;

        /// <summary>
        /// The mass attached to all springs.
        /// </summary>
        [Tooltip("The mass attached to each simulated spring.")]
        [Min(0.001f)]
        public float Mass = 1f;

        /// <summary>
        /// The stiffness of the springs.
        /// </summary>
        [Tooltip("The stiffness of the simulated springs.")]
        [Min(0.001f)]
        public float SpringConstant = 1f;

        /// <summary>
        /// The dampening of the springs.
        /// </summary>
        [Tooltip("The dampening of the springs.")]
        [Min(0)]
        public float Dampening = 1f;

        /// <summary>
        /// The time scale of the simulation.
        /// </summary>
        [Tooltip("The time scale of the simulation.")]
        public float SimulationSpeed = 1;

        // Job Variables
        protected NativeArray<int> SpringCount = new NativeArray<int>(1, Allocator.Persistent);

        protected NativeArray<int> Size = new NativeArray<int>(2, Allocator.Persistent);

        protected NativeArray<float> SpringPositions;

        protected NativeArray<float> SpringVelocities;

        protected virtual void Awake()
        {
            int springCount = _size.x * _size.y;

            Size[0] = _size.x;
            Size[1] = _size.y;
            SpringCount[0] = springCount;
            SpringPositions = new NativeArray<float>(springCount, Allocator.Persistent);
            SpringVelocities = new NativeArray<float>(springCount, Allocator.Persistent);
        }

        protected virtual void OnDrawGizmos()
        {
            List<Vector3> lines = new();

            Vector2 extent = (Vector2)_size * 0.5f;

            for (int x = 1; x < _size.x; x++)
            {
                lines.Add(new(x - extent.x, 0, extent.y));
                lines.Add(new(x - extent.x, 0, -extent.y));
            }
            for (int y = 1; y < _size.y; y++)
            {
                lines.Add(new(extent.x, 0, y - extent.y));
                lines.Add(new(-extent.x, 0, y - extent.y));
            }
            if (_size.x <= 0 || _size.y <= 0 || Depth < 0)
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.blue;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawLineList(lines.ToArray());
            Gizmos.DrawWireCube(new(0, -0.5f * Depth, 0), new(_size.x, Depth, _size.y));
        }
    }
}
