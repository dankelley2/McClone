using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.IO;
using VoxelGame.Rendering; // For Shader class

namespace VoxelGame.Rendering
{
    public class Ui : IDisposable
    {
        private Shader _uiShader;
        private int _rectVao;
        private int _rectVbo;

        // Simple quad vertices (position only)
        private readonly float[] _rectVertices =
        {
            // positions
            0.0f, 1.0f, // top left
            0.0f, 0.0f, // bottom left
            1.0f, 0.0f, // bottom right

            0.0f, 1.0f, // top left
            1.0f, 0.0f, // bottom right
            1.0f, 1.0f  // top right
        };

        public Ui()
        {
            // Shader paths (relative to executable)
            string baseDirectory = AppContext.BaseDirectory;
            string vertexPath = Path.Combine(baseDirectory, "Assets", "Shaders", "ui.vert");
            string fragmentPath = Path.Combine(baseDirectory, "Assets", "Shaders", "ui.frag");
            _uiShader = new Shader(vertexPath, fragmentPath);

            // --- Setup Rectangle VAO/VBO ---
            _rectVao = GL.GenVertexArray();
            _rectVbo = GL.GenBuffer();

            GL.BindVertexArray(_rectVao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _rectVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _rectVertices.Length * sizeof(float), _rectVertices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0); // Unbind VBO
            GL.BindVertexArray(0); // Unbind VAO
        }

        public void DrawRectangle(Vector2 position, Vector2 size, Color4 color, Matrix4 projection)
        {
            _uiShader.Use();

            // Set projection matrix (orthographic for UI)
            _uiShader.SetMatrix4("projection", projection);

            // Set model matrix (position and scale)
            Matrix4 model = Matrix4.Identity;
            model *= Matrix4.CreateScale(size.X, size.Y, 1.0f);
            model *= Matrix4.CreateTranslation(position.X, position.Y, 0.0f);
            _uiShader.SetMatrix4("model", model);

            // Set color
            _uiShader.SetVector4("objectColor", new Vector4(color.R, color.G, color.B, color.A));

            // Draw the rectangle
            GL.BindVertexArray(_rectVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0); // Unbind VAO
        }

        // Placeholder for text rendering
        public void DrawText(string text, Vector2 position, float scale, Color4 color, Matrix4 projection)
        {
            // Text rendering requires a font library and more complex setup
            // For now, this is just a placeholder.
            Console.WriteLine($"TODO: Render text '{text}' at {position}");
        }


        public void Dispose()
        {
            _uiShader?.Dispose();
            GL.DeleteBuffer(_rectVbo);
            GL.DeleteVertexArray(_rectVao);
            GC.SuppressFinalize(this);
        }

        ~Ui()
        {
             Console.WriteLine("Warning: UI Resource leak detected. Dispose was not called.");
             // In a debug build, you might want to throw an exception or log more details.
             // We cannot call GL functions here because this runs on the finalizer thread.
        }
    }
}
