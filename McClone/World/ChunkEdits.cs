using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VoxelGame.World
{
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
        /// </summary>
        /// <param name="coords">The coordinates of the chunk these edits belong to.</param>
        private ChunkEdits(Vector2i coords)
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

        // TODO: Add methods for saving/loading _allEdits to/from a file.
        // public static void SaveAllEdits(string filePath) { ... }
        // public static void LoadAllEdits(string filePath) { ... }
    }
}
