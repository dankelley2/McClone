using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework; // For Keys enum
using System; // Added for MathF

namespace VoxelGame.Rendering
{
    public class Camera
    {
        public Vector3 Position { get; set; }
        public float Pitch { get; private set; } // Rotation around X axis (up/down)
        public float Yaw { get; private set; }   // Rotation around Y axis (left/right)
        public float AspectRatio { get; set; }

        private Vector3 _front = -Vector3.UnitZ;
        private Vector3 _up = Vector3.UnitY;
        private Vector3 _right = Vector3.UnitX;

        private float _fov = MathHelper.DegreesToRadians(70.0f); // 70 degrees FOV
        private float _sensitivity = 0.1f;

        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            Yaw = -90.0f; // Start facing towards -Z
            Pitch = 0.0f;
            UpdateVectors();
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + _front, _up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(_fov, AspectRatio, 0.1f, 200.0f);
        }

        public void UpdateVectors()
        {
            // Calculate new Front vector
            float pitchRad = MathHelper.DegreesToRadians(Pitch);
            float yawRad = MathHelper.DegreesToRadians(Yaw);

            _front.X = MathF.Cos(pitchRad) * MathF.Cos(yawRad);
            _front.Y = MathF.Sin(pitchRad);
            _front.Z = MathF.Cos(pitchRad) * MathF.Sin(yawRad);
            _front = Vector3.Normalize(_front);

            // Recalculate Right and Up vectors
            _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
            _up = Vector3.Normalize(Vector3.Cross(_right, _front));
        }

         public void ProcessMouseMovement(float xoffset, float yoffset, bool constrainPitch = true)
        {
            xoffset *= _sensitivity;
            yoffset *= _sensitivity;

            Yaw += xoffset;
            Pitch -= yoffset; // Reversed since y-coordinates go from bottom to top

            // Constrain pitch to avoid flipping
            if (constrainPitch)
            {
                Pitch = Math.Clamp(Pitch, -89.0f, 89.0f); // Use Math.Clamp
            }

            UpdateVectors();
        }

        /// <summary>
        /// Sets the camera's pitch and yaw directly and updates direction vectors.
        /// Also updates the camera's position based on the player's base position and new orientation.
        /// </summary>
        /// <param name="pitch">The new pitch value.</param>
        /// <param name="yaw">The new yaw value.</param>
        /// <param name="playerPosition">The player's base position (feet level).</param>
        /// <param name="playerHeight">The player's height.</param>
        public void SetOrientation(float pitch, float yaw, Vector3 playerPosition, float playerHeight)
        {
            Pitch = Math.Clamp(pitch, -89.0f, 89.0f); // Clamp pitch
            Yaw = yaw;
            UpdateVectors();
            // Recalculate camera position based on player's base position and height
            Position = playerPosition + Vector3.UnitY * playerHeight * 0.9f;
        }

        // Overload for setting orientation without explicitly providing player position/height
        // Assumes the Position property is already correctly set or will be set immediately after.
        public void SetOrientation(float pitch, float yaw)
        {
            Pitch = Math.Clamp(pitch, -89.0f, 89.0f); // Clamp pitch
            Yaw = yaw;
            UpdateVectors();
            // Note: This overload doesn't automatically adjust Position based on player height.
            // Ensure Position is set correctly elsewhere when using this.
        }


        // Public accessors for direction vectors if needed
        public Vector3 Front => _front;
        public Vector3 Up => _up;
        public Vector3 Right => _right;
    }
}