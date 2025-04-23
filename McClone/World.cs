using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SharpNoise.Modules;
using System;
using System.Collections.Concurrent; // For concurrent collections
using System.Collections.Generic;
using System.Linq;
using System.Threading;      // For CancellationTokenSource and Thread
using System.Threading.Tasks; // For Task

namespace VoxelGame
{
    public class World : IDisposable
    {
        // --- Removed Nested Chunk Class ---

        // --- Removed Voxel/Cube Data (moved to CubeData.cs) ---

        // World Generation Parameters
        private Perlin _noiseModule = new();
        public const float NoiseScale = 0.03f;
        public const int NoiseOctaves = 6;
        public const float TerrainAmplitude = 15f; // Increased amplitude
        public const int BaseHeight = 50; // Base ground level

        // Chunk Management
        private ConcurrentDictionary<(int, int), Chunk> _activeChunks = new(); // Thread-safe dictionary
        private BlockingCollection<(int, int)> _generationQueue = new(new ConcurrentQueue<(int, int)>()); // Queue for coords needing generation
        private ConcurrentQueue<Chunk> _buildQueue = new(); // Queue for chunks ready for buffer building (on main thread)
        private const int RenderDistance = 6;
        private const int LoadDistance = RenderDistance + 2; // Load distance slightly larger to preload

        // Background Task Management
        private CancellationTokenSource _cancellationSource = new();
        private Task _generationTask = null!;

        // Height Map (sparse, populated by chunks) - Needs thread-safe access
        private ConcurrentDictionary<(int x, int z), int> _terrainHeightMap = new();

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
            CubeData.GenerateVertices(); // Generate the template cube vertices once using static class
            CheckGLError("World.Initialize CubeData");

            // Start the background generation task
            _generationTask = Task.Run(() => ProcessGenerationQueue(_cancellationSource.Token), _cancellationSource.Token);
            Console.WriteLine("World Initialized. Background chunk generation task started.");
        }

        // Method to queue the initial set of chunks needed at startup
        public void QueueInitialChunks(Vector3 startPosition)
        {
            Console.WriteLine("Queueing initial chunks...");
            Vector2i centerChunk = GetChunkCoords(startPosition);
            var initialCoords = new HashSet<(int, int)>();

            for (int x = centerChunk.X - LoadDistance; x <= centerChunk.X + LoadDistance; x++)
            {
                for (int z = centerChunk.Y - LoadDistance; z <= centerChunk.Y + LoadDistance; z++)
                {
                    initialCoords.Add((x, z));
                }
            }

            foreach (var coord in initialCoords)
            {
                if (!_activeChunks.ContainsKey(coord)) // Avoid adding if somehow already there
                {
                    var newChunk = new Chunk(new Vector2i(coord.Item1, coord.Item2), this);
                    if (_activeChunks.TryAdd(coord, newChunk))
                    {
                        _generationQueue.Add(coord); // Add to background queue
                        // Console.WriteLine($"Queued initial chunk {coord} for generation");
                    }
                }
            }
            Console.WriteLine($"Queued {initialCoords.Count} initial chunks.");
        }

        // Method to check if the initial set of chunks are ready for drawing
        public bool AreInitialChunksReady(Vector3 startPosition)
        {
            Vector2i centerChunk = GetChunkCoords(startPosition);
            for (int x = centerChunk.X - RenderDistance; x <= centerChunk.X + RenderDistance; x++) // Check render distance
            {
                for (int z = centerChunk.Y - RenderDistance; z <= centerChunk.Y + RenderDistance; z++)
                {
                    if (!_activeChunks.TryGetValue((x, z), out var chunk) || !chunk.IsReadyToDraw)
                    {
                        // Console.WriteLine($"Waiting for chunk ({x},{z})..."); // Debugging
                        return false; // Not ready yet
                    }
                }
            }
            Console.WriteLine("Initial chunks are ready!");
            return true;
        }


        // Background task method
        private void ProcessGenerationQueue(CancellationToken token)
        {
            Console.WriteLine($"Generation Task started on thread {Thread.CurrentThread.ManagedThreadId}");
            try
            {
                // Consume items from the queue as they arrive
                foreach (var coord in _generationQueue.GetConsumingEnumerable(token))
                {
                    if (token.IsCancellationRequested) break;

                    if (_activeChunks.TryGetValue(coord, out var chunk))
                    {
                        // Console.WriteLine($"Generating chunk {coord}...");
                        chunk.GenerateTerrain(); // This now happens on the background thread
                        if (chunk._isInitialized) // Check if generation completed
                        {
                            _buildQueue.Enqueue(chunk); // Add to the build queue for the main thread
                            // Console.WriteLine($"Chunk {coord} generated, queued for build.");
                        }
                    }
                    else
                    {
                        // Chunk might have been unloaded before generation started
                        // Console.WriteLine($"Chunk {coord} not found in active chunks during generation.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Generation task cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"!!!!!!!! Error in generation task: {ex.Message} !!!!!!!!!");
                Console.WriteLine(ex.StackTrace); // Print stack trace separately
            }
            finally
            {
                Console.WriteLine("Generation Task finished.");
            }
        }

        // Process the build queue (MUST be called from the main/OpenGL thread)
        public void ProcessBuildQueue()
        {
            int builtThisFrame = 0;
            // Reduce max builds per frame to lessen potential main thread spikes
            int maxBuildPerFrame = 2; // Lowered from 4

            while (builtThisFrame < maxBuildPerFrame && _buildQueue.TryDequeue(out var chunkToBuild))
            {
                 // Double check if the chunk still exists and needs building
                 // Convert Vector2i to tuple for dictionary key lookup
                 if (_activeChunks.ContainsKey((chunkToBuild.ChunkCoords.X, chunkToBuild.ChunkCoords.Y)) && chunkToBuild.IsDirty)
                 {
                    // Console.WriteLine($"Building chunk {chunkToBuild.ChunkCoords}...");
                    chunkToBuild.SetupBuffers(); // Use the parameterless version
                    builtThisFrame++;
                 }
                 // else Console.WriteLine($"Skipping build for chunk {chunkToBuild.ChunkCoords} (already built or removed).");
            }
            // if (builtThisFrame > 0) Console.WriteLine($"Built {builtThisFrame} chunks this frame.");
        }


        // Update method called each frame to manage chunks (Main Thread)
        public void Update(Vector3 playerPosition)
        {
            Vector2i playerChunkCoords = GetChunkCoords(playerPosition);

            // --- Process Build Queue (OpenGL operations) ---
            ProcessBuildQueue(); // Build buffers for generated chunks

            // --- Identify Chunks to Load/Unload ---
            HashSet<(int, int)> requiredChunks = new HashSet<(int, int)>();

            for (int x = playerChunkCoords.X - LoadDistance; x <= playerChunkCoords.X + LoadDistance; x++)
            {
                for (int z = playerChunkCoords.Y - LoadDistance; z <= playerChunkCoords.Y + LoadDistance; z++)
                {
                    requiredChunks.Add((x, z));
                }
            }

            // --- Unload Far Chunks ---
            // Iterate directly over the dictionary to avoid ToList() allocation.
            // ConcurrentDictionary allows safe iteration while modifying.
            foreach (var kvp in _activeChunks)
            {
                var loadedCoord = kvp.Key;
                if (!requiredChunks.Contains(loadedCoord))
                {
                    // TryRemove is thread-safe.
                    if (_activeChunks.TryRemove(loadedCoord, out var chunk))
                    {
                        // Console.WriteLine($"Unloading chunk {loadedCoord}");
                        chunk.Dispose(); // Dispose buffers (must happen on main thread)
                    }
                }
            }

            // --- Identify and Queue New Chunks for Generation ---
            foreach (var coord in requiredChunks)
            {
                // Use ContainsKey for check, TryAdd for adding to avoid race conditions
                if (!_activeChunks.ContainsKey(coord))
                {
                    var newChunk = new Chunk(new Vector2i(coord.Item1, coord.Item2), this);
                    if (_activeChunks.TryAdd(coord, newChunk)) // Attempt to add atomically
                    {
                        _generationQueue.Add(coord); // Add to background generation queue
                        // Console.WriteLine($"Queued chunk {coord} for generation");
                    }
                    // If TryAdd fails, another thread likely added it just before, which is fine.
                }
            }

            // --- Generation and Build processing happens in ProcessGenerationQueue (background) and ProcessBuildQueue (main thread) ---
        }


        // Method to get voxel positions relevant for collision near a point
        public List<Vector3> GetNearbyVoxelPositions(Vector3 center, float radius)
        {
            List<Vector3> nearbyVoxels = new List<Vector3>();
            // float radiusSq = radius * radius; // Not currently used

            // Determine the range of chunks to check based on radius
            int centerChunkX = (int)Math.Floor(center.X / Chunk.ChunkSize);
            int centerChunkZ = (int)Math.Floor(center.Z / Chunk.ChunkSize);
            int chunkRadius = (int)Math.Ceiling(radius / Chunk.ChunkSize) + 1; // Check slightly larger area

            for (int cx = centerChunkX - chunkRadius; cx <= centerChunkX + chunkRadius; cx++)
            {
                for (int cz = centerChunkZ - chunkRadius; cz <= centerChunkZ + chunkRadius; cz++)
                {
                    // Use TryGetValue for thread-safe access
                    if (_activeChunks.TryGetValue((cx, cz), out var chunk) && chunk.IsReadyToDraw)
                    {
                        // A simple approach: add all voxels from nearby ready chunks.
                        // Optimization: Could filter voxels within the chunk that are actually close to 'center'.
                        // For now, let the CollisionManager handle the precise distance check.
                        // Note: Accessing GetVoxelPositions might need locking if chunks could be modified after generation. Currently, they are not.
                        nearbyVoxels.AddRange(chunk.GetVoxelPositions());
                    }
                }
            }
            return nearbyVoxels;
        }

        // Thread-safe way to get height from cache
        public int GetHeight(int worldX, int worldZ)
        {
            if (_terrainHeightMap.TryGetValue((worldX, worldZ), out int height))
            {
                return height;
            }

            // Fallback: Calculate on the fly using noise
            double noiseValue = GetNoiseValue(worldX, worldZ); // Use helper
            int calculatedHeight = BaseHeight + (int)Math.Round((noiseValue + 1.0) / 2.0 * TerrainAmplitude);
            // Optionally cache this calculated value? Be careful about cache size.
            // _terrainHeightMap.TryAdd((worldX, worldZ), calculatedHeight); // Use TryAdd for thread safety
            return calculatedHeight;
        }

        // Helper for noise calculation (potentially accessed by multiple threads)
        internal double GetNoiseValue(int worldX, int worldZ)
        {
             // SharpNoise Perlin is generally considered thread-safe for reads if Seed/OctaveCount etc. aren't changed after initialization.
             return _noiseModule.GetValue(worldX * NoiseScale, 0, worldZ * NoiseScale);
        }

        // Helper to cache height (potentially accessed by multiple threads)
        internal void SetTerrainHeight(int worldX, int worldZ, int height)
        {
            _terrainHeightMap[(worldX, worldZ)] = height; // ConcurrentDictionary handles locking internally
        }


        private Vector2i GetChunkCoords(Vector3 worldPosition)
        {
            int chunkX = (int)Math.Floor(worldPosition.X / Chunk.ChunkSize);
            int chunkZ = (int)Math.Floor(worldPosition.Z / Chunk.ChunkSize);
            return new Vector2i(chunkX, chunkZ);
        }

        // --- Removed GenerateCubeVertices (moved to CubeData.cs) ---

        // Modify Draw to accept a Texture
        public void Draw(Shader shader, Camera camera, Texture texture)
        {
            shader.Use();

            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();
            Matrix4 model = Matrix4.Identity; // Model matrix is identity

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetMatrix4("model", model);
            shader.SetVector3("viewPos", camera.Position);

            // Set Fog Uniforms
            shader.SetVector3("fogColor", FogColor);
            shader.SetFloat("fogDensity", FogDensity);
            shader.SetFloat("fogGradient", FogGradient);

            // --- Texture Setup ---
            texture.Use(TextureUnit.Texture0); // Activate texture unit 0 and bind the texture
            shader.SetInt("textureSampler", 0); // Tell the shader to use texture unit 0

            CheckGLError("World.Draw SetUniforms");

            // Determine visible chunks based on RenderDistance
            Vector2i playerChunkCoords = GetChunkCoords(camera.Position);
            int drawnChunkCount = 0;

            // Iterate over the values (Chunks) of the ConcurrentDictionary
            // This is generally safe, but the collection might change during iteration.
            // Chunks added during iteration might or might not be drawn. Chunks removed might cause exceptions if not handled carefully.
            // A safer approach might be to iterate over _activeChunks.ToArray() but that creates garbage.
            // For rendering, iterating directly is often acceptable if occasional visual glitches are tolerable.
            foreach (var chunk in _activeChunks.Values)
            {
                var coord = chunk.ChunkCoords; // Get coords from the chunk itself

                // Check if chunk is within render distance
                int dx = Math.Abs(coord.X - playerChunkCoords.X);
                int dz = Math.Abs(coord.Y - playerChunkCoords.Y);

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

            // Signal cancellation to the background task
            _cancellationSource.Cancel();
            // Signal the BlockingCollection that no more items will be added
            _generationQueue.CompleteAdding();

            // Wait for the background task to finish
            try
            {
                _generationTask?.Wait(TimeSpan.FromSeconds(5)); // Wait with a timeout
                if (_generationTask != null && !_generationTask.IsCompleted)
                {
                     Console.WriteLine("Warning: Generation task did not complete within timeout.");
                }
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex => ex is OperationCanceledException); // Ignore cancellation exceptions
                Console.WriteLine("Generation task cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error waiting for generation task: {ex.Message}");
            }


            // Dispose remaining chunks (must be on main thread for GL calls)
            foreach (var chunk in _activeChunks.Values)
            {
                chunk.Dispose();
            }
            _activeChunks.Clear();
            _terrainHeightMap.Clear();

            // Dispose task-related resources
            _generationQueue.Dispose();
            _cancellationSource.Dispose();

            GC.SuppressFinalize(this);
            CheckGLError("World.Dispose");
            Console.WriteLine("World Disposed.");
        }

        // Helper to check for errors
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
