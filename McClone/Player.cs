using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic; // Required for List<Vector3>

namespace VoxelGame
{
    public class Player
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; private set; } = Vector3.Zero;
        public Camera PlayerCamera { get; private set; }

        // Player Physics Properties
        private const float Gravity = 25.0f;
        private const float JumpForce = 9.0f;
        private const float PlayerSpeed = 5.0f;
        public Vector3 Size { get; } = new Vector3(0.6f, 1.8f, 0.6f); // Width, Height, Depth

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
            Position = startPosition;
            PlayerCamera = new Camera(startPosition, aspectRatio);
            _collisionManager = collisionManager;
            _world = world;
        }

        public void Update(float dt, KeyboardState input, MouseState mouse, bool isFirstMove, Vector2 lastMousePos)
        {
            // Update cooldown timer
            if (_blockBreakCooldown > 0)
            {
                _blockBreakCooldown -= dt;
            }

            // --- Mouse Look ---
            if (!isFirstMove)
            {
                var deltaX = mouse.X - lastMousePos.X;
                var deltaY = mouse.Y - lastMousePos.Y;
                PlayerCamera.ProcessMouseMovement(deltaX, deltaY);
            }

            // --- Block Breaking Input ---
            if (mouse.IsButtonDown(MouseButton.Left) && _blockBreakCooldown <= 0) // Left click to break
            {
                if (_world.Raycast(PlayerCamera.Position, PlayerCamera.Front, 5.0f, out Vector3 hitBlockPos, out _)) // 5.0f is reach distance
                {
                    Vector3i blockToRemove = new Vector3i((int)Math.Floor(hitBlockPos.X), (int)Math.Floor(hitBlockPos.Y), (int)Math.Floor(hitBlockPos.Z));
                    _world.RemoveBlockAt(blockToRemove);
                    _blockBreakCooldown = BlockBreakInterval; // Reset cooldown
                }
            }

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

            // --- Physics and Collision ---
            Vector3 currentPosition = Position;
            Vector3 currentVelocity = Velocity;

            // Apply Gravity
            currentVelocity.Y -= Gravity * dt;

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

            // Final Update
            Position = currentPosition;
            Velocity = currentVelocity;

            // Update Camera position to match player
            PlayerCamera.Position = Position;
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
