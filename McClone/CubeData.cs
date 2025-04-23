using OpenTK.Graphics.OpenGL4; // Required for CheckGLError if kept here
using System;

namespace VoxelGame
{
    public static class CubeData
    {
        // Stride is 9: 3 Pos, 3 Color, 3 Normal
        public const int VertexStride = 9;
        public static float[] Vertices { get; private set; } = null!;
        public static int VertexCount { get; private set; } = 0;

        public static void GenerateVertices()
        {
            Vertices = new float[]
            {
                // Position          Color           Normal
                // Front face (+Z) - Normal (0, 0, 1) - Red
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                 0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                -0.5f,  0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,
                -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f,

                // Back face (-Z) - Normal (0, 0, -1) - Green
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, -1.0f,

                // Left face (-X) - Normal (-1, 0, 0) - Blue
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, 0.0f, 0.0f, 1.0f, -1.0f, 0.0f, 0.0f,

                // Right face (+X) - Normal (1, 0, 0) - Yellow
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 1.0f, 0.0f, 1.0f, 0.0f, 0.0f,

                // Bottom face (-Y) - Normal (0, -1, 0) - Cyan
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                -0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                 0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,
                -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f, 0.0f, -1.0f, 0.0f,

                // Top face (+Y) - Normal (0, 1, 0) - Magenta
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f
            };
            VertexCount = Vertices.Length / VertexStride;
            // CheckGLError("CubeData.GenerateVertices"); // Consider moving CheckGLError to a utility class
        }

        // Optional: Helper to check for errors if kept here
        private static void CheckGLError(string stage)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
            }
        }
    }
}
