using OpenTK.Graphics.OpenGL4; // Required for CheckGLError if kept here
using System;

namespace VoxelGame.World
{
    public static class CubeData
    {
        // Stride is 11: 3 Pos, 3 Normal, 2 TexCoord
        public const int VertexStride = 8;
        public static float[] Vertices { get; private set; } = null!;
        public static int VertexCount { get; private set; } = 0;

        public static void GenerateVertices()
        {
            float uMinTop = 0f, uMaxTop = 1f, vMinTop = 0.666f, vMaxTop = 1.0f;
            float uMinSide = 0f, uMaxSide = 1f, vMinSide = 0.333f, vMaxSide = 0.666f;
            float uMinBottom = 0f, uMaxBottom = 1f, vMinBottom = 0.0f, vMaxBottom = 0.333f;

            // OpenGL UV origin (0,0) is bottom-left. Adjust if texture appears flipped.
            // If textures are upside down, you might need to swap vMin/vMax or uncomment
            // StbImage.stbi_set_flip_vertically_on_load(1); in Texture.cs

            
            Vertices = new float[]
            {
                // Position          Color(Unused)   Normal          TexCoord (UV)

                // Front face (+Z) - Normal (0, 0, 1) - Use Side Texture (Rotated 180)
                -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV
                 0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV
                 0.5f,  0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV
                 0.5f,  0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV
                -0.5f,  0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV
                -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV

                // Back face (-Z) - Normal (0, 0, -1) - Use Side Texture (Rotated 180)
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV
                 0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV
                 0.5f,  0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV
                 0.5f,  0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV
                -0.5f,  0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV
                -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV

                // Left face (-X) - Normal (-1, 0, 0) - Use Side Texture (Rotated 180)
                -0.5f,  0.5f,  0.5f, -1.0f, 0.0f, 0.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV
                -0.5f, -0.5f,  0.5f, -1.0f, 0.0f, 0.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV
                -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV
                -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV
                -0.5f,  0.5f, -0.5f, -1.0f, 0.0f, 0.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV
                -0.5f,  0.5f,  0.5f, -1.0f, 0.0f, 0.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV

                // Right face (+X) - Normal (1, 0, 0) - Use Side Texture (Rotated 180)
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 0.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV
                 0.5f,  0.5f, -0.5f, 1.0f, 0.0f, 0.0f, uMinSide, vMinSide, // Top-right -> Maps to Bottom-Left UV
                 0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV
                 0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f, uMinSide, vMaxSide, // Bottom-right -> Maps to Top-Left UV
                 0.5f, -0.5f,  0.5f, 1.0f, 0.0f, 0.0f, uMaxSide, vMaxSide, // Bottom-left -> Maps to Top-Right UV
                 0.5f,  0.5f,  0.5f, 1.0f, 0.0f, 0.0f, uMaxSide, vMinSide, // Top-left -> Maps to Bottom-Right UV

                // Bottom face (-Y) - Normal (0, -1, 0) - Use TOP Texture (Flipped V)
                -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, uMinTop, vMinTop, // Top-left -> Maps to Bottom-Left UV of Top Texture
                -0.5f, -0.5f,  0.5f, 0.0f, -1.0f, 0.0f, uMinTop, vMaxTop, // Bottom-left -> Maps to Top-Left UV of Top Texture
                 0.5f, -0.5f,  0.5f, 0.0f, -1.0f, 0.0f, uMaxTop, vMaxTop, // Bottom-right -> Maps to Top-Right UV of Top Texture
                 0.5f, -0.5f,  0.5f, 0.0f, -1.0f, 0.0f, uMaxTop, vMaxTop, // Bottom-right -> Maps to Top-Right UV of Top Texture
                 0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, uMaxTop, vMinTop, // Top-right -> Maps to Bottom-Right UV of Top Texture
                -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, uMinTop, vMinTop, // Top-left -> Maps to Bottom-Left UV of Top Texture

                // Top face (+Y) - Normal (0, 1, 0) - Use BOTTOM Texture (Original Mapping)
                -0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, uMinBottom, vMaxBottom, // Top-left vertex maps to Top-Left UV of Bottom Texture
                 0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, uMaxBottom, vMaxBottom, // Top-right vertex maps to Top-Right UV of Bottom Texture
                 0.5f,  0.5f,  0.5f, 0.0f, 1.0f, 0.0f, uMaxBottom, vMinBottom, // Bottom-right vertex maps to Bottom-Right UV of Bottom Texture
                 0.5f,  0.5f,  0.5f, 0.0f, 1.0f, 0.0f, uMaxBottom, vMinBottom, // Bottom-right vertex maps to Bottom-Right UV of Bottom Texture
                -0.5f,  0.5f,  0.5f, 0.0f, 1.0f, 0.0f, uMinBottom, vMinBottom, // Bottom-left vertex maps to Bottom-Left UV of Bottom Texture
                -0.5f,  0.5f, -0.5f, 0.0f, 1.0f, 0.0f, uMinBottom, vMaxBottom  // Top-left vertex maps to Top-Left UV of Bottom Texture
            };
            VertexCount = Vertices.Length / VertexStride;
            // CheckGLError("CubeData.GenerateVertices"); // Consider moving CheckGLError to a utility class
        }
    }
}
