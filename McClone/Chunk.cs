using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelGame
{
    // Moved from World.cs
    public class Chunk : IDisposable
    {
        public const int ChunkSize = 16; // Size of chunk in X and Z dimensions
        public const int ChunkHeight = 128; // Max height of a chunk column

        public Vector2i ChunkCoords { get; } // (ChunkX, ChunkZ)
        public Vector3 WorldOffset => new Vector3(ChunkCoords.X * ChunkSize, 0, ChunkCoords.Y * ChunkSize);

        private List<Vector3> _voxelPositions = new();

        private int _vao = 0;
        private int _vbo = 0;
        private int _vertexCount = 0;
        private volatile bool _isDirty = true; // Needs buffer rebuild (volatile for thread safety)
        internal volatile bool _isGenerating = false; // volatile for thread safety
        internal volatile bool _isInitialized = false; // volatile for thread safety

        private readonly World _parentWorld; // Reference to parent world for noise, etc.

        public Chunk(Vector2i coords, World parentWorld)
        {
            ChunkCoords = coords;
            _parentWorld = parentWorld;
        }

        public bool IsReadyToDraw => _isInitialized && !_isDirty && _vao != 0;
        internal bool IsDirty => _isDirty; // Add internal getter

        // Add internal getter for voxel positions
        internal List<Vector3> GetVoxelPositions() => _voxelPositions; // Note: Access should be synchronized if modified after generation

        // Generates terrain data for this chunk (Called from background thread)
        public void GenerateTerrain()
        {
            // Simple check first, then lock for finer-grained control
            if (_isInitialized || _isGenerating) return;

            lock (this) // Lock the chunk instance during generation
            {
                // Double-check inside lock
                if (_isInitialized || _isGenerating) return;
                _isGenerating = true;
            }

            try
            {
                _voxelPositions.Clear();

                for (int x = 0; x < ChunkSize; x++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        int worldX = ChunkCoords.X * ChunkSize + x;
                        int worldZ = ChunkCoords.Y * ChunkSize + z;

                        // Use noise module from parent world (thread-safe read assumed for SharpNoise)
                        double noiseValue = _parentWorld.GetNoiseValue(worldX, worldZ); // Use helper method
                        int height = World.BaseHeight + (int)Math.Round((noiseValue + 1.0) / 2.0 * World.TerrainAmplitude);

                        // Cache height in parent world's map (needs thread-safe dictionary)
                        _parentWorld.SetTerrainHeight(worldX, worldZ, height);

                        // Generate column of voxels within chunk height limits
                        for (int y = World.BaseHeight - 5; y <= height && y < ChunkHeight; y++)
                        {
                            Vector3 blockPos = new(worldX, y, worldZ);
                            _voxelPositions.Add(blockPos);
                        }
                    }
                }
            }
            finally // Ensure flags are reset even if an error occurs
            {
                 // No lock needed for volatile writes if atomicity isn't required across multiple fields
                _isDirty = true; // Mark for buffer update (main thread will handle)
                _isGenerating = false;
                _isInitialized = true; // Mark as generated
            }
            // Console.WriteLine($"Generated terrain for chunk {ChunkCoords} on thread {Thread.CurrentThread.ManagedThreadId}");
        }

        // Sets up OpenGL buffers for this chunk (MUST be called from the main/OpenGL thread)
        public void SetupBuffers()
        {
            if (!_isDirty || !_isInitialized || _isGenerating) return; // Only setup if dirty and initialized

            if (!_voxelPositions.Any())
            {
                // Chunk is empty (e.g., all air), clean up old buffers if they exist
                DisposeBuffers(); // Ensure cleanup happens on the correct thread
                _isDirty = false;
                return;
            }

            List<float> vertexData = new List<float>();
            float[] cubeVertices = CubeData.Vertices; // Get from static class
            int vertexStride = CubeData.VertexStride; // Get from static class

            for (int i = 0; i < _voxelPositions.Count; i++)
            {
                Vector3 pos = _voxelPositions[i];

                for (int j = 0; j < cubeVertices.Length; j += vertexStride)
                {
                    // Position (already in world space)
                    vertexData.Add(cubeVertices[j + 0] + pos.X);
                    vertexData.Add(cubeVertices[j + 1] + pos.Y);
                    vertexData.Add(cubeVertices[j + 2] + pos.Z);
                    // Color (using predefined cube face colors for now)
                    vertexData.Add(cubeVertices[j + 3]);
                    vertexData.Add(cubeVertices[j + 4]);
                    vertexData.Add(cubeVertices[j + 5]);
                    // Normal
                    vertexData.Add(cubeVertices[j + 6]);
                    vertexData.Add(cubeVertices[j + 7]);
                    vertexData.Add(cubeVertices[j + 8]);
                    // TexCoord (New)
                    vertexData.Add(cubeVertices[j + 9]);
                    vertexData.Add(cubeVertices[j + 10]);
                }
            }

            _vertexCount = vertexData.Count / vertexStride;

            if (_vertexCount == 0)
            {
                DisposeBuffers(); // Clean up if no vertices generated
                _isDirty = false;
                return;
            }

            // --- Create/Update VAO and VBO ---
            if (_vao == 0) _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            if (_vbo == 0) _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Count * sizeof(float), vertexData.ToArray(), BufferUsageHint.StaticDraw); // Use StaticDraw for now

            // --- Configure Vertex Attributes ---
            int strideBytes = vertexStride * sizeof(float);
            // Position (location = 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, strideBytes, 0);
            // Color (location = 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, strideBytes, 3 * sizeof(float));
            // Normal (location = 2)
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, strideBytes, 6 * sizeof(float));
            // TexCoord (location = 3 - New)
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, strideBytes, 9 * sizeof(float)); // 2 floats, offset 9

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            _isDirty = false; // Buffers are now up-to-date
            // Console.WriteLine($"Setup buffers for chunk {ChunkCoords}, Vertices: {_vertexCount}");
        }

        // Draw method (MUST be called from the main/OpenGL thread)
        public void Draw()
        {
            if (!IsReadyToDraw) return;

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
            GL.BindVertexArray(0);
        }

        // Dispose OpenGL buffers (MUST be called from the main/OpenGL thread)
        private void DisposeBuffers()
        {
             if (_vbo != 0)
             {
                 GL.DeleteBuffer(_vbo);
                 _vbo = 0;
             }
             if (_vao != 0)
             {
                 GL.DeleteVertexArray(_vao);
                 _vao = 0;
             }
             _vertexCount = 0;
             _isInitialized = false; // Needs regeneration if loaded again
             _isDirty = true;
             // Console.WriteLine($"Disposed buffers for chunk {ChunkCoords}");
        }

        // Dispose method (MUST be called from the main/OpenGL thread)
        public void Dispose()
        {
            DisposeBuffers();
            // No GC.SuppressFinalize needed if not overriding finalizer
        }
    }
}
