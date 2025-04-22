using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework; // For Keys enum

namespace VoxelGame;

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
        return Matrix4.CreatePerspectiveFieldOfView(_fov, AspectRatio, 0.1f, 100.0f);
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
            if (Pitch > 89.0f)
                Pitch = 89.0f;
            if (Pitch < -89.0f)
                Pitch = -89.0f;
        }

        UpdateVectors();
    }

    // Public accessors for direction vectors if needed
    public Vector3 Front => _front;
    public Vector3 Up => _up;
    public Vector3 Right => _right;
}