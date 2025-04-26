using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO; // Added
using System.Linq; // Added
using System.Text.Json; // Added
using System.Text.Json.Serialization; // Added for attributes if needed

namespace VoxelGame.World
{
    // --- Serialization Data Structures (Optimized) ---

    // Represents a single layer's edits as a 2D array.
    // Using byte is sufficient for block states (0-255).
    internal class LayerEditData
    {
        public int YLevel { get; set; }
        // Stores the full 16x16 layer state.
        // A value of NO_EDIT_MARKER indicates the original generated block should be used.
        // Assumes Chunk.ChunkSize is 16. If not, this needs adjustment.
        public byte[][] Blocks { get; set; } = null!; // Initialize later

        // Define a marker for unedited blocks within the saved array.
        // Choose a value unlikely to be a valid block state.
        [JsonIgnore] // Don't serialize this constant itself
        public const byte NO_EDIT_MARKER = 255;
    }

    // Represents all edits for a single chunk.
    internal class ChunkEditData
    {
        public int ChunkX { get; set; }
        public int ChunkZ { get; set; }
        public List<LayerEditData> Layers { get; set; } = new();
    }

    // Represents the overall save file structure.
    internal class SaveData
    {
        public int WorldSeed { get; set; }
        // Store player position components as nullable floats for backward compatibility
        public float? PlayerPositionX { get; set; }
        public float? PlayerPositionY { get; set; }
        public float? PlayerPositionZ { get; set; }
        // Store player orientation components as nullable floats
        public float? PlayerPitch { get; set; }
        public float? PlayerYaw { get; set; }
        public List<ChunkEditData> Edits { get; set; } = new();
    }

    // --- End Serialization Data Structures ---


    /// <summary>
    /// Stores persistent edits made to a specific chunk.
    /// This class manages the storage and retrieval of block changes.
    /// </summary>
    public class ChunkEdits
    {
        /// <summary>
        /// Chunk coordinates (X, Z) these edits apply to.
        /// </summary>
        public Vector2i ChunkCoords { get; }

        /// <summary>
        /// Stores the edited voxel states. Key is Y-level, Value is another dictionary
        /// mapping local (X, Z) coordinates within the chunk to the new block state (byte).
        /// Using ConcurrentDictionary for thread safety during edits.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<(byte x, byte z), byte>> _editedLayers = new();

        /// <summary>
        /// Static, thread-safe dictionary holding all ChunkEdits instances, keyed by chunk coordinates (X, Z).
        /// </summary>
        private static readonly ConcurrentDictionary<(int, int), ChunkEdits> _allEdits = new();

        /// <summary>
        /// Private constructor to ensure instances are managed via static methods.
        /// Also used internally by LoadAllEdits.
        /// </summary>
        /// <param name="coords">The coordinates of the chunk these edits belong to.</param>
        private ChunkEdits(Vector2i coords) // Keep private
        {
            ChunkCoords = coords;
        }

        /// <summary>
        /// Retrieves the ChunkEdits instance for the given chunk coordinates, if any edits exist.
        /// </summary>
        /// <param name="coords">The chunk coordinates (X, Z).</param>
        /// <returns>The ChunkEdits instance or null if no edits have been recorded for this chunk.</returns>
        public static ChunkEdits? GetEdits(Vector2i coords)
        {
            // Assuming Vector2i Y corresponds to World Z
            _allEdits.TryGetValue((coords.X, coords.Y), out var edits);
            return edits;
        }

        /// <summary>
        /// Records a block change at the specified local coordinates within a chunk.
        /// Creates the ChunkEdits instance and necessary layer dictionaries if they don't exist.
        /// </summary>
        /// <param name="coords">The chunk coordinates (X, Z).</param>
        /// <param name="y">The local Y coordinate within the chunk.</param>
        /// <param name="x">The local X coordinate within the chunk.</param>
        /// <param name="z">The local Z coordinate within the chunk.</param>
        /// <param name="newState">The new state of the block (0 for air, >0 for solid types).</param>
        public static void RecordEdit(Vector2i coords, int y, byte x, byte z, byte newState)
        {
            // Get or create the ChunkEdits instance for these coordinates
            // Assuming Vector2i Y corresponds to World Z
            var edits = _allEdits.GetOrAdd((coords.X, coords.Y), key => new ChunkEdits(new Vector2i(key.Item1, key.Item2)));

            // Get or create the dictionary for the specific Y-level
            var layerEdits = edits._editedLayers.GetOrAdd(y, _ => new ConcurrentDictionary<(byte x, byte z), byte>());

            // Add or update the specific block edit for (x, z) at this y-level
            layerEdits[((byte)x, (byte)z)] = newState;

            // TODO: Consider if comparing newState to original generated state is needed
            // to potentially *remove* an edit if it matches the original terrain.
            // This would require access to the world generator or original chunk data.
        }

        /// <summary>
        /// Applies the stored edits to the provided Chunk object's layer data.
        /// This should be called after the chunk's initial terrain generation.
        /// </summary>
        /// <param name="chunk">The chunk to apply edits to.</param>
        internal void ApplyToChunk(Chunk chunk)
        {
            // Assuming Vector2i Y corresponds to World Z
            if (chunk.ChunkCoords.X != this.ChunkCoords.X || chunk.ChunkCoords.Y != this.ChunkCoords.Y)
            {
                Console.WriteLine($"Warning: Attempting to apply edits from {this.ChunkCoords} to chunk {chunk.ChunkCoords}.");
                return;
            }

            bool appliedAnyEdit = false;
            foreach (var kvpY in _editedLayers)
            {
                int y = kvpY.Key;
                var layerEdits = kvpY.Value;

                // Ensure Y is within valid chunk height
                if (y < 0 || y >= Chunk.ChunkHeight) continue;

                foreach (var kvpXZ in layerEdits)
                {
                    byte x = kvpXZ.Key.x;
                    byte z = kvpXZ.Key.z;
                    byte state = kvpXZ.Value;

                    // Ensure X and Z are within valid chunk size
                    if (x >= Chunk.ChunkSize || z >= Chunk.ChunkSize) continue;

                    // Use the chunk's method to set the state, handling layer creation
                    chunk.SetLocalVoxelStateInternal(x, y, z, state);
                    appliedAnyEdit = true;
                }
            }
             // After applying edits, the chunk mesh needs rebuilding if any edit was applied
            if (appliedAnyEdit)
            {
                chunk.MarkDirty();
            }
        }

        private static string GetSaveFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mcClonePath = Path.Combine(appDataPath, "McClone");
            Directory.CreateDirectory(mcClonePath); // Ensure the directory exists
            return Path.Combine(mcClonePath, "world_edits.json");
        }

        /// <summary>
        /// Saves all current chunk edits, the world seed, player position, and orientation to a compact JSON file.
        /// </summary>
        /// <param name="worldSeed">The seed of the world being saved.</param>
        /// <param name="playerPosition">The player's current position.</param>
        /// <param name="playerPitch">The player camera's pitch.</param>
        /// <param name="playerYaw">The player camera's yaw.</param>
        public static void SaveAllEdits(int worldSeed, Vector3 playerPosition, float playerPitch, float playerYaw)
        {
            var saveData = new SaveData
            {
                WorldSeed = worldSeed,
                PlayerPositionX = playerPosition.X,
                PlayerPositionY = playerPosition.Y,
                PlayerPositionZ = playerPosition.Z,
                PlayerPitch = playerPitch,
                PlayerYaw = playerYaw
            };
            const int chunkSize = Chunk.ChunkSize; // Assuming Chunk.ChunkSize is accessible and constant

            foreach (var kvpChunk in _allEdits)
            {
                var chunkCoordsTuple = kvpChunk.Key;
                var chunkEdits = kvpChunk.Value;

                // Skip saving if there are no edited layers for this chunk
                if (chunkEdits._editedLayers.IsEmpty) continue;

                var chunkEditData = new ChunkEditData
                {
                    ChunkX = chunkCoordsTuple.Item1,
                    ChunkZ = chunkCoordsTuple.Item2
                };

                foreach (var kvpLayer in chunkEdits._editedLayers)
                {
                    var yLevel = kvpLayer.Key;
                    var layerBlockEdits = kvpLayer.Value; // The dictionary of (x,z) -> state

                    // Skip saving if this specific layer has no edits recorded
                    if (layerBlockEdits.IsEmpty) continue;

                    // Create the 2D array for this layer
                    var layerBlocksArray = new byte[chunkSize][];
                    for (int i = 0; i < chunkSize; i++)
                    {
                        layerBlocksArray[i] = new byte[chunkSize];
                        // Initialize with the NO_EDIT_MARKER
                        Array.Fill(layerBlocksArray[i], LayerEditData.NO_EDIT_MARKER);
                    }

                    // Populate the array with actual edits
                    foreach (var kvpBlock in layerBlockEdits)
                    {
                        byte x = kvpBlock.Key.x;
                        byte z = kvpBlock.Key.z;
                        byte state = kvpBlock.Value;

                        if (x < chunkSize && z < chunkSize) // Bounds check
                        {
                            layerBlocksArray[x][z] = state;
                        }
                        else
                        {
                             Console.WriteLine($"Warning: Edit ({x},{z}) at Y={yLevel} in chunk ({chunkEditData.ChunkX},{chunkEditData.ChunkZ}) is outside expected chunk size {chunkSize}. Skipping save for this block.");
                        }
                    }

                    var layerEditData = new LayerEditData
                    {
                        YLevel = yLevel,
                        Blocks = layerBlocksArray
                    };
                    chunkEditData.Layers.Add(layerEditData);
                }

                // Only add chunk data if it actually contains edited layers
                if (chunkEditData.Layers.Any())
                {
                    saveData.Edits.Add(chunkEditData);
                }
            }


            try
            {
                string filePath = GetSaveFilePath();
                // Use minimal options for compacted JSON
                string jsonString = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(filePath, jsonString);
                Console.WriteLine($"World data saved to {filePath}"); // Updated message
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving world data: {ex.Message}"); // Updated message
                // Consider more robust error handling/logging
            }
        }

        /// <summary>
        /// Loads chunk edits, player position, and orientation from the JSON file.
        /// Clears existing edits before loading.
        /// </summary>
        /// <returns>A tuple containing the loaded world seed, an optional player position (Vector3?),
        /// an optional player pitch (float?), and an optional player yaw (float?).
        /// Returns defaults (0, null, null, null) if the file doesn't exist or fails to load.</returns>
        public static (int seed, Vector3? playerPosition, float? playerPitch, float? playerYaw) LoadAllEdits()
        {
            string filePath = GetSaveFilePath();
            int loadedSeed = 0; // Default seed if load fails
            Vector3? loadedPlayerPosition = null; // Default position is null
            float? loadedPlayerPitch = null; // Default pitch is null
            float? loadedPlayerYaw = null; // Default yaw is null
            const int chunkSize = Chunk.ChunkSize; // Assuming Chunk.ChunkSize is accessible and constant

            if (!File.Exists(filePath))
            {
                Console.WriteLine("No world save file found. Starting fresh.");
                 _allEdits.Clear(); // Ensure edits are cleared even if file doesn't exist
                return (loadedSeed, loadedPlayerPosition, loadedPlayerPitch, loadedPlayerYaw); // Return defaults
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                var saveData = JsonSerializer.Deserialize<SaveData>(jsonString);

                if (saveData == null)
                {
                     Console.WriteLine($"Error: Failed to deserialize save data from {filePath}.");
                     _allEdits.Clear(); // Clear edits on failed deserialization
                     return (0, null, null, null); // Return defaults
                }

                loadedSeed = saveData.WorldSeed;

                // Attempt to load player position if present
                if (saveData.PlayerPositionX.HasValue && saveData.PlayerPositionY.HasValue && saveData.PlayerPositionZ.HasValue)
                {
                    loadedPlayerPosition = new Vector3(
                        saveData.PlayerPositionX.Value,
                        saveData.PlayerPositionY.Value,
                        saveData.PlayerPositionZ.Value
                    );
                    Console.WriteLine($"Loaded player position: {loadedPlayerPosition.Value}");
                }
                else
                {
                    Console.WriteLine("No player position found in save file.");
                }

                // Attempt to load player orientation if present
                if (saveData.PlayerPitch.HasValue && saveData.PlayerYaw.HasValue)
                {
                    loadedPlayerPitch = saveData.PlayerPitch.Value;
                    loadedPlayerYaw = saveData.PlayerYaw.Value;
                    Console.WriteLine($"Loaded player orientation: Pitch={loadedPlayerPitch.Value}, Yaw={loadedPlayerYaw.Value}");
                }
                else
                {
                     Console.WriteLine("No player orientation found in save file.");
                }


                _allEdits.Clear(); // Clear current edits before loading chunk edits

                foreach (var chunkEditData in saveData.Edits)
                {
                    var chunkCoords = new Vector2i(chunkEditData.ChunkX, chunkEditData.ChunkZ);
                    // Use GetOrAdd pattern to handle potential concurrency, though Load should ideally happen before world gen starts
                     var chunkEdits = _allEdits.GetOrAdd((chunkCoords.X, chunkCoords.Y), _ => new ChunkEdits(chunkCoords));


                    foreach (var layerEditData in chunkEditData.Layers)
                    {
                        var layerEditsDict = chunkEdits._editedLayers.GetOrAdd(layerEditData.YLevel, _ => new ConcurrentDictionary<(byte x, byte z), byte>());

                        byte[][] layerBlocksArray = layerEditData.Blocks;

                        // Validate array dimensions (optional but good practice)
                        if (layerBlocksArray == null || layerBlocksArray.Length != chunkSize || layerBlocksArray.Any(row => row == null || row.Length != chunkSize))
                        {
                            Console.WriteLine($"Warning: Invalid block array dimensions for chunk ({chunkEditData.ChunkX},{chunkEditData.ChunkZ}), Y={layerEditData.YLevel}. Skipping layer.");
                            continue;
                        }


                        for (byte x = 0; x < chunkSize; x++)
                        {
                            for (byte z = 0; z < chunkSize; z++)
                            {
                                byte state = layerBlocksArray[x][z];
                                // If the state is not the marker, record it as an edit
                                if (state != LayerEditData.NO_EDIT_MARKER)
                                {
                                    layerEditsDict[(x, z)] = state;
                                }
                            }
                        }
                    }
                }
                Console.WriteLine($"World edits loaded successfully from {filePath}. World Seed: {loadedSeed}");

            }
            catch (JsonException jsonEx)
            {
                 Console.WriteLine($"Error deserializing world save JSON from {filePath}: {jsonEx.Message}");
                 _allEdits.Clear(); // Clear edits on exception
                 loadedSeed = 0; // Reset seed to default on error
                 loadedPlayerPosition = null; // Reset position on error
                 loadedPlayerPitch = null; // Reset pitch on error
                 loadedPlayerYaw = null; // Reset yaw on error
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading world save from {filePath}: {ex.Message}");
                 _allEdits.Clear(); // Clear edits on exception
                loadedSeed = 0; // Reset seed to default on error
                loadedPlayerPosition = null; // Reset position on error
                loadedPlayerPitch = null; // Reset pitch on error
                loadedPlayerYaw = null; // Reset yaw on error
                // Consider more robust error handling/logging
            }

            return (loadedSeed, loadedPlayerPosition, loadedPlayerPitch, loadedPlayerYaw);
        }
    }
}
