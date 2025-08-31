using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace Assets.WaterSurface
{
    /// <summary>
    /// Simulates a water surface that responds to Rigidbodies
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WaterSurface : MonoBehaviour
    {
        [Header("Surface Settings")]
        /// <summary>
        /// The size of the water surface in units.
        /// </summary>
        [SerializeField]
        [Tooltip("The size of the water surface in units.")]
        private Vector2Int _size = Vector2Int.one * 10;

        /// <summary>
        /// The depth in units at which Rigidbodies still influence the surface.
        /// </summary>
        [SerializeField]
        [Tooltip("The depth in units at which Rigidbodies still influence the surface.")]
        [Min(0.01f)]
        private float _depth = 1f;

        public float Depth
        {
            get => _depth;
            set
            {
                _depth = value;
                BoxColliderComponent.center = Vector3.down * _depth * 0.5f;
                BoxColliderComponent.size = new(_size.x, _depth, _size.y);
            }
        }

        /// <summary>
        /// The number of simulated springs per unit.
        /// </summary>
        [SerializeField]
        [Tooltip("The number of simulated springs per unit.")]
        [Min(1)]
        private int _springDensity = 4;

        [Header("Simulation Settings")]
        /// <summary>
        /// The number of batches used for parallel for jobs.
        /// </summary>
        [SerializeField]
        [Min(1)]
        private int InnerLoopBatchCount = 64;

        /// <summary>
        /// The density of the liquid in kg/unit³.
        /// </summary>
        [Tooltip("The density of the liquid in kg/unit³.")]
        [Min(0.001f)]
        public float Density = 1f;

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

        /// <summary>
        /// The number of simulation steps. This allows waves to propagate quicker, but is performance heavy.
        /// </summary>
        [Tooltip("The number of times the simulation runs. Makes waves propagate more quickly.")]
        [Min(1)]
        public int Steps = 1;

        /// <summary>
        /// The mesh filter of which the mesh will be generated and simulated
        /// </summary>
        private MeshFilter MeshFilterComponent;

        /// <summary>
        /// The trigger that will react to rigidbodies
        /// </summary>
        private BoxCollider BoxColliderComponent;

        // Jobs
        private BaseOffsetJob _baseOffsetJob;
        private DisplacementJob _displacementJob;

        // JobHandles
        private JobHandle _baseOffsetJobHandle;
        private JobHandle _displacementJobHandle;

        // Job Variables
        private int _springCount;
        private NativeArray<int> _springGridSize;

        /// <summary>
        /// Time.deltaTime * SimulationSpeed
        /// </summary>
        private NativeArray<float> _adjustedDeltaTime;

        /// <summary>
        /// Dampening/(2 * Mass)
        /// </summary>
        private NativeArray<float> _alpha;

        /// <summary>
        /// The angular frequency of the dampened springs.
        /// </summary>
        private NativeArray<float> _angularFrequency;

        private NativeArray<float> _springPositions;
        private NativeArray<float> _springVelocities;
        private NativeArray<float> _springBaseOffsets;

        private float _timeSinceUpdate = 0f;

        private void Awake()
        {
            MeshFilterComponent = GetComponent<MeshFilter>();

            BoxColliderComponent = gameObject.AddComponent<BoxCollider>();
            BoxColliderComponent.isTrigger = true;
            BoxColliderComponent.center = Vector3.down * _depth * 0.5f;
            BoxColliderComponent.size = new(_size.x, _depth, _size.y);

            Vector2Int springGridSize = _size * _springDensity + Vector2Int.one;
            _springCount = springGridSize.x * springGridSize.y;

            _springGridSize = new(2, Allocator.Persistent);
            _springGridSize[0] = springGridSize.x;
            _springGridSize[1] = springGridSize.y;

            _adjustedDeltaTime = new(1, Allocator.Persistent);
            _alpha = new(1, Allocator.Persistent);
            _angularFrequency = new(1, Allocator.Persistent);

            _springPositions = new NativeArray<float>(_springCount, Allocator.Persistent);
            _springVelocities = new NativeArray<float>(_springCount, Allocator.Persistent);
            _springBaseOffsets = new NativeArray<float>(_springCount, Allocator.Persistent);

            _baseOffsetJob = new BaseOffsetJob
            {
                SpringGridSize = _springGridSize,
                Positions = _springPositions,
                BaseOffsets = _springBaseOffsets
            };

            _displacementJob = new DisplacementJob
            {
                AdjustedDeltaTime = _adjustedDeltaTime,
                Alpha = _alpha,
                AngularFrequency = _angularFrequency,
                BaseOffsets = _springBaseOffsets,
                Positions = _springPositions,
                Velocities = _springVelocities
            };
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < Steps; i++)
            {
                _adjustedDeltaTime[0] = Time.fixedDeltaTime * SimulationSpeed / Steps;
                float mass = Density / _springDensity / _springDensity * Depth;
                _alpha[0] = Dampening / (2f * mass);
                _angularFrequency[0] = Mathf.Sqrt(SpringConstant / mass - (_alpha[0] * _alpha[0]));
                _baseOffsetJobHandle = _baseOffsetJob.Schedule(_springCount, InnerLoopBatchCount);
                _displacementJobHandle = _displacementJob.Schedule(
                    _springCount,
                    InnerLoopBatchCount,
                    _baseOffsetJobHandle
                );
                Awaitable.WaitForSecondsAsync(Time.fixedDeltaTime / Steps);

                _baseOffsetJobHandle.Complete();
                _displacementJobHandle.Complete();
            }
            UpdateMesh();
            _timeSinceUpdate = 0f;
        }

        private void Update()
        {
            if (_timeSinceUpdate > 0)
            {
                UpdateMesh();
            }
            _timeSinceUpdate += Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.bounds.TryGetOverlap(BoxColliderComponent.bounds, out Bounds overlap))
            {
                return;
            }
            if (!other.TryGetComponent<Rigidbody>(out Rigidbody rigidbody))
            {
                return;
            }

            Vector3 localMin = transform.InverseTransformPoint(overlap.min);
            Vector3 localMax = transform.InverseTransformPoint(overlap.max);

            Vector2 surfaceExtents = (Vector2)_size * 0.5f;

            int startColumn = Math.Max(
                (int)Mathf.Floor((localMin.x + surfaceExtents.x) * _springDensity),
                0
            );
            int startRow = Math.Max(
                (int)Mathf.Floor((localMin.z + surfaceExtents.y) * _springDensity),
                0
            );

            int endColumn = Math.Min(
                (int)Mathf.Ceil((localMax.x + surfaceExtents.x) * _springDensity),
                _springGridSize[0] - 1
            );
            int endRow = Math.Min(
                (int)Mathf.Ceil((localMax.z + surfaceExtents.y) * _springDensity),
                _springGridSize[1] - 1
            );

            for (int row = startRow; row <= endRow; row++)
            {
                int rowIndex = row * _springGridSize[0];
                for (int column = startColumn; column <= endColumn; column++)
                {
                    int index = rowIndex + column;
                    _springVelocities[index] += rigidbody.linearVelocity.y;
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.bounds.TryGetOverlap(BoxColliderComponent.bounds, out Bounds overlap))
            {
                return;
            }
            if (!other.TryGetComponent<Rigidbody>(out Rigidbody rigidbody))
            {
                return;
            }
            // Buoyancy
            rigidbody.AddForce(-Density * overlap.Volume() * Physics.gravity, ForceMode.Force);

            // Resistance
            rigidbody.AddForce(
                new(
                    -0.5f
                        * Density
                        * Mathf.Sign(rigidbody.linearVelocity.x)
                        * rigidbody.linearVelocity.x
                        * rigidbody.linearVelocity.x
                        * overlap.size.y
                        * overlap.size.z,
                    -0.5f
                        * Density
                        * Mathf.Sign(rigidbody.linearVelocity.y)
                        * rigidbody.linearVelocity.y
                        * rigidbody.linearVelocity.y
                        * overlap.size.x
                        * overlap.size.z,
                    -0.5f
                        * Density
                        * Mathf.Sign(rigidbody.linearVelocity.z)
                        * rigidbody.linearVelocity.z
                        * rigidbody.linearVelocity.z
                        * overlap.size.x
                        * overlap.size.y
                ),
                ForceMode.Force
            );
        }

        private void OnDestroy()
        {
            if (!_baseOffsetJobHandle.IsCompleted)
            {
                _baseOffsetJobHandle.Complete();
            }
            if (!_displacementJobHandle.IsCompleted)
            {
                _displacementJobHandle.Complete();
            }
            _springGridSize.Dispose();
            _adjustedDeltaTime.Dispose();
            _alpha.Dispose();
            _angularFrequency.Dispose();
            _springPositions.Dispose();
            _springVelocities.Dispose();
            _springBaseOffsets.Dispose();
        }

#if UNITY_EDITOR
        private async void OnValidate()
        {
            // Min size in either direction should be 0
            if (_size.x < 0)
            {
                _size.x = 0;
                return;
            }
            if (_size.y < 0)
            {
                _size.y = 0;
                return;
            }

            // waiting since the sharedMesh of the MeshFilter can not be assigned during OnValidate.
            await Awaitable.NextFrameAsync();

            GetComponent<MeshFilter>().sharedMesh = GenerateMesh();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new(0, -0.5f * _depth, 0), new(_size.x, _depth, _size.y));
        }
#endif

        private void UpdateMesh()
        {
            float t = _timeSinceUpdate / Time.fixedDeltaTime;

            Vector3[] vertices = MeshFilterComponent.sharedMesh.vertices;
            for (int v = 0; v < _springCount; v++)
            {
                vertices[v].y = Mathf.Lerp(
                    vertices[v].y,
                    _springPositions[v] - _springBaseOffsets[v],
                    t
                );
            }
            MeshFilterComponent.sharedMesh.vertices = vertices;
            MeshFilterComponent.sharedMesh.RecalculateNormals();
            MeshFilterComponent.sharedMesh.RecalculateTangents();
        }

        private Mesh GenerateMesh()
        {
            Mesh mesh = new();
            int springColumns = _springDensity * _size.x + 1;
            int springRows = _springDensity * _size.y + 1;
            int springCount = springRows * springColumns;

            Vector2 extents = (Vector2)_size * 0.5f;
            float cellSize = 1 / (float)_springDensity;

            // Calculate vertex positions
            Vector3[] vertices = new Vector3[springCount];
            for (int y = 0; y < springRows; y++)
            {
                int row = y * springColumns;
                for (int x = 0; x < springColumns; x++)
                {
                    int index = row + x;
                    vertices[index] = new(x * cellSize - extents.x, 0, y * cellSize - extents.y);
                }
            }
            mesh.vertices = vertices;

            // Calculate triangles
            int[] triangles = new int[6 * (springCount - springRows - springColumns + 1)];
            for (int y = 1; y < springRows; y++)
            {
                int row = y * springColumns;
                for (int x = 1; x < springColumns; x++)
                {
                    int vertexIndex = row + x;
                    int triangleIndex = (vertexIndex - springColumns - y) * 6;

                    triangles[triangleIndex] = vertexIndex;
                    triangles[triangleIndex + 1] = vertexIndex - springColumns;
                    triangles[triangleIndex + 2] = vertexIndex - springColumns - 1;
                    triangles[triangleIndex + 3] = vertexIndex;
                    triangles[triangleIndex + 4] = vertexIndex - springColumns - 1;
                    triangles[triangleIndex + 5] = vertexIndex - 1;
                }
            }
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
