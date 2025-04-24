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
        private const float MouseSensitivity = 0.002f;

        private Vector3 _velocity;
        private bool _isGrounded;
        private CollisionManager _collisionManager;
        private World.World _world;

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
            moveDir.Y = 0; // Prevent flying
            if (moveDir.LengthSquared > 0) moveDir.Normalize();

            // Calculate intended displacement
            Vector3 horizontalDisplacement = moveDir * MoveSpeed * dt;
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
                _velocity.Y = JumpForce;
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
