using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SharpNoise.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // For parallel chunk generation

namespace VoxelGame
{
    public class World : IDisposable
    {
        // --- Nested Chunk Class ---
        private class Chunk : IDisposable
        {
            public const int ChunkSize = 16; // Size of chunk in X and Z dimensions
            public const int ChunkHeight = 128; // Max height of a chunk column

            public Vector2i ChunkCoords { get; } // (ChunkX, ChunkZ)
            public Vector3 WorldOffset => new Vector3(ChunkCoords.X * ChunkSize, 0, ChunkCoords.Y * ChunkSize);

            private List<Vector3> _voxelPositions = new();
            private List<Vector3> _voxelColors = new(); // Could optimize later to not store this if colors are procedural

            private int _vao = 0;
            private int _vbo = 0;
            private int _vertexCount = 0;
            private bool _isDirty = true; // Needs buffer rebuild
            internal bool _isGenerating = false; // Changed to internal
            internal bool _isInitialized = false; // Changed to internal

            private readonly World _parentWorld; // Reference to parent world for noise, etc.

            public Chunk(Vector2i coords, World parentWorld)
            {
                ChunkCoords = coords;
                _parentWorld = parentWorld;
            }

            public bool IsReadyToDraw => _isInitialized && !_isDirty && _vao != 0;
            internal bool IsDirty => _isDirty; // Add internal getter

            // Add internal getter for voxel positions
            internal List<Vector3> GetVoxelPositions() => _voxelPositions;

            // Generates terrain data for this chunk
            public void GenerateTerrain()
            {
                if (_isGenerating || _isInitialized) return; // Don't regenerate if already done or in progress
                _isGenerating = true;

                _voxelPositions.Clear();
                _voxelColors.Clear(); // Clear colors too

                Vector3 grassColor = new(0.0f, 0.8f, 0.1f);
                Vector3 dirtColor = new(0.6f, 0.4f, 0.2f);
                Vector3 stoneColor = new(0.5f, 0.5f, 0.5f);

                for (int x = 0; x < ChunkSize; x++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        int worldX = ChunkCoords.X * ChunkSize + x;
                        int worldZ = ChunkCoords.Y * ChunkSize + z;

                        // Use noise module from parent world
                        double noiseValue = _parentWorld._noiseModule.GetValue(worldX * World.NoiseScale, 0, worldZ * World.NoiseScale);
                        int height = World.BaseHeight + (int)Math.Round((noiseValue + 1.0) / 2.0 * World.TerrainAmplitude);

                        // Cache height in parent world's map
                        _parentWorld._terrainHeightMap[(worldX, worldZ)] = height;

                        // Generate column of voxels within chunk height limits
                        for (int y = World.BaseHeight - 5; y <= height && y < ChunkHeight; y++)
                        {
                            // Store position relative to chunk origin for buffer efficiency? No, keep world coords for now.
                            Vector3 blockPos = new(worldX, y, worldZ);
                            _voxelPositions.Add(blockPos);

                            // Determine color based on height
                            Vector3 color;
                            if (y == height) color = grassColor;
                            else if (y > height - 3) color = dirtColor; // Dirt layer
                            else color = stoneColor; // Stone below dirt
                            _voxelColors.Add(color);
                        }
                    }
                }
                _isDirty = true; // Mark for buffer update
                _isGenerating = false;
                _isInitialized = true; // Mark as generated
                // Console.WriteLine($"Generated terrain for chunk {ChunkCoords}");
            }

            // Sets up OpenGL buffers for this chunk
            public void SetupBuffers(float[] cubeVertices, int vertexStride)
            {
                if (!_isDirty || !_isInitialized || _isGenerating) return; // Only setup if dirty and initialized

                if (!_voxelPositions.Any())
                {
                    // Chunk is empty (e.g., all air), clean up old buffers if they exist
                    DisposeBuffers();
                    _isDirty = false;
                    return;
                }

                List<float> vertexData = new List<float>();
                int cubeVertexCount = cubeVertices.Length / vertexStride;

                for (int i = 0; i < _voxelPositions.Count; i++)
                {
                    Vector3 pos = _voxelPositions[i];
                    // Vector3 color = _voxelColors[i]; // Use per-block color if needed

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

                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);

                _isDirty = false; // Buffers are now up-to-date
                // Console.WriteLine($"Setup buffers for chunk {ChunkCoords}, Vertices: {_vertexCount}");
            }

            public void Draw()
            {
                if (!IsReadyToDraw) return;

                GL.BindVertexArray(_vao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
                GL.BindVertexArray(0);
            }

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

            public void Dispose()
            {
                DisposeBuffers();
                // No GC.SuppressFinalize needed if not overriding finalizer
            }
        }
        // --- End Nested Chunk Class ---


        // Voxel/Cube Data (shared across chunks)
        private float[] _cubeVertices = null!;
        private int _cubeVertexCount = 0;
        private const int _vertexStride = 9; // 3 Pos + 3 Color + 3 Normal

        // World Generation Parameters
        private Perlin _noiseModule = new();
        // private const int WorldSize = 64; // No longer a fixed size limit
        public const float NoiseScale = 0.04f; // Made public for Chunk access
        public const int NoiseOctaves = 4; // Made public for Chunk access
        public const float TerrainAmplitude = 15f; // Increased amplitude
        public const int BaseHeight = 50; // Base ground level

        // Chunk Management
        private Dictionary<(int, int), Chunk> _activeChunks = new();
        private List<(int, int)> _chunksToGenerate = new(); // Changed to List for priority
        private List<(int, int)> _chunksToBuild = new(); // Changed to List for priority
        private const int RenderDistance = 4; // Changed from 8 to 4
        private const int LoadDistance = RenderDistance + 2; // Load distance slightly larger to preload

        // Height Map (sparse, populated by chunks)
        private Dictionary<(int x, int z), int> _terrainHeightMap = new();

        // Fog Parameters
        public Vector3 FogColor { get; set; } = new Vector3(0.5f, 0.75f, 0.9f); // Match clear color
        public float FogDensity { get; set; } = 0.015f; // Exponential fog density
        public float FogGradient { get; set; } = 2.5f; // Controls how quickly fog thickens

        public World()
        {
            _noiseModule.Seed = new Random().Next();
            _noiseModule.OctaveCount = NoiseOctaves;
            _noiseModule.Persistence = 0.5; // Default persistence
            _noiseModule.Lacunarity = 2.0; // Default lacunarity
        }

        public void Initialize()
        {
            Console.WriteLine("Initializing World...");
            GenerateCubeVertices(); // Generate the template cube vertices once
            // Initial chunk loading will happen in the first Update call based on player pos
            Console.WriteLine("World Initialized. Waiting for first update to load initial chunks.");
        }

        // Update method called each frame to manage chunks
        public void Update(Vector3 playerPosition)
        {
            Vector2i playerChunkCoords = GetChunkCoords(playerPosition);
            // Console.WriteLine($"Player at {playerPosition}, Chunk: {playerChunkCoords}");

            // --- Identify Chunks to Load/Unload ---
            HashSet<(int, int)> requiredChunks = new HashSet<(int, int)>();
            List<(int, int)> chunksToUnload = new List<(int, int)>();
            List<(int, int)> newChunksToLoad = new List<(int, int)>(); // Track newly required chunks

            // Prioritize player's current chunk
            requiredChunks.Add((playerChunkCoords.X, playerChunkCoords.Y));

            for (int x = playerChunkCoords.X - LoadDistance; x <= playerChunkCoords.X + LoadDistance; x++)
            {
                for (int z = playerChunkCoords.Y - LoadDistance; z <= playerChunkCoords.Y + LoadDistance; z++)
                {
                    requiredChunks.Add((x, z));
                }
            }

            // Find chunks currently active but no longer required
            foreach (var loadedCoord in _activeChunks.Keys)
            {
                if (!requiredChunks.Contains(loadedCoord))
                {
                    chunksToUnload.Add(loadedCoord);
                }
            }

            // --- Unload Far Chunks ---
            foreach (var coord in chunksToUnload)
            {
                if (_activeChunks.TryGetValue(coord, out var chunk))
                {
                    chunk.Dispose(); // Dispose buffers
                    _activeChunks.Remove(coord);
                    // Console.WriteLine($"Unloaded chunk {coord}");
                }
                _chunksToGenerate.Remove(coord); // Cancel generation if pending
                _chunksToBuild.Remove(coord);    // Cancel build if pending
            }

            // --- Identify and Queue New Chunks ---
            // Add player chunk first if it needs loading
            var playerCoordTuple = (playerChunkCoords.X, playerChunkCoords.Y);
            if (!_activeChunks.ContainsKey(playerCoordTuple))
            {
                 if (!_chunksToGenerate.Contains(playerCoordTuple)) // Avoid duplicates if already queued
                 {
                    newChunksToLoad.Add(playerCoordTuple);
                 }
            }

            // Add other required chunks
            foreach (var coord in requiredChunks)
            {
                if (coord != playerCoordTuple && !_activeChunks.ContainsKey(coord))
                {
                    if (!_chunksToGenerate.Contains(coord)) // Avoid duplicates
                    {
                        newChunksToLoad.Add(coord);
                    }
                }
            }

            // Add newly identified chunks to the generation queue (player chunk is first)
            foreach (var coord in newChunksToLoad)
            {
                 var newChunk = new Chunk(new Vector2i(coord.Item1, coord.Item2), this);
                 _activeChunks.Add(coord, newChunk);
                 _chunksToGenerate.Add(coord); // Add to generation queue
                 // Console.WriteLine($"Queued chunk {coord} for generation");
            }

            // --- Process Generation Queue (Prioritizes player chunk if first in list) ---
            var generatedCoords = new List<(int, int)>(); // Track coords generated this frame
            int maxGeneratePerFrame = 4;
            int generatedThisFrame = 0;

            // Process in order (player chunk should be near the start if newly added)
            for (int i = 0; i < _chunksToGenerate.Count && generatedThisFrame < maxGeneratePerFrame; i++)
            {
                var coord = _chunksToGenerate[i];

                if (_activeChunks.TryGetValue(coord, out var chunk))
                {
                    // Simple sync generation:
                    chunk.GenerateTerrain();
                    if (chunk._isInitialized) // Check if generation actually happened
                    {
                        // Prioritize building the player chunk
                        if (coord == playerCoordTuple)
                        {
                            _chunksToBuild.Insert(0, coord); // Add to front of build queue
                        }
                        else
                        {
                            _chunksToBuild.Add(coord); // Add to end of build queue
                        }
                        generatedCoords.Add(coord);
                        generatedThisFrame++;
                    }
                    else
                    {
                        // If generation didn't happen (e.g., already initialized), still remove from generate queue
                        generatedCoords.Add(coord);
                    }
                }
                else
                {
                    // Chunk might have been unloaded before generation started
                    generatedCoords.Add(coord); // Remove from queue anyway
                }
            }

            // Remove generated chunks from the generation queue
            // Iterate backwards to avoid index issues when removing
            for (int i = _chunksToGenerate.Count - 1; i >= 0; i--)
            {
                if (generatedCoords.Contains(_chunksToGenerate[i]))
                {
                    _chunksToGenerate.RemoveAt(i);
                }
            }

            // --- Process Build Queue (Prioritizes player chunk if first in list) ---
            var builtCoords = new List<(int, int)>(); // Track coords built this frame
            int maxBuildPerFrame = 4;
            int builtThisFrame = 0;

            // Process in order (player chunk should be near the start if newly generated)
            for (int i = 0; i < _chunksToBuild.Count && builtThisFrame < maxBuildPerFrame; i++)
            {
                var coord = _chunksToBuild[i];

                if (_activeChunks.TryGetValue(coord, out var chunk))
                {
                    if (chunk.IsDirty) // Check if it still needs building
                    {
                        chunk.SetupBuffers(_cubeVertices, _vertexStride);
                        builtThisFrame++;
                    }
                    // Always remove from build queue once processed, even if not dirty anymore
                    builtCoords.Add(coord);
                }
                else
                {
                     // Chunk might have been unloaded
                     builtCoords.Add(coord); // Remove from queue
                }
            }

            // Remove built chunks from the build queue
            // Iterate backwards
            for (int i = _chunksToBuild.Count - 1; i >= 0; i--)
            {
                if (builtCoords.Contains(_chunksToBuild[i]))
                {
                    _chunksToBuild.RemoveAt(i);
                }
            }
        }

        // Method to get voxel positions relevant for collision near a point
        public List<Vector3> GetNearbyVoxelPositions(Vector3 center, float radius)
        {
            List<Vector3> nearbyVoxels = new List<Vector3>();
            float radiusSq = radius * radius;

            // Determine the range of chunks to check based on radius
            int centerChunkX = (int)Math.Floor(center.X / Chunk.ChunkSize);
            int centerChunkZ = (int)Math.Floor(center.Z / Chunk.ChunkSize);
            int chunkRadius = (int)Math.Ceiling(radius / Chunk.ChunkSize) + 1; // Check slightly larger area

            for (int cx = centerChunkX - chunkRadius; cx <= centerChunkX + chunkRadius; cx++)
            {
                for (int cz = centerChunkZ - chunkRadius; cz <= centerChunkZ + chunkRadius; cz++)
                {
                    if (_activeChunks.TryGetValue((cx, cz), out var chunk) && chunk.IsReadyToDraw)
                    {
                        // A simple approach: add all voxels from nearby ready chunks.
                        // Optimization: Could filter voxels within the chunk that are actually close to 'center'.
                        // For now, let the CollisionManager handle the precise distance check.
                        nearbyVoxels.AddRange(chunk.GetVoxelPositions());
                    }
                }
            }
            return nearbyVoxels;
        }

        public int GetHeight(int worldX, int worldZ)
        {
            // First, check the cache
            if (_terrainHeightMap.TryGetValue((worldX, worldZ), out int height))
            {
                return height;
            }

            // If not cached, calculate on the fly using noise (could be slow if called often)
            // Consider if this should trigger chunk generation or just provide the value
            // For physics, just providing the value is likely best.
            double noiseValue = _noiseModule.GetValue(worldX * NoiseScale, 0, worldZ * NoiseScale);
            int calculatedHeight = BaseHeight + (int)Math.Round((noiseValue + 1.0) / 2.0 * TerrainAmplitude);
            // Optionally cache this calculated value? Be careful about cache size.
            // _terrainHeightMap[(worldX, worldZ)] = calculatedHeight; // Uncomment to cache on-the-fly results
            // Console.WriteLine($"Height cache miss for ({worldX},{worldZ}). Calculated: {calculatedHeight}");
            return calculatedHeight;
        }

        private Vector2i GetChunkCoords(Vector3 worldPosition)
        {
            int chunkX = (int)Math.Floor(worldPosition.X / Chunk.ChunkSize);
            int chunkZ = (int)Math.Floor(worldPosition.Z / Chunk.ChunkSize);
            return new Vector2i(chunkX, chunkZ);
        }


        private void GenerateCubeVertices()
        {
            // ... existing code ...
            // Stride is now 9: 3 Pos, 3 Color, 3 Normal
            _cubeVertices = new float[]
            {
                // Position          Color           Normal
                // Front face (+Z) - Normal (0, 0, 1) - Red
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                -0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,

                // Back face (-Z) - Normal (0, 0, -1) - Green
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,

                // Left face (-X) - Normal (-1, 0, 0) - Blue
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,

                // Right face (+X) - Normal (1, 0, 0) - Yellow
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,

                // Bottom face (-Y) - Normal (0, -1, 0) - Cyan
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,

                // Top face (+Y) - Normal (0, 1, 0) - Magenta
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f
            };
            _cubeVertexCount = _cubeVertices.Length / _vertexStride;
            CheckGLError("World.GenerateCubeVertices");
        }

        // Removed GenerateSimpleWorld - generation is per-chunk now
        // Removed SetupVoxelBuffers - setup is per-chunk now

        public void Draw(Shader shader, Camera camera)
        {
            shader.Use();

            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();
            // Model matrix is identity for chunks as vertices are in world space
            Matrix4 model = Matrix4.Identity;

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetMatrix4("model", model);
            shader.SetVector3("viewPos", camera.Position);

            // Set Fog Uniforms
            shader.SetVector3("fogColor", FogColor);
            shader.SetFloat("fogDensity", FogDensity); // Use SetFloat
            shader.SetFloat("fogGradient", FogGradient); // Use SetFloat

            CheckGLError("World.Draw SetUniforms");

            // Determine visible chunks based on RenderDistance
            Vector2i playerChunkCoords = GetChunkCoords(camera.Position);
            int drawnChunkCount = 0;
            foreach (var kvp in _activeChunks)
            {
                var coord = kvp.Key;
                var chunk = kvp.Value;

                // Check if chunk is within render distance
                int dx = Math.Abs(coord.Item1 - playerChunkCoords.X);
                int dz = Math.Abs(coord.Item2 - playerChunkCoords.Y);

                if (dx <= RenderDistance && dz <= RenderDistance)
                {
                    if (chunk.IsReadyToDraw) // Only draw if buffers are ready
                    {
                        // Frustum culling could be added here for optimization
                        chunk.Draw();
                        drawnChunkCount++;
                    }
                }
            }
             // Console.WriteLine($"Drew {drawnChunkCount} chunks.");
            CheckGLError("World.Draw DrawChunks");
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing World...");
            foreach (var chunk in _activeChunks.Values)
            {
                chunk.Dispose();
            }
            _activeChunks.Clear();
            _terrainHeightMap.Clear();
            // Note: _cubeVertices is managed memory, GC handles it.
            GC.SuppressFinalize(this);
            CheckGLError("World.Dispose");
            Console.WriteLine("World Disposed.");
        }

        // Helper to check for errors
        private static void CheckGLError(string stage)
        {
            // ... existing code ...
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
                // System.Diagnostics.Debugger.Break(); // Optional: Break in debugger
            }
        }
    }
}
