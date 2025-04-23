using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SharpNoise.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VoxelGame
{
    public class World : IDisposable
    {
        public List<Vector3> VoxelPositions { get; private set; } = new();
        private List<Vector3> _voxelColors = new(); // Keep private if only used internally for buffer setup
        private Dictionary<(int x, int z), int> _terrainHeightMap = new();

        private int _voxelVao;
        private int _voxelVbo;
        private float[] _cubeVertices = null!;
        private int _cubeVertexCount = 0;

        // World Generation Parameters
        private Perlin _noiseModule = new();
        private const int WorldSize = 64;
        private const float NoiseScale = 0.04f;
        private const int NoiseOctaves = 4;
        private const float TerrainAmplitude = 8f;
        private const int BaseHeight = 0;

        public World()
        {
            // Initialize noise module
            _noiseModule.Seed = new Random().Next();
            _noiseModule.OctaveCount = NoiseOctaves;
        }

        public void Initialize()
        {
            Console.WriteLine("Initializing World...");
            GenerateCubeVertices();
            GenerateSimpleWorld();
            SetupVoxelBuffers();
            Console.WriteLine("World Initialized.");
        }

        public int GetHeight(int x, int z)
        {
            if (_terrainHeightMap.TryGetValue((x, z), out int height))
            {
                return height;
            }
            // Return a default or handle the case where height is not found
            // Maybe return BaseHeight or query noise again? For now, returning a low value.
            Console.WriteLine($"Warning: Height not found in map for ({x},{z}). Returning {BaseHeight - 5}.");
            return BaseHeight - 5; // Or some other indicator of missing data
        }


        private void GenerateCubeVertices()
        {
             _cubeVertices = new float[]
            {
                // Position          Color (using distinct face colors for debugging)
                // Front face (Red)
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                -0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,

                // Back face (Green)
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,

                // Left face (Blue)
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 1.0f,
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f,

                // Right face (Yellow)
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 1.0f, 1.0f, 0.0f,

                // Bottom face (Cyan)
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,

                // Top face (Magenta)
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f,
                -0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f,
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f
            };
            _cubeVertexCount = _cubeVertices.Length / 6; // 6 floats per vertex (3 pos, 3 color)
            CheckGLError("World.GenerateCubeVertices");
        }

        private void GenerateSimpleWorld()
        {
            Console.WriteLine("Generating world geometry...");
            VoxelPositions.Clear();
            _voxelColors.Clear();
            _terrainHeightMap.Clear();

            Vector3 grassColor = new(0.0f, 0.8f, 0.1f);
            Vector3 dirtColor = new(0.6f, 0.4f, 0.2f);
            Vector3 stoneColor = new(0.5f, 0.5f, 0.5f);

            for (int x = 0; x < WorldSize; x++)
            {
                for (int z = 0; z < WorldSize; z++)
                {
                    // Use noise module to get height
                    double noiseValue = _noiseModule.GetValue(x * NoiseScale, 0, z * NoiseScale);
                    int height = BaseHeight + (int)Math.Round((noiseValue + 1.0) / 2.0 * TerrainAmplitude);
                    _terrainHeightMap[(x, z)] = height; // Store height

                    // Generate column of voxels
                    for (int y = BaseHeight - 3; y <= height; y++) // Example: Generate down to 3 blocks below base
                    {
                        Vector3 blockPos = new(x, y, z);
                        VoxelPositions.Add(blockPos);

                        // Determine color based on height
                        Vector3 color;
                        if (y == height) color = grassColor;
                        else if (y > height - 3) color = dirtColor; // Dirt layer
                        else color = stoneColor; // Stone below dirt
                        _voxelColors.Add(color);
                    }
                }
            }
            Console.WriteLine($"Generated {VoxelPositions.Count} voxel positions.");
            CheckGLError("World.GenerateSimpleWorld");
        }

        private void SetupVoxelBuffers()
        {
            if (!VoxelPositions.Any())
            {
                Console.WriteLine("Skipping VBO setup - no voxel positions.");
                return;
            }

            List<float> allVertexData = new List<float>();
            for (int i = 0; i < VoxelPositions.Count; i++)
            {
                Vector3 pos = VoxelPositions[i];
                // Vector3 color = _voxelColors[i]; // Use block-specific color if needed later

                // Add all vertices for a single cube at the voxel's position
                for (int j = 0; j < _cubeVertices.Length; j += 6) // Stride is 6 (pos+color)
                {
                    // Position (offset by voxel position)
                    allVertexData.Add(_cubeVertices[j + 0] + pos.X);
                    allVertexData.Add(_cubeVertices[j + 1] + pos.Y);
                    allVertexData.Add(_cubeVertices[j + 2] + pos.Z);
                    // Color (use the pre-defined cube face colors for now)
                    allVertexData.Add(_cubeVertices[j + 3]);
                    allVertexData.Add(_cubeVertices[j + 4]);
                    allVertexData.Add(_cubeVertices[j + 5]);
                }
            }

            Console.WriteLine($"Total vertices generated for VBO: {allVertexData.Count / 6}");
            if (allVertexData.Count == 0)
            {
                Console.WriteLine("ERROR: Vertex data list is empty after processing voxels!");
                return;
            }

            // --- Create VAO and VBO ---
            _voxelVao = GL.GenVertexArray();
            GL.BindVertexArray(_voxelVao);
            CheckGLError("World.SetupVoxelBuffers BindVAO");

            _voxelVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _voxelVbo);
            CheckGLError("World.SetupVoxelBuffers Bind VBO");
            GL.BufferData(BufferTarget.ArrayBuffer, allVertexData.Count * sizeof(float), allVertexData.ToArray(), BufferUsageHint.StaticDraw);
            CheckGLError("World.SetupVoxelBuffers BufferData");

            // --- Configure Vertex Attributes ---
            // Assuming shader attributes are named "aPosition" and "aColor"
            // Position attribute (location = 0)
            GL.EnableVertexAttribArray(0); // Use layout location = 0
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            CheckGLError("World.SetupVoxelBuffers AttribPointer Pos");

            // Color attribute (location = 1)
            GL.EnableVertexAttribArray(1); // Use layout location = 1
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            CheckGLError("World.SetupVoxelBuffers AttribPointer Color");

            // Unbind
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            Console.WriteLine("Voxel buffers setup complete.");
        }

        public void Draw(Shader shader, Camera camera)
        {
            if (VoxelPositions.Count == 0 || _voxelVao == 0)
            {
                return; // Nothing to draw
            }

            shader.Use();

            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();
            Matrix4 model = Matrix4.Identity; // Model matrix is identity since vertices are already in world space

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetMatrix4("model", model);
            CheckGLError("World.Draw SetUniforms");


            int vertexCountToDraw = VoxelPositions.Count * _cubeVertexCount;
            if (vertexCountToDraw > 0)
            {
                GL.BindVertexArray(_voxelVao);
                CheckGLError("World.Draw BindVAO");
                GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCountToDraw);
                CheckGLError("World.Draw DrawArrays");
                GL.BindVertexArray(0); // Unbind VAO
            }
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_voxelVbo);
            GL.DeleteVertexArray(_voxelVao);
            GC.SuppressFinalize(this); // Suppress finalization
             CheckGLError("World.Dispose");
        }

        // Helper to check for errors (consider moving to a static utility class)
        private static void CheckGLError(string stage)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
                // System.Diagnostics.Debugger.Break(); // Optional: Break in debugger
            }
        }
    }
}
