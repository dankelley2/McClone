using System.IO;
using System.Linq;
using System.Text.Json;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VoxelGame.World
{
    // --- Serialization Data Structures ---
    // These structures are designed for easy JSON serialization/deserialization

    internal class BlockEditData
    {
        public byte X { get; set; }
        public byte Z { get; set; }
        public byte State { get; set; }
    }

    internal class LayerEditData
    {
        public int YLevel { get; set; }
        public List<BlockEditData> Blocks { get; set; } = new();
    }

    internal class ChunkEditData
    {
        public int ChunkX { get; set; }
        public int ChunkZ { get; set; } // Changed from Y to Z for clarity, assuming Vector2i uses X,Y for horizontal plane
        public List<LayerEditData> Layers { get; set; } = new();
    }

    internal class SaveData
    {
        public int WorldSeed { get; set; }
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
            var edits = _allEdits.GetOrAdd((coords.X, coords.Y), key => new ChunkEdits(new Vector2i(key.Item1, key.Item2)));

            // Get or create the dictionary for the specific Y-level
            var layerEdits = edits._editedLayers.GetOrAdd(y, _ => new ConcurrentDictionary<(byte x, byte z), byte>());

            // Add or update the specific block edit for (x, z) at this y-level
            // Note: This currently stores *all* changes, including setting back to default.
            // Optimization: Could compare newState to the original generated state and remove the edit if they match.
            layerEdits[((byte)x, (byte)z)] = newState;

            // TODO: Add persistence logic here (e.g., mark for saving to disk)
        }

        /// <summary>
        /// Applies the stored edits to the provided Chunk object's layer data.
        /// This should be called after the chunk's initial terrain generation.
        /// </summary>
        /// <param name="chunk">The chunk to apply edits to.</param>
        internal void ApplyToChunk(Chunk chunk)
        {
            if (chunk.ChunkCoords != this.ChunkCoords)
            {
                Console.WriteLine($"Warning: Attempting to apply edits from {this.ChunkCoords} to chunk {chunk.ChunkCoords}.");
                return;
            }

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
                }
            }
             // After applying edits, the chunk mesh needs rebuilding
            chunk.MarkDirty();
        }

        private static string GetSaveFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mcClonePath = Path.Combine(appDataPath, "McClone");
            Directory.CreateDirectory(mcClonePath); // Ensure the directory exists
            return Path.Combine(mcClonePath, "world_edits.json");
        }

        /// <summary>
        /// Saves all current chunk edits and the world seed to a JSON file in the AppData directory.
        /// </summary>
        /// <param name="worldSeed">The seed of the world being saved.</param>
        public static void SaveAllEdits(int worldSeed)
        {
            var saveData = new SaveData { WorldSeed = worldSeed };

            foreach (var kvpChunk in _allEdits)
            {
                var chunkCoords = kvpChunk.Key;
                var chunkEdits = kvpChunk.Value;

                var chunkEditData = new ChunkEditData
                {
                    ChunkX = chunkCoords.Item1,
                    ChunkZ = chunkCoords.Item2 // Assuming Item2 is Z
                };

                foreach (var kvpLayer in chunkEdits._editedLayers)
                {
                    var yLevel = kvpLayer.Key;
                    var layerBlocks = kvpLayer.Value;

                    var layerEditData = new LayerEditData { YLevel = yLevel };

                    foreach (var kvpBlock in layerBlocks)
                    {
                        layerEditData.Blocks.Add(new BlockEditData
                        {
                            X = kvpBlock.Key.x,
                            Z = kvpBlock.Key.z,
                            State = kvpBlock.Value
                        });
                    }
                    // Only add layer data if there are actual block edits in it
                    if (layerEditData.Blocks.Any())
                    {
                        chunkEditData.Layers.Add(layerEditData);
                    }
                }
                 // Only add chunk data if there are actual layer edits in it
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
                Console.WriteLine($"World edits saved to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving world edits: {ex.Message}");
                // Consider more robust error handling/logging
            }
        }

        /// <summary>
        /// Loads chunk edits from the JSON file in the AppData directory.
        /// Clears existing edits before loading.
        /// </summary>
        /// <returns>The world seed loaded from the file, or a default value (e.g., 0) if the file doesn't exist or fails to load.</returns>
        public static int LoadAllEdits()
        {
            string filePath = GetSaveFilePath();
            int loadedSeed = 0; // Default seed if load fails

            if (!File.Exists(filePath))
            {
                Console.WriteLine("No world edit save file found. Starting with no edits.");
                 _allEdits.Clear(); // Ensure edits are cleared even if file doesn't exist
                return loadedSeed; // Return default seed
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                var saveData = JsonSerializer.Deserialize<SaveData>(jsonString);

                if (saveData == null)
                {
                     Console.WriteLine($"Error: Failed to deserialize save data from {filePath}.");
                     _allEdits.Clear(); // Clear edits on failed deserialization
                     return 0; // Return default seed
                }

                loadedSeed = saveData.WorldSeed;
                _allEdits.Clear(); // Clear current edits before loading

                foreach (var chunkEditData in saveData.Edits)
                {
                    var chunkCoords = new Vector2i(chunkEditData.ChunkX, chunkEditData.ChunkZ);
                    // Use GetOrAdd pattern to handle potential concurrency, though Load should ideally happen before world gen starts
                     var chunkEdits = _allEdits.GetOrAdd((chunkCoords.X, chunkCoords.Y), _ => new ChunkEdits(chunkCoords));


                    foreach (var layerEditData in chunkEditData.Layers)
                    {
                        var layerEdits = chunkEdits._editedLayers.GetOrAdd(layerEditData.YLevel, _ => new ConcurrentDictionary<(byte x, byte z), byte>());

                        foreach (var blockEditData in layerEditData.Blocks)
                        {
                            layerEdits[(blockEditData.X, blockEditData.Z)] = blockEditData.State;
                        }
                    }
                }
                Console.WriteLine($"World edits loaded successfully from {filePath}. World Seed: {loadedSeed}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading world edits from {filePath}: {ex.Message}");
                 _allEdits.Clear(); // Clear edits on exception
                loadedSeed = 0; // Reset seed to default on error
                // Consider more robust error handling/logging
            }

            return loadedSeed;
        }
    }
}
