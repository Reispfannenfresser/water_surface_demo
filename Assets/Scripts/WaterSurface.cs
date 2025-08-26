using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;

namespace Assets.Scripts
{
    /// <summary>
    /// Component that displaces vertices of a mesh by simulating a water surface using springs
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public class WaterSurface : MonoBehaviour
    {
        /// <summary>
        /// The mesh filter this component will alter the mesh of
        /// </summary>
        protected MeshFilter MeshFilterComponent { get; private set; } = null;

        /// <summary>
        /// The mesh this component alters
        /// </summary>
        protected Mesh Mesh { get; private set; } = null;

        /// <summary>
        /// The number of vertices in the Mesh
        /// </summary>
        protected int VertexCount { get; private set; } = 0;

        /// <summary>
        /// The number of batches used for parallel for jobs.
        /// </summary>
        [Header("Simulation Settings")]
        [SerializeField]
        [Min(1)]
        protected int InnerLoopBatchCount = 64;

        /// <summary>
        /// The max number of neighbours to take into account
        /// </summary>
        [field: SerializeField]
        protected byte MaxNeighbourCount { get; private set; } = 6;

        /// <summary>
        /// The stiffness of all simulated springs.
        /// </summary>
        [Tooltip("The stiffness of the simulated springs.")]
        [Min(0)]
        public float SpringConstant = 0.025f;

        /// <summary>
        /// The time scale of the simulation.
        /// </summary>
        [Tooltip("The time scale of the simulation.")]
        public float SimulationSpeed = 1;

        // Job Variables
        private NativeArray<float> _adjustedDeltaTime = new(1, Allocator.Persistent);
        private NativeArray<float> _springConstant = new(1, Allocator.Persistent);
        private NativeArray<byte> _maxNeighbourCount = new(1, Allocator.Persistent);
        private NativeArray<Vector3> _initialPositions;
        private NativeArray<Vector3> _positions;
        private NativeArray<Vector3> _velocities;
        private NativeArray<int> _neighbours;
        private NativeArray<byte> _neighbourCounts;
        private NativeArray<Vector3> _neighbourForces;
        private NativeArray<Vector3> _worldForces;

        // Jobs
        private SetupJob _setupJob;
        private NeighbourForceJob _neighbourForceJob;
        private DisplacementJob _displacementJob;

        // Handles
        private JobHandle _setupJobHandle;
        private JobHandle _neighbourForceJobHandle;
        private JobHandle _displacementJobHandle;

        protected virtual void Awake()
        {
            MeshFilterComponent = GetComponent<MeshFilter>();
            Mesh = MeshFilterComponent.mesh;
            VertexCount = Mesh.vertices.Length;

            _maxNeighbourCount[0] = MaxNeighbourCount;
            _initialPositions = new NativeArray<Vector3>(Mesh.vertices, Allocator.Persistent);
            _positions = new NativeArray<Vector3>(Mesh.vertices, Allocator.Persistent);
            _velocities = new NativeArray<Vector3>(VertexCount, Allocator.Persistent);

            _neighbourForces = new NativeArray<Vector3>(VertexCount, Allocator.Persistent);
            _worldForces = new NativeArray<Vector3>(VertexCount, Allocator.Persistent);

            _neighbourCounts = new NativeArray<byte>(VertexCount, Allocator.Persistent);
            _neighbours = new NativeArray<int>(
                VertexCount * MaxNeighbourCount,
                Allocator.Persistent
            );

            //TODO: Remove once WorldForces work
            _positions[0] += Vector3.up * 1;

            _setupJob = new SetupJob
            {
                Velocities = _velocities,
                NeighbourCounts = _neighbourCounts,
                NeighbourForces = _neighbourForces,
                WorldForces = _worldForces
            };

            _neighbourForceJob = new NeighbourForceJob
            {
                Positions = _positions,
                InitialPositions = _initialPositions,
                Velocities = _velocities,
                Neighbours = _neighbours,
                NeighbourCounts = _neighbourCounts,
                MaxNeighbourCount = _maxNeighbourCount,
                NeighbourForces = _neighbourForces
            };

            _displacementJob = new DisplacementJob
            {
                AdjustedDeltaTime = _adjustedDeltaTime,
                SpringConstant = _springConstant,
                InitialPositions = _initialPositions,
                WorldForces = _worldForces,
                NeighbourForces = _neighbourForces,
                Positions = _positions,
                Velocities = _velocities
            };

            _setupJobHandle = _setupJob.Schedule(VertexCount, InnerLoopBatchCount);
        }

        protected virtual void Start()
        {
            _setupJobHandle.Complete();
            LoadNeighbourData();
        }

        protected virtual void Update()
        {
            // Set job variables
            _adjustedDeltaTime[0] = Time.deltaTime * SimulationSpeed;
            _springConstant[0] = SpringConstant;

            // Schedule displacement job
            _neighbourForceJobHandle = _neighbourForceJob.Schedule(
                VertexCount,
                InnerLoopBatchCount
            );
            _displacementJobHandle = _displacementJob.Schedule(
                VertexCount,
                InnerLoopBatchCount,
                _neighbourForceJobHandle
            );
        }

        protected virtual void LateUpdate()
        {
            _neighbourForceJobHandle.Complete();
            _displacementJobHandle.Complete();
            Mesh.SetVertices(_positions);
            Mesh.RecalculateNormals();
            Mesh.RecalculateTangents();
        }

        protected virtual void OnDestroy()
        {
            if (!_setupJobHandle.IsCompleted)
            {
                _setupJobHandle.Complete();
            }
            if (!_neighbourForceJobHandle.IsCompleted)
            {
                _neighbourForceJobHandle.Complete();
            }
            if (!_displacementJobHandle.IsCompleted)
            {
                _displacementJobHandle.Complete();
            }
            _adjustedDeltaTime.Dispose();
            _springConstant.Dispose();
            _maxNeighbourCount.Dispose();
            _initialPositions.Dispose();
            _neighbourForces.Dispose();
            _worldForces.Dispose();
            _positions.Dispose();
            _velocities.Dispose();
            _neighbourCounts.Dispose();
            _neighbours.Dispose();
            Destroy(Mesh);
        }

        private void AddNeighbour(int vertex, int neighbourVertex)
        {
            byte neighbourCount = _neighbourCounts[vertex];
            if (neighbourCount >= MaxNeighbourCount)
            {
                return;
            }

            _neighbours[vertex * MaxNeighbourCount + neighbourCount] = neighbourVertex;
            for (int neighbourIndex = 0; neighbourIndex < neighbourCount; neighbourIndex++)
            {
                // Counterbalance neighbourCount increase when duplicate
                if (_neighbours[vertex * MaxNeighbourCount + neighbourIndex] == neighbourVertex)
                {
                    neighbourCount--;
                    break;
                }
            }

            neighbourCount++;
            _neighbourCounts[vertex] = neighbourCount;
        }

        /// <summary>
        /// Ideally this should be baked before start since it's a hefty calculation
        /// </summary>
        protected virtual void LoadNeighbourData()
        {
            for (int triangleIndex = 0; triangleIndex < Mesh.triangles.Length; triangleIndex += 3)
            {
                int vertex0 = Mesh.triangles[triangleIndex];
                int vertex1 = Mesh.triangles[triangleIndex + 1];
                int vertex2 = Mesh.triangles[triangleIndex + 2];

                AddNeighbour(vertex0, vertex1);
                AddNeighbour(vertex0, vertex2);
                AddNeighbour(vertex1, vertex0);
                AddNeighbour(vertex1, vertex2);
                AddNeighbour(vertex2, vertex0);
                AddNeighbour(vertex2, vertex1);
            }
        }
    }
}
