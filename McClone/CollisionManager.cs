using OpenTK.Mathematics;
using System;
using System.Collections.Generic; // Required for List

namespace VoxelGame;

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

        // Define the AABB bounds based on checkPosition (assuming checkPosition is head)
        Vector3 aabbMin = new Vector3(
            checkPosition.X - halfWidth,
            checkPosition.Y - height, // Feet position
            checkPosition.Z - halfDepth
        );
        Vector3 aabbMax = new Vector3(
            checkPosition.X + halfWidth,
            checkPosition.Y, // Head position
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

        bool collided = false;

        // Iterate through nearby blocks
        // WARNING: Iterating the full list is SLOW for large worlds!
        foreach (Vector3 blockCenter in voxelPositions)
        {
            // Broad-phase check: Only consider blocks roughly within the checking radius
            if (blockCenter.X < minX || blockCenter.X > maxX ||
                blockCenter.Y < minY || blockCenter.Y > maxY ||
                blockCenter.Z < minZ || blockCenter.Z > maxZ)
            {
                continue; // Skip block if it's too far away
            }

            // Define block AABB bounds (assuming blocks are 1x1x1 centered at their position)
            Vector3 blockMin = blockCenter - new Vector3(0.5f);
            Vector3 blockMax = blockCenter + new Vector3(0.5f);

            // Narrow-phase check: AABB Intersection Test
            bool intersects = (aabbMax.X > blockMin.X &&
                               aabbMin.X < blockMax.X &&
                               aabbMax.Y > blockMin.Y &&
                               aabbMin.Y < blockMax.Y &&
                               aabbMax.Z > blockMin.Z &&
                               aabbMin.Z < blockMax.Z);

            if (intersects)
            {
                collided = true;

                // Simplistic normal calculation (same as before)
                float dx1 = aabbMax.X - blockMin.X; // Penetration from left
                float dx2 = blockMax.X - aabbMin.X; // Penetration from right
                float dy1 = aabbMax.Y - blockMin.Y; // Penetration from bottom
                float dy2 = blockMax.Y - aabbMin.Y; // Penetration from top
                float dz1 = aabbMax.Z - blockMin.Z; // Penetration from front
                float dz2 = blockMax.Z - aabbMin.Z; // Penetration from back

                // Find minimum penetration depth (smallest overlap)
                float minOverlap = float.MaxValue;
                Vector3 potentialNormal = Vector3.Zero;

                if (dx1 < minOverlap) { minOverlap = dx1; potentialNormal = -Vector3.UnitX; }
                if (dx2 < minOverlap) { minOverlap = dx2; potentialNormal = Vector3.UnitX; }
                if (dy1 < minOverlap) { minOverlap = dy1; potentialNormal = -Vector3.UnitY; } // Feet hit ground from top
                if (dy2 < minOverlap) { minOverlap = dy2; potentialNormal = Vector3.UnitY; } // Head hit ceiling from bottom
                if (dz1 < minOverlap) { minOverlap = dz1; potentialNormal = -Vector3.UnitZ; }
                if (dz2 < minOverlap) { minOverlap = dz2; potentialNormal = Vector3.UnitZ; }

                collisionNormal = potentialNormal;

                // For this basic version, return immediately upon first collision.
                // More advanced physics might check all collisions in the step.
                return true;
            }
        }

        return false; // No collision detected
    }
}