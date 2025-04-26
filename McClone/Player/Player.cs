using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using VoxelGame.World;
using VoxelGame.Rendering; // For Camera

namespace VoxelGame.Player
{
    public class Player
    {
        public Camera PlayerCamera { get; private set; }
        public Vector3 Position { get; set; }
        public Vector3 Size { get; } = new Vector3(0.6f, 1.8f, 0.6f); // Player dimensions (width, height, depth)

        private const float Gravity = -19.81f; // Adjusted gravity
        private const float MoveSpeed = 5.0f;
        private const float JumpForce = 8.0f;
        public Vector3i? TargetedBlockPosition { get; private set; } // Store the position of the block being looked at

        private Vector3 _velocity;
        private bool _isGrounded;
        private CollisionManager _collisionManager;
        private World.World _world;
        private float _blockBreakCooldown = 0.0f; // Cooldown timer for breaking
        private float _blockPlaceCooldown = 0.0f; // Cooldown timer for placing
        private const float BlockActionInterval = 0.1f; // Time between block actions (break or place)

        public Player(Vector3 startPosition, float aspectRatio, CollisionManager collisionManager, World.World world)
        {
            Position = startPosition;
            // Initialize camera at the correct eye level
            PlayerCamera = new Camera(startPosition + Vector3.UnitY * Size.Y * 0.9f, aspectRatio);
            _collisionManager = collisionManager;
            _world = world;
        }

        public void Update(float dt, KeyboardState input, MouseState mouse, bool firstMove, Vector2 lastMousePos)
        {
            // --- Physics and Collision ---
            // (Perform physics calculations to update player's base Position first)
            Vector3 currentPosition = Position;
            Vector3 currentVelocity = _velocity;

            // Apply gravity
            currentVelocity.Y += Gravity * dt;

            // --- Player Movement Input ---
            Vector3 moveDir = Vector3.Zero;

            if (input.IsKeyDown(Keys.W)) moveDir += PlayerCamera.Front;
            if (input.IsKeyDown(Keys.S)) moveDir -= PlayerCamera.Front;
            if (input.IsKeyDown(Keys.A)) moveDir -= PlayerCamera.Right;
            if (input.IsKeyDown(Keys.D)) moveDir += PlayerCamera.Right;
            var shiftPressed = input.IsKeyDown(Keys.LeftShift);
            moveDir.Y = 0; // Prevent flying
            if (moveDir.LengthSquared > 0) moveDir.Normalize();

            // Calculate intended displacement
            Vector3 horizontalDisplacement = shiftPressed ? moveDir * (MoveSpeed * 4) * dt : moveDir * MoveSpeed * dt;
            Vector3 verticalDisplacement = new Vector3(0, currentVelocity.Y * dt, 0);

            // --- Collision Detection and Response ---
            Vector3 nextPosition;
            Vector3 collisionNormal;

            // 1. Check X-axis movement
            nextPosition = currentPosition + new Vector3(horizontalDisplacement.X, 0, 0);
            // Get nearby voxels for collision check
            var nearbyVoxelsX = _world.GetNearbyVoxelPositions(nextPosition);
            bool collisionX = _collisionManager.CheckWorldCollision(nextPosition, Size, nearbyVoxelsX, out collisionNormal);
            if (!collisionX)
            {
                currentPosition = nextPosition; // Move is valid
            }

            // 2. Check Z-axis movement
            nextPosition = currentPosition + new Vector3(0, 0, horizontalDisplacement.Z);
            // Get nearby voxels for collision check
            var nearbyVoxelsZ = _world.GetNearbyVoxelPositions(nextPosition);
            bool collisionZ = _collisionManager.CheckWorldCollision(nextPosition, Size, nearbyVoxelsZ, out collisionNormal);
            if (!collisionZ)
            {
                currentPosition = nextPosition; // Move is valid
            }

            // 3. Check Y-axis movement
            nextPosition = currentPosition + verticalDisplacement;
            bool collisionY = _collisionManager.CheckWorldCollision(nextPosition, Size, _world.GetNearbyVoxelPositions(nextPosition), out collisionNormal);
            if (collisionY)
            {
                // Collision detected vertically
                if (MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.X) &&
                    MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.Z))
                {
                    if (verticalDisplacement.Y < 0 && collisionNormal.Y > 0.5f) // Moving down, hit ground
                    {
                        currentVelocity.Y = 0; // Stop downward velocity
                        _isGrounded = true;
                    }
                    else if (verticalDisplacement.Y > 0 && collisionNormal.Y < -0.5f) // Moving up, hit ceiling
                    {
                        currentVelocity.Y = 0; // Stop upward velocity
                    }
                }
            }
            else
            {
                // No vertical collision, apply the full vertical movement
                currentPosition = nextPosition;
                _isGrounded = false; // Definitely in the air
            }

            // Jump logic
            if (input.IsKeyDown(Keys.Space) && _isGrounded)
            {
                currentVelocity.Y = shiftPressed ? JumpForce * 4 : JumpForce;
                _isGrounded = false;
            }

            // Final Update of player's base position happens here
            Position = currentPosition;
            _velocity = currentVelocity;

            // --- Update Camera Position based on new Player Position ---
            // Calculate the correct eye position *before* raycasting and mouse look
            Vector3 currentEyePosition = Position + Vector3.UnitY * Size.Y * 0.9f;
            PlayerCamera.Position = currentEyePosition; // Update camera's internal position


            // --- Mouse Look ---
            // Process mouse look using the updated camera state
            if (!firstMove) // Removed CursorState check, assuming it's handled elsewhere or always grabbed
            {
                var deltaX = mouse.X - lastMousePos.X;
                var deltaY = mouse.Y - lastMousePos.Y;
                PlayerCamera.ProcessMouseMovement(deltaX, deltaY);
            }

            // Update cooldown timers
            if (_blockBreakCooldown > 0)
            {
                _blockBreakCooldown -= dt;
            }
            if (_blockPlaceCooldown > 0)
            {
                _blockPlaceCooldown -= dt;
            }

            // --- Block Targeting Raycast (for highlighting and actions) ---
            // Use the *updated* camera position and direction for the raycast
            // Increased reach slightly for placement consistency
            const float reachDistance = 10.0f; 
            bool isTargetingBlock = _world.Raycast(PlayerCamera.Position, PlayerCamera.Front, reachDistance, out Vector3 hitBlockPos, out Vector3 adjacentBlockPos);

            if (isTargetingBlock)
            {
                // Store the integer coords of the hit block for highlighting
                TargetedBlockPosition = new Vector3i((int)Math.Floor(hitBlockPos.X), (int)Math.Floor(hitBlockPos.Y), (int)Math.Floor(hitBlockPos.Z));

                // --- Block Breaking Input (Left Click) ---
                if (mouse.IsButtonDown(MouseButton.Left) && _blockBreakCooldown <= 0) 
                {
                    var success = _world.RemoveBlockAt(TargetedBlockPosition.Value); // Use the stored value
                    if (success) {
                        _blockBreakCooldown = BlockActionInterval; // Reset break cooldown
                        _blockPlaceCooldown = BlockActionInterval; // Also reset place cooldown to prevent instant place after break
                        TargetedBlockPosition = null; // Clear target immediately after breaking
                    }
                }
                // --- Block Placing Input (Right Click) ---
                else if (mouse.IsButtonDown(MouseButton.Right) && _blockPlaceCooldown <= 0) 
                {
                    // Use the AddBlockFromNormalVector which uses the adjacentBlockPos from the raycast
                    var success = _world.AddBlockFromNormalVector(PlayerCamera, reachDistance); // Default newState is 1 (solid)
                    if (success) {
                        _blockPlaceCooldown = BlockActionInterval; // Reset place cooldown
                        _blockBreakCooldown = BlockActionInterval; // Also reset break cooldown
                        // No need to clear TargetedBlockPosition here, as placement doesn't remove the target
                    }
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
