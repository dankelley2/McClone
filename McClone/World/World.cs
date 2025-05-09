// World.cs: Manages the overall game world, including chunk management, world generation, and rendering

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent; // Add for concurrent collections
using System.Threading;             // Add for CancellationTokenSource
using System.Threading.Tasks;        // Add for Task
using VoxelGame.Rendering;
using VoxelGame.World; // Keep this

namespace VoxelGame.World
{
    public class World : IDisposable
    {
        private readonly WorldGeneration _worldGen; // Remove initialization here
        private readonly FogSettings _fogSettings = new();

        // Chunk Management
        private ConcurrentDictionary<(int, int), Chunk> _activeChunks = new(); // Thread-safe dictionary
        private BlockingCollection<(int, int)> _generationQueue = new(new ConcurrentQueue<(int, int)>()); // Queue for coords needing generation or mesh rebuild
        private ConcurrentQueue<Chunk> _buildQueue = new(); // Queue for chunks ready for GL buffer updates (on main thread)
        private const int RenderDistance = 12;
        private const int LoadDistance = RenderDistance + 2; // Load distance slightly larger to preload

        // Background Task Management
        private CancellationTokenSource _cancellationSource = new();
        private Task _generationTask = null!;

        // Expose the world seed used by the generator
        public int WorldSeed => _worldGen.NoiseModule.Seed;

        public World(int seed) // Add seed parameter to constructor
        {
             _worldGen = new WorldGeneration(seed); // Initialize WorldGeneration with the seed
        }

        public void Initialize()
        {
            Console.WriteLine("Initializing World...");
            CubeData.GenerateVertices(); // Generate the template cube vertices once using static class
            WorldUtils.CheckGLError("World.Initialize CubeData");

            // Loading is now handled in Game.OnLoad before World is constructed

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
                        return false; // Not ready yet
                    }
                }
            }
            Console.WriteLine("Initial chunks are ready!");
            return true;
        }


        // Background task method - Handles BOTH initial terrain gen AND mesh rebuilds
        private void ProcessGenerationQueue(CancellationToken token)
        {
            Console.WriteLine($"Generation/Rebuild Task started on thread {Thread.CurrentThread.ManagedThreadId}");
            try
            {
                foreach (var coord in _generationQueue.GetConsumingEnumerable(token))
                {
                    if (token.IsCancellationRequested) break;

                    if (_activeChunks.TryGetValue(coord, out var chunk))
                    {
                        bool needsInitialTerrain = !chunk._isInitialized;
                        if (needsInitialTerrain)
                        {
                            chunk.GenerateTerrain(); // Generates terrain, sets _isInitialized=true, _isDirty=true
                        }

                        // If initialized (or just became initialized) and is marked dirty, generate mesh data
                        if (chunk._isInitialized && chunk.IsDirty) // Check IsDirty flag
                        {
                            List<float> vertexData = chunk.GenerateVertexData(); // CPU-bound mesh generation
                            chunk.PendingVertexData = vertexData; // Store data for main thread
                            _buildQueue.Enqueue(chunk); // Add to the main thread queue for GL update
                        }
                        // Note: _isDirty is set to false only after GL update on main thread
                    }
                    else
                    {
                        // Chunk might have been unloaded before generation started
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
                Console.WriteLine("Generation/Rebuild Task finished.");
            }
        }

        // Process the build queue (MUST be called from the main/OpenGL thread) - Renamed for clarity
        public void ProcessGLBufferUpdates() // Renamed from ProcessBuildQueue
        {
            int updatedThisFrame = 0;
            int maxUpdatesPerFrame = 4; // Limit GL updates per frame

            while (updatedThisFrame < maxUpdatesPerFrame && _buildQueue.TryDequeue(out var chunkToUpdate))
            {
                 // Check if chunk still exists and has pending data
                 if (_activeChunks.ContainsKey((chunkToUpdate.ChunkCoords.X, chunkToUpdate.ChunkCoords.Y)) && chunkToUpdate.PendingVertexData != null)
                 {
                    chunkToUpdate.UpdateGLBuffers(chunkToUpdate.PendingVertexData); // Upload data to GPU
                    chunkToUpdate.PendingVertexData = null; // Clear pending data
                    // UpdateGLBuffers sets _isDirty = false internally
                    updatedThisFrame++;
                 }
            }
        }


        // Update method called each frame to manage chunks (Main Thread)
        public void Update(Vector3 playerPosition)
        {
            Vector2i playerChunkCoords = GetChunkCoords(playerPosition);

            // --- Process GL Buffer Updates (Main Thread OpenGL operations) ---
            ProcessGLBufferUpdates(); // Renamed call

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
                        // Add to background queue for terrain generation AND initial mesh data generation
                        _generationQueue.Add(coord);
                    }
                    // If TryAdd fails, another thread likely added it just before, which is fine.
                }
            }

            // --- Generation and Build processing happens in ProcessGenerationQueue (background) and ProcessGLBufferUpdates (main thread) ---
        }


        // Method to get voxel positions relevant for collision near a point
        public List<Vector3> GetNearbyVoxelPositions(Vector3 center)
        {
            List<Vector3> nearbyVoxels = new List<Vector3>();
            int centerX = (int)Math.Floor(center.X);
            int centerZ = (int)Math.Floor(center.Z);

            // Define the search range around the player (1 block radius horizontally)
            int radius = 1;
            int minX = centerX - radius;
            int maxX = centerX + radius;
            int minZ = centerZ - radius;
            int maxZ = centerZ + radius;

            // Define vertical range based on player position (e.g., from feet-2 to head+1)
            // Adjust these offsets based on player height and potential jump/fall checks
            int minY = (int)Math.Floor(center.Y) - 2; // Check slightly below feet
            int maxY = (int)Math.Floor(center.Y) + 2; // Check up to head height + 1
            minY = Math.Max(0, minY); // Clamp to world bottom
            maxY = Math.Min(Chunk.ChunkHeight - 1, maxY); // Clamp to world top

            // Iterate through the 3x3xN volume around the center point
            for (int worldY = minY; worldY <= maxY; worldY++)
            {
                for (int worldX = minX; worldX <= maxX; worldX++)
                {
                    for (int worldZ = minZ; worldZ <= maxZ; worldZ++)
                    {
                        // GetBlockState handles finding the correct chunk for any world coordinate
                        if (GetBlockState(worldX, worldY, worldZ) != 0) // Check if the block is solid
                        {
                            // Add the world coordinates of the solid block
                            nearbyVoxels.Add(new Vector3(worldX, worldY, worldZ));
                        }
                    }
                }
            }

            return nearbyVoxels;
        }

        // Expose fog properties via FogSettings
        public Vector3 FogColor { get => _fogSettings.FogColor; set => _fogSettings.FogColor = value; }
        public float FogDensity { get => _fogSettings.FogDensity; set => _fogSettings.FogDensity = value; }
        public float FogGradient { get => _fogSettings.FogGradient; set => _fogSettings.FogGradient = value; }


        // Thread-safe way to get height from cache
        public int GetHeight(int worldX, int worldZ)
        {
            // Use WorldGeneration for noise and height
            double noiseValue = _worldGen.GetNoiseValue(worldX, worldZ);
            byte calculatedHeight = (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, WorldGeneration.BaseHeight + Math.Round((noiseValue + 1.0) / 2.0 * WorldGeneration.TerrainAmplitude)));
            return calculatedHeight;
        }

        // Helper for noise calculation (potentially accessed by multiple threads)
        internal double GetNoiseValue(int worldX, int worldZ)
        {
            return _worldGen.GetNoiseValue(worldX, worldZ);
        }


        private Vector2i GetChunkCoords(Vector3 worldPosition)
        {
            return WorldUtils.GetChunkCoords(worldPosition, Chunk.ChunkSize);
        }

        // --- Removed GenerateCubeVertices (moved to CubeData.cs) ---

        // Cache shader uniform locations
        private void CacheUniformLocations(Shader shader)
        {
            // Ensure caching is only attempted once or make idempotent if called multiple times
        }

        // Modify Draw to accept a Texture
        public void Draw(Shader shader, Camera camera, Texture texture)
        {
            shader.Use();
            CacheUniformLocations(shader); // Ensure locations are cached (if any remain)

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

            WorldUtils.CheckGLError("World.Draw SetUniforms");

            // Determine visible chunks based on RenderDistance
            Vector2i playerChunkCoords = GetChunkCoords(camera.Position);
            int drawnChunkCount = 0;

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
                        chunk.Draw();
                        drawnChunkCount++;
                    }
                }
            }
            WorldUtils.CheckGLError("World.Draw DrawChunks");
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

            // Saving is now handled in Game.OnUnload before World is disposed


            // Dispose remaining chunks (must be on main thread for GL calls)
            foreach (var chunk in _activeChunks.Values)
            {
                chunk.Dispose();
            }
            _activeChunks.Clear();

            // Dispose task-related resources
            _generationQueue.Dispose();
            _cancellationSource.Dispose();

            GC.SuppressFinalize(this);
            WorldUtils.CheckGLError("World.Dispose");
            Console.WriteLine("World Disposed.");
        }

        // Simple raycasting implementation
        // Returns true if a block is hit, false otherwise.
        // Outputs the world position of the hit block and the position of the block *before* the hit (for placement).
        public bool Raycast(Vector3 start, Vector3 direction, float maxDistance, out Vector3 hitBlockPosition, out Vector3 adjacentBlockPosition)
        {
            hitBlockPosition = Vector3.Zero;
            adjacentBlockPosition = Vector3.Zero;
            direction.Normalize();
            Vector3 currentPos = start;

            for (float dist = 0; dist < maxDistance; dist += 0.1f) // Step along the ray
            {
                currentPos = start + direction * dist;
                Vector3i blockPos = new Vector3i((int)Math.Floor(currentPos.X), (int)Math.Floor(currentPos.Y), (int)Math.Floor(currentPos.Z));

                // Check if this block position is solid
                if (GetBlockState(blockPos.X, blockPos.Y, blockPos.Z) != 0)
                {
                    hitBlockPosition = new Vector3(blockPos.X, blockPos.Y, blockPos.Z);

                    // Calculate the position adjacent to the hit face
                    Vector3 prevPos = start + direction * (dist - 0.11f); // Step back slightly
                    adjacentBlockPosition = new Vector3(
                        (int)Math.Floor(prevPos.X),
                        (int)Math.Floor(prevPos.Y),
                        (int)Math.Floor(prevPos.Z)
                    );
                    return true; // Hit!
                }
            }

            return false; // No hit within maxDistance
        }

        // Performs a raycast from the camera and adds a block adjacent to the hit face.
        public bool AddBlockFromNormalVector(Camera camera, float maxDistance, byte newState = 1)
        {
            if (newState == 0) return false; // Cannot add air this way

            bool hit = Raycast(camera.Position, camera.Front, maxDistance, out _, out Vector3 adjacentBlockPos);

            if (hit)
            {
                // Convert adjacent position to integer coordinates for block placement
                Vector3i placePos = new Vector3i((int)Math.Floor(adjacentBlockPos.X), (int)Math.Floor(adjacentBlockPos.Y), (int)Math.Floor(adjacentBlockPos.Z));
                // Get camera position as integer coordinates
                Vector3i cameraPos = new Vector3i((int)Math.Floor(camera.Position.X), (int)Math.Floor(camera.Position.Y), (int)Math.Floor(camera.Position.Z));

                // if X and Z are the same
                if (placePos.X == cameraPos.X && placePos.Z == cameraPos.Z){
                    // check if y is the same, or y is one less than cameraPos.y
                    if (placePos.Y == cameraPos.Y || placePos.Y == cameraPos.Y - 1 || placePos.Y == cameraPos.Y - 2)
                    {
                        return false; // Don't place a block in the same position as the player
                    }
                }

                AddBlockAt(placePos, newState);
                return true; // Block placed successfully
            }
            return false; // No hit, no block placed
        }

        // Gets the state of a block at world coordinates
        public byte GetBlockState(int worldX, int worldY, int worldZ)
        {
            if (worldY < 0 || worldY >= Chunk.ChunkHeight) return 0; // Out of vertical bounds

            Vector2i chunkCoords = GetChunkCoords(new Vector3(worldX, worldY, worldZ));
            if (_activeChunks.TryGetValue((chunkCoords.X, chunkCoords.Y), out var chunk))
            {
                int localX = worldX - chunk.WorldOffset.X;
                int localZ = worldZ - chunk.WorldOffset.Z;
                // Ensure local coordinates are within the chunk bounds (0 to ChunkSize-1)
                localX = (localX % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;
                localZ = (localZ % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;

                return chunk.GetLocalVoxelState(localX, worldY, localZ);
            }
            return 0; // Chunk not loaded or found
        }


        // Removes a block at the specified world coordinates
        public bool RemoveBlockAt(Vector3i worldBlockPos)
        {
             if (worldBlockPos.Y < 0 || worldBlockPos.Y >= Chunk.ChunkHeight) return false;

             Vector2i chunkCoords = GetChunkCoords(new Vector3(worldBlockPos.X, worldBlockPos.Y, worldBlockPos.Z));
             if (_activeChunks.TryGetValue((chunkCoords.X, chunkCoords.Y), out var chunk))
             {
                 int localX = worldBlockPos.X - chunk.WorldOffset.X;
                 int localZ = worldBlockPos.Z - chunk.WorldOffset.Z;
                 // Ensure local coordinates are within the chunk bounds (0 to ChunkSize-1)
                 localX = (localX % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;
                 localZ = (localZ % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;

                 // Check if the block is actually within this chunk's horizontal bounds after modulo
                 if (worldBlockPos.X >= chunk.WorldOffset.X && worldBlockPos.X < chunk.WorldOffset.X + Chunk.ChunkSize &&
                     worldBlockPos.Z >= chunk.WorldOffset.Z && worldBlockPos.Z < chunk.WorldOffset.Z + Chunk.ChunkSize)
                 {
                     // Call Chunk.RemoveBlock - it now handles internal state and recording the edit
                     chunk.RemoveBlock((byte)localX, (byte)worldBlockPos.Y, (byte)localZ);

                     // If the chunk became dirty (which RemoveBlock ensures if a change occurred),
                     // queue it and its neighbors for rebuild.
                     if (chunk.IsDirty)
                     {
                         // Add coordinates to background queue to trigger mesh data regeneration
                         _generationQueue.Add((chunkCoords.X, chunkCoords.Y));
                         // Console.WriteLine($"Queued chunk {chunkCoords} for rebuild after block removal.");

                         // Mark neighbors dirty if block was on a border
                         MarkNeighborsDirty(worldBlockPos.X, worldBlockPos.Z, localX, localZ);
                     }
                 }
                 return true; // Block removed successfully
             }
             return false; // Chunk not loaded or found
        }

        // Adds a block at the specified world coordinates with the given state
        public void AddBlockAt(Vector3i worldBlockPos, byte newState)
        {
            // Basic bounds check
            if (worldBlockPos.Y < 0 || worldBlockPos.Y >= Chunk.ChunkHeight) return;
            // Don't allow adding air this way, use RemoveBlockAt
            if (newState == 0) return;

            Vector2i chunkCoords = GetChunkCoords(new Vector3(worldBlockPos.X, worldBlockPos.Y, worldBlockPos.Z));
            if (_activeChunks.TryGetValue((chunkCoords.X, chunkCoords.Y), out var chunk))
            {
                int localX = worldBlockPos.X - chunk.WorldOffset.X;
                int localZ = worldBlockPos.Z - chunk.WorldOffset.Z;
                // Ensure local coordinates are within the chunk bounds (0 to ChunkSize-1)
                localX = (localX % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;
                localZ = (localZ % Chunk.ChunkSize + Chunk.ChunkSize) % Chunk.ChunkSize;

                // Check if the block is actually within this chunk's horizontal bounds after modulo
                if (worldBlockPos.X >= chunk.WorldOffset.X && worldBlockPos.X < chunk.WorldOffset.X + Chunk.ChunkSize &&
                    worldBlockPos.Z >= chunk.WorldOffset.Z && worldBlockPos.Z < chunk.WorldOffset.Z + Chunk.ChunkSize)
                {
                    // Call Chunk.AddBlock - it handles internal state and recording the edit
                    chunk.AddBlock((byte)localX, (byte)worldBlockPos.Y, (byte)localZ, newState);

                    // If the chunk became dirty (which AddBlock ensures if a change occurred),
                    // queue it and its neighbors for rebuild.
                    if (chunk.IsDirty)
                    {
                        // Add coordinates to background queue to trigger mesh data regeneration
                        _generationQueue.Add((chunkCoords.X, chunkCoords.Y));
                        // Console.WriteLine($"Queued chunk {chunkCoords} for rebuild after block placement.");

                        // Mark neighbors dirty if block was placed on a border
                        MarkNeighborsDirty(worldBlockPos.X, worldBlockPos.Z, localX, localZ);
                    }
                }
            }
            // If chunk doesn't exist, we can't add a block to it yet.
            // Future enhancement: Queue placement if chunk is pending generation?
        }

        // Helper to mark neighboring chunks dirty if a block is modified on a border
        private void MarkNeighborsDirty(int worldX, int worldZ, int localX, int localZ)
        {
            // Check and mark neighbors if the modified block is on a chunk border
            Action<int, int> markNeighbor = (dx, dz) => {
                Vector2i neighborCoords = GetChunkCoords(new Vector3(worldX + dx, 0, worldZ + dz));
                 if (_activeChunks.TryGetValue((neighborCoords.X, neighborCoords.Y), out var neighborChunk))
                 {
                     neighborChunk.MarkDirty();
                     // Add neighbor coordinates to background queue for mesh data regeneration
                     _generationQueue.Add((neighborCoords.X, neighborCoords.Y));
                     // Console.WriteLine($"Queued neighbor chunk {neighborCoords} for rebuild.");
                 }
            };

            bool onXBorder = localX == 0 || localX == Chunk.ChunkSize - 1;
            bool onZBorder = localZ == 0 || localZ == Chunk.ChunkSize - 1;

            if (localX == 0) markNeighbor(-1, 0); // Left neighbor
            if (localX == Chunk.ChunkSize - 1) markNeighbor(1, 0); // Right neighbor
            if (localZ == 0) markNeighbor(0, -1); // Back neighbor
            if (localZ == Chunk.ChunkSize - 1) markNeighbor(0, 1); // Front neighbor

            // Only check diagonals if it was on a corner
            if (onXBorder && onZBorder)
            {
                if (localX == 0 && localZ == 0) markNeighbor(-1, -1); // Bottom-Left
                if (localX == Chunk.ChunkSize - 1 && localZ == 0) markNeighbor(1, -1); // Bottom-Right
                if (localX == 0 && localZ == Chunk.ChunkSize - 1) markNeighbor(-1, 1); // Top-Left
                if (localX == Chunk.ChunkSize - 1 && localZ == Chunk.ChunkSize - 1) markNeighbor(1, 1); // Top-Right
            }
        }

    }
}
