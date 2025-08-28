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
        private NativeArray<float> _adjustedDeltaTime = new(1, Allocator.Persistent);
        private NativeArray<float> _alpha = new(1, Allocator.Persistent);
        private NativeArray<float> _angularFrequency = new(1, Allocator.Persistent);
        private NativeArray<byte> _maxNeighbourCount = new(1, Allocator.Persistent);
        private NativeArray<float> _positions;
        private NativeArray<float> _velocities;
        private NativeArray<int> _neighbours;
        private NativeArray<byte> _neighbourCounts;
        private NativeArray<float> _baseOffsets;
        private NativeArray<float> _worldForces;

        // Jobs
        private BaseOffsetJob _baseOffsetJob;
        private DisplacementJob _displacementJob;

        // Handles
        private JobHandle _baseOffsetJobHandle;
        private JobHandle _displacementJobHandle;

        protected virtual void Awake()
        {
            MeshFilterComponent = GetComponent<MeshFilter>();
            Mesh = MeshFilterComponent.mesh;
            VertexCount = Mesh.vertexCount;

            _maxNeighbourCount[0] = MaxNeighbourCount;
            _positions = new NativeArray<float>(VertexCount, Allocator.Persistent);
            _velocities = new NativeArray<float>(VertexCount, Allocator.Persistent);

            _baseOffsets = new NativeArray<float>(VertexCount, Allocator.Persistent);
            _worldForces = new NativeArray<float>(VertexCount, Allocator.Persistent);

            _neighbourCounts = new NativeArray<byte>(VertexCount, Allocator.Persistent);
            _neighbours = new NativeArray<int>(
                VertexCount * MaxNeighbourCount,
                Allocator.Persistent
            );

            _baseOffsetJob = new BaseOffsetJob
            {
                Positions = _positions,
                Neighbours = _neighbours,
                NeighbourCounts = _neighbourCounts,
                MaxNeighbourCount = _maxNeighbourCount,
                BaseOffsets = _baseOffsets
            };

            _displacementJob = new DisplacementJob
            {
                AdjustedDeltaTime = _adjustedDeltaTime,
                Alpha = _alpha,
                AngularFrequency = _angularFrequency,
                BaseOffsets = _baseOffsets,
                WorldForces = _worldForces,
                Positions = _positions,
                Velocities = _velocities
            };

            // _setupJobHandle = _setupJob.Schedule(VertexCount, InnerLoopBatchCount);
        }

        protected virtual void Start()
        {
            //  _setupJobHandle.Complete();
            LoadMeshData();

            // TODO: Remove once WorldForces work
            _positions[1700] += 10;
        }

        protected virtual void Update()
        {
            // Set job variables
            _adjustedDeltaTime[0] = Time.deltaTime * SimulationSpeed;
            _alpha[0] = Dampening / (2f * Mass);
            _angularFrequency[0] = Mathf.Sqrt(SpringConstant / Mass - (_alpha[0] * _alpha[0]));

            // Schedule displacement job
            _baseOffsetJobHandle = _baseOffsetJob.Schedule(VertexCount, InnerLoopBatchCount);
            _displacementJobHandle = _displacementJob.Schedule(
                VertexCount,
                InnerLoopBatchCount,
                _baseOffsetJobHandle
            );
        }

        protected virtual void LateUpdate()
        {
            _baseOffsetJobHandle.Complete();
            _displacementJobHandle.Complete();

            UpdateMesh();
        }

        protected virtual void OnDestroy()
        {
            if (!_baseOffsetJobHandle.IsCompleted)
            {
                _baseOffsetJobHandle.Complete();
            }
            if (!_displacementJobHandle.IsCompleted)
            {
                _displacementJobHandle.Complete();
            }
            _adjustedDeltaTime.Dispose();
            _angularFrequency.Dispose();
            _maxNeighbourCount.Dispose();
            _positions.Dispose();
            _velocities.Dispose();
            _neighbours.Dispose();
            _neighbourCounts.Dispose();
            _baseOffsets.Dispose();
            _worldForces.Dispose();
            Destroy(Mesh);
        }

        protected virtual void UpdateMesh()
        {
            Vector3[] vertices = Mesh.vertices;
            for (int vertexIndex = 0; vertexIndex < VertexCount; vertexIndex++)
            {
                vertices[vertexIndex].y = _positions[vertexIndex];
            }
            Mesh.vertices = vertices;

            Mesh.RecalculateNormals();
            Mesh.RecalculateTangents();
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
        protected virtual void LoadMeshData()
        {
            Vector3[] vertices = Mesh.vertices;

            // Set initial positions
            for (int vertexIndex = 0; vertexIndex < VertexCount; vertexIndex++)
            {
                _positions[vertexIndex] = vertices[vertexIndex].y;
            }

            // Load Neighbour Data
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
