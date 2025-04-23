using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic; // Required for List<Vector3>

namespace VoxelGame
{
    public class Player
    {
        public Vector3 Position { get; set; } // Represents the position of the player's feet
        public Vector3 Velocity { get; private set; } = Vector3.Zero;
        public Camera PlayerCamera { get; private set; }
        public Vector3i? TargetedBlockPosition { get; private set; } // Store the position of the block being looked at

        // Player Physics Properties
        private const float Gravity = 25.0f;
        private const float JumpForce = 9.0f;
        private const float PlayerSpeed = 5.0f;
        public Vector3 Size { get; } = new Vector3(0.6f, 1f, 0.6f); // Width, Height, Depth
        public float EyeHeight => Size.Y * 0.9f; // Eye level relative to feet position (e.g., 90% of height)

        private bool _isOnGround = false;
        private bool _canJump = false;

        private CollisionManager _collisionManager;
        private World _world; // Reference to the world for collision data
        private bool _shiftPressed = false;
        private bool _ctrlPressed;
        private float _blockBreakCooldown = 0.0f; // Cooldown timer
        private const float BlockBreakInterval = 0.2f; // Time between block breaks

        public Player(Vector3 startPosition, float aspectRatio, CollisionManager collisionManager, World world)
        {
            Position = startPosition; // startPosition should be feet position
            // Initialize camera at the correct eye level
            PlayerCamera = new Camera(startPosition + Vector3.UnitY * EyeHeight, aspectRatio);
            _collisionManager = collisionManager;
            _world = world;
        }

        public void Update(float dt, KeyboardState input, MouseState mouse, bool isFirstMove, Vector2 lastMousePos)
        {
            // --- Physics and Collision ---
            // (Perform physics calculations to update player's base Position first)
            Vector3 currentPosition = Position;
            Vector3 currentVelocity = Velocity;

            // Apply Gravity
            currentVelocity.Y -= Gravity * dt;

            // --- Player Movement Input ---
            Vector3 moveDir = Vector3.Zero;

            if (input.IsKeyDown(Keys.W)) moveDir += PlayerCamera.Front;
            if (input.IsKeyDown(Keys.S)) moveDir -= PlayerCamera.Front;
            if (input.IsKeyDown(Keys.A)) moveDir -= PlayerCamera.Right;
            if (input.IsKeyDown(Keys.D)) moveDir += PlayerCamera.Right;
            _shiftPressed = input.IsKeyDown(Keys.LeftShift);
            _ctrlPressed = input.IsKeyDown(Keys.LeftControl);
            moveDir.Y = 0; // Prevent flying
            if (moveDir.LengthSquared > 0) moveDir.Normalize();

            // Calculate intended displacement
            Vector3 horizontalDisplacement = _shiftPressed ? moveDir * (PlayerSpeed * 2) * dt : moveDir * PlayerSpeed * dt;
            Vector3 verticalDisplacement = new Vector3(0, currentVelocity.Y * dt, 0);

            // --- Collision Detection and Response ---
            Vector3 nextPosition;
            Vector3 collisionNormal;

            // 1. Check X-axis movement
            nextPosition = currentPosition + new Vector3(horizontalDisplacement.X, 0, 0);
            // Get nearby voxels for collision check
            List<Vector3> nearbyVoxelsX = _world.GetNearbyVoxelPositions(nextPosition);
            bool collisionX = _collisionManager.CheckWorldCollision(nextPosition, Size, nearbyVoxelsX, out collisionNormal);
            if (!collisionX)
            {
                currentPosition = nextPosition; // Move is valid
            }

            // 2. Check Z-axis movement
            nextPosition = currentPosition + new Vector3(0, 0, horizontalDisplacement.Z);
            // Get nearby voxels for collision check
            List<Vector3> nearbyVoxelsZ = _world.GetNearbyVoxelPositions(nextPosition);
            bool collisionZ = _collisionManager.CheckWorldCollision(nextPosition, Size, nearbyVoxelsZ, out collisionNormal);
            if (!collisionZ)
            {
                currentPosition = nextPosition; // Move is valid
            }

            // 3. Check Y-axis movement
            nextPosition = currentPosition + verticalDisplacement;
            _isOnGround = false; // Assume not on ground unless collision proves otherwise
            _canJump = false; // Assume can't jump unless landed

            // Get nearby voxels for collision check
            List<Vector3> nearbyVoxelsY = _world.GetNearbyVoxelPositions(nextPosition);
            bool collisionY = _collisionManager.CheckWorldCollision(nextPosition, Size, nearbyVoxelsY, out collisionNormal);
            if (collisionY)
            {
                // Collision detected vertically
                if (MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.X) &&
                    MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.Z))
                {
                    if (verticalDisplacement.Y < 0 && collisionNormal.Y > 0.5f) // Moving down, hit ground
                    {
                        currentVelocity.Y = 0; // Stop downward velocity
                        _isOnGround = true;
                        _canJump = true;
                        // Position remains currentPosition (don't apply verticalDisplacement)
                    }
                    else if (verticalDisplacement.Y > 0 && collisionNormal.Y < -0.5f) // Moving up, hit ceiling
                    {
                        currentVelocity.Y = 0; // Stop upward velocity
                        // Position remains currentPosition
                    }
                }
            }
            else
            {
                // No vertical collision, apply the full vertical movement
                currentPosition = nextPosition;
                _isOnGround = false; // Definitely in the air
            }

            // Handle Jump Input
            // Change IsKeyPressed to IsKeyDown to allow holding space to jump repeatedly
            if (input.IsKeyDown(Keys.Space) && _isOnGround) // Check if space is HELD and player is on ground
            {
                currentVelocity.Y = JumpForce;
                _isOnGround = false;
                _canJump = false;
            }

            // Final Update of player's base position happens here
            Position = currentPosition;
            Velocity = currentVelocity;

            // --- Update Camera Position based on new Player Position ---
            // Calculate the correct eye position *before* raycasting and mouse look
            Vector3 currentEyePosition = Position + Vector3.UnitY * EyeHeight;
            PlayerCamera.Position = currentEyePosition; // Update camera's internal position

            // Update cooldown timer
            if (_blockBreakCooldown > 0)
            {
                _blockBreakCooldown -= dt;
            }

            // --- Mouse Look ---
            // Process mouse look using the updated camera state
            if (!isFirstMove) // Removed CursorState check, assuming it's handled elsewhere or always grabbed
            {
                var deltaX = mouse.X - lastMousePos.X;
                var deltaY = mouse.Y - lastMousePos.Y;
                PlayerCamera.ProcessMouseMovement(deltaX, deltaY);
            }

            // --- Block Targeting Raycast (for highlighting) ---
            // Use the *updated* camera position and direction for the raycast
            if (_world.Raycast(PlayerCamera.Position, PlayerCamera.Front, 5.0f, out Vector3 hitBlockPos, out _)) // 5.0f is reach distance
            {
                // Store the integer coords of the hit block for highlighting
                TargetedBlockPosition = new Vector3i((int)Math.Floor(hitBlockPos.X), (int)Math.Floor(hitBlockPos.Y), (int)Math.Floor(hitBlockPos.Z));

                // --- Block Breaking Input (only if targeting a block) ---
                if (mouse.IsButtonDown(MouseButton.Left) && _blockBreakCooldown <= 0) // Left click to break
                {
                    _world.RemoveBlockAt(TargetedBlockPosition.Value); // Use the stored value
                    _blockBreakCooldown = BlockBreakInterval; // Reset cooldown
                    TargetedBlockPosition = null; // Clear target immediately after breaking to avoid re-highlighting instantly
                }
            }
            else
            {
                TargetedBlockPosition = null; // No block hit by raycast, clear highlight target
            }
        }

        public void UpdateAspectRatio(float aspectRatio)
        {
             if (aspectRatio > 0)
             {
                PlayerCamera.AspectRatio = aspectRatio;
             }
        }
    }
}
