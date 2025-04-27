using OpenTK.Mathematics;
using System;
using System.Collections.Generic; // Required for List

namespace VoxelGame.World
{
    public class CollisionManager
    {
        // How far around the player to check blocks (can be tuned)
        private const float VoxelCheckRadius = 1.5f;

        public CollisionManager()
        {
            // Constructor - can be used for initialization later
            // (e.g., building spatial partitioning structures)
        }

        /// <summary>
        /// Checks if a given AABB collides with any voxels in the world.
        /// </summary>
        /// <param name="checkPosition">The center position of the AABB to check (usually player's intended head position).</param>
        /// <param name="aabbSize">The dimensions (Width, Height, Depth) of the AABB.</param>
        /// <param name="voxelPositions">A list of all voxel center positions in the world.
        /// IMPORTANT: For performance, this should be replaced with a spatial data structure (e.g., Dictionary, Octree)
        /// for large worlds.</param>
        /// <param name="collisionNormal">Outputs a basic normal vector indicating the approximate direction of collision.</param>
        /// <returns>True if a collision occurs, false otherwise.</returns>
        public bool CheckWorldCollision(
            Vector3 checkPosition,
            Vector3 aabbSize,
            IEnumerable<Vector3> voxelPositions, // Use IEnumerable for flexibility
            out Vector3 collisionNormal)
        {
            collisionNormal = Vector3.Zero;
            float halfWidth = aabbSize.X / 2.0f;
            float height = aabbSize.Y;
            float halfDepth = aabbSize.Z / 2.0f;

            // Define the AABB bounds based on checkPosition (player's feet position)
            Vector3 aabbMin = new Vector3(
                checkPosition.X - halfWidth,
                checkPosition.Y, // Feet position Y
                checkPosition.Z - halfDepth
            );
            Vector3 aabbMax = new Vector3(
                checkPosition.X + halfWidth,
                checkPosition.Y + height, // Top of head position Y
                checkPosition.Z + halfDepth
            );

            // Determine the broad range of blocks to check
            // Use Floor/Ceiling for safety around coordinate boundaries
            int minX = (int)MathF.Floor(aabbMin.X - VoxelCheckRadius);
            int maxX = (int)MathF.Ceiling(aabbMax.X + VoxelCheckRadius);
            int minY = (int)MathF.Floor(aabbMin.Y - VoxelCheckRadius);
            int maxY = (int)MathF.Ceiling(aabbMax.Y + VoxelCheckRadius);
            int minZ = (int)MathF.Floor(aabbMin.Z - VoxelCheckRadius);
            int maxZ = (int)MathF.Ceiling(aabbMax.Z + VoxelCheckRadius);

            // Iterate through nearby blocks
            // WARNING: Iterating the full list is SLOW for large worlds!
            foreach (Vector3 blockCorner in voxelPositions)
            {
                // Broad-phase check: Only consider blocks roughly within the checking radius
                if (blockCorner.X < minX || blockCorner.X > maxX ||
                    blockCorner.Y < minY || blockCorner.Y > maxY ||
                    blockCorner.Z < minZ || blockCorner.Z > maxZ)
                {
                    continue; // Skip block if it's too far away
                }

                // Define block AABB bounds using integer corner coordinates
                Vector3 blockMin = blockCorner; // blockCorner is the integer coordinate (e.g., 10, 5, 20)
                Vector3 blockMax = blockCorner + Vector3.One; // Extends to (11, 6, 21)

                // Narrow-phase check: AABB Intersection Test
                bool intersects = (aabbMax.X > blockMin.X &&
                                   aabbMin.X < blockMax.X &&
                                   aabbMax.Y > blockMin.Y &&
                                   aabbMin.Y < blockMax.Y &&
                                   aabbMax.Z > blockMin.Z &&
                                   aabbMin.Z < blockMax.Z);

                if (intersects)
                {
                    // Simplistic normal calculation - based on minimum penetration
                    // Calculate overlaps on each axis
                    float overlapX1 = aabbMax.X - blockMin.X;
                    float overlapX2 = blockMax.X - aabbMin.X;
                    float overlapY1 = aabbMax.Y - blockMin.Y;
                    float overlapY2 = blockMax.Y - aabbMin.Y;
                    float overlapZ1 = aabbMax.Z - blockMin.Z;
                    float overlapZ2 = blockMax.Z - aabbMin.Z;

                    // Find minimum overlap
                    float minOverlap = float.MaxValue;
                    Vector3 potentialNormal = Vector3.Zero;

                    // Check positive overlaps (penetration from negative side)
                    if (overlapX1 > 0 && overlapX1 < minOverlap) { minOverlap = overlapX1; potentialNormal = -Vector3.UnitX; } // Player hit block from left
                    if (overlapY1 > 0 && overlapY1 < minOverlap) { minOverlap = overlapY1; potentialNormal = -Vector3.UnitY; } // Player hit block from below (head)
                    if (overlapZ1 > 0 && overlapZ1 < minOverlap) { minOverlap = overlapZ1; potentialNormal = -Vector3.UnitZ; } // Player hit block from back

                    // Check negative overlaps (penetration from positive side)
                    if (overlapX2 > 0 && overlapX2 < minOverlap) { minOverlap = overlapX2; potentialNormal = Vector3.UnitX; } // Player hit block from right
                    if (overlapY2 > 0 && overlapY2 < minOverlap) { minOverlap = overlapY2; potentialNormal = Vector3.UnitY; } // Player hit block from above (feet)
                    if (overlapZ2 > 0 && overlapZ2 < minOverlap) { minOverlap = overlapZ2; potentialNormal = Vector3.UnitZ; } // Player hit block from front

                    collisionNormal = potentialNormal;

                    // For this basic version, return immediately upon first collision.
                    // More advanced physics might check all collisions in the step.
                    return true;
                }
            }

            return false; // No collision detected
        }
    }
}