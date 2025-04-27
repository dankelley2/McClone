using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing; // Correct namespace
using System.Collections.Generic;
using System.Linq; // Needed for FirstOrDefault

namespace VoxelGame.Rendering
{
    public class Ui : IDisposable
    {
        private Shader _uiShader;
        private int _rectVao;
        private int _rectVbo;
        private int _textVao;
        private int _textVbo;
        private int _textTexture;

        private FontCollection _fontCollection;
        private FontFamily _fontFamily;
        private Font _font;

        // Quad vertices with texture coordinates
        private readonly float[] _quadVertices =
        {
            // positions // texCoords
            0.0f, 1.0f,  0.0f, 1.0f, // top left
            0.0f, 0.0f,  0.0f, 0.0f, // bottom left
            1.0f, 0.0f,  1.0f, 0.0f, // bottom right

            0.0f, 1.0f,  0.0f, 1.0f, // top left
            1.0f, 0.0f,  1.0f, 0.0f, // bottom right
            1.0f, 1.0f,  1.0f, 1.0f  // top right
        };

        public Ui()
        {
            // Shader paths
            string baseDirectory = AppContext.BaseDirectory;
            string vertexPath = Path.Combine(baseDirectory, "Assets", "Shaders", "ui.vert");
            string fragmentPath = Path.Combine(baseDirectory, "Assets", "Shaders", "ui.frag");
            _uiShader = new Shader(vertexPath, fragmentPath);

            // --- Load Font ---
            _fontCollection = new FontCollection();
            string fontDirectory = Path.Combine(baseDirectory, "Assets", "Fonts");
            // Ensure the Fonts directory exists
            if (!Directory.Exists(fontDirectory))
            {
                 Console.WriteLine($"Warning: Font directory not found at '{fontDirectory}'. Creating it.");
                 Directory.CreateDirectory(fontDirectory);
            }
            // Attempt to load a default font - replace "arial.ttf" with your font file
            string fontPath = Path.Combine(fontDirectory, "arial.ttf"); // Make sure arial.ttf exists here!

            try
            {
                if (File.Exists(fontPath))
                {
                    _fontFamily = _fontCollection.Add(fontPath);
                    Console.WriteLine($"Loaded font: {fontPath}");
                }
                else
                {
                    Console.WriteLine($"Warning: Font file '{fontPath}' not found. Attempting system default.");
                    _fontFamily = SystemFonts.Families.FirstOrDefault();
                    if (_fontFamily == null)
                    {
                        throw new Exception("No suitable font found (arial.ttf missing and no system default available).");
                    }
                     Console.WriteLine($"Using system default font '{_fontFamily.Name}'.");
                }
                _font = _fontFamily.CreateFont(16); // Create a default font instance
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR loading font: {ex.Message}");
                // Handle fatal error - perhaps rethrow or set a flag indicating UI failure
                throw;
            }


            // --- Setup Rectangle VAO/VBO (Now includes TexCoords) ---
            _rectVao = GL.GenVertexArray();
            _rectVbo = GL.GenBuffer();

            GL.BindVertexArray(_rectVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _rectVbo);
            // Use _quadVertices which includes texture coordinates
            GL.BufferData(BufferTarget.ArrayBuffer, _quadVertices.Length * sizeof(float), _quadVertices, BufferUsageHint.StaticDraw);

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            // Texture coord attribute (location 1)
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            // --- Setup Text Rendering Resources ---
            _textVao = GL.GenVertexArray();
            _textVbo = GL.GenBuffer();
            _textTexture = GL.GenTexture();

            GL.BindVertexArray(_textVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _textVbo);
            // Buffer size for a quad (6 vertices, 4 floats each: pos + texcoord)
            GL.BufferData(BufferTarget.ArrayBuffer, _quadVertices.Length * sizeof(float), _quadVertices, BufferUsageHint.DynamicDraw); // Use DynamicDraw for text

            // Position attribute (location 0)
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            // Texture coord attribute (location 1)
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            // Configure text texture
            GL.BindTexture(TextureTarget.Texture2D, _textTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            // Revert back to Linear filtering
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void DrawRectangle(Vector2 position, Vector2 size, OpenTK.Mathematics.Color4 color, Matrix4 projection)
        {
            _uiShader.Use();
            _uiShader.SetMatrix4("projection", projection);
            _uiShader.SetBool("useTexture", false); // Tell shader not to use texture

            Matrix4 model = Matrix4.Identity;
            model *= Matrix4.CreateScale(size.X, size.Y, 1.0f);
            model *= Matrix4.CreateTranslation(position.X, position.Y, 0.0f);
            _uiShader.SetMatrix4("model", model);

            _uiShader.SetVector4("objectColor", new Vector4(color.R, color.G, color.B, color.A));

            GL.BindVertexArray(_rectVao); // Use the simple rect VAO
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        public void DrawText(string text, Vector2 position, float size, OpenTK.Mathematics.Color4 color, Matrix4 projection)
        {
             if (string.IsNullOrEmpty(text)) return;

             // 1. Create Font instance
             Font currentFont = _fontFamily.CreateFont(size);

             // 2. Measure Text using RichTextOptions
             var textOptions = new RichTextOptions(currentFont)
             {
                 Origin = System.Numerics.Vector2.Zero // Measure from top-left
                 // Dpi = 72 // Temporarily removed DPI setting
             };
             FontRectangle measuredSize = TextMeasurer.MeasureSize(text, textOptions);

             // If height is still zero, use font size as a fallback estimate
             float calculatedHeight = measuredSize.Height;
             if (calculatedHeight <= 0)
             {
                 Console.WriteLine($"[Debug DrawText] Warning: Measured height is zero. Using font size ({currentFont.Size}) as fallback height.");
                 calculatedHeight = currentFont.Size; // Use font size as a rough fallback
             }

             // 3. Create Image and Draw Text
             const int heightPadding = 2; // Add a couple of pixels padding
             const int widthPadding = 4; // Add horizontal padding
             int imgWidth = Math.Max(1, (int)Math.Ceiling(measuredSize.Width)) + widthPadding; // Add width padding
             int imgHeight = Math.Max(1, (int)Math.Ceiling(calculatedHeight)) + heightPadding; // Use calculatedHeight + padding
 
             using (var img = new Image<Rgba32>(imgWidth, imgHeight))
             {
                 // Use RichTextOptions for drawing as well
                 img.Mutate(ctx => ctx.DrawText(textOptions, text, SixLabors.ImageSharp.Color.White));

                 // 4. Upload Image Data to OpenGL Texture
                 GL.ActiveTexture(TextureUnit.Texture0);
                 GL.BindTexture(TextureTarget.Texture2D, _textTexture);

                 // Get pixel data using direct access (ImageSharp 3+)
                 var pixels = new byte[imgWidth * imgHeight * 4];
                 int index = 0;
                 for (int y = 0; y < img.Height; y++)
                 {
                     for (int x = 0; x < img.Width; x++)
                     {
                         Rgba32 pixel = img[x, y]; // Access pixel directly
                         pixels[index++] = 255; // R
                         pixels[index++] = 255; // G
                         pixels[index++] = 255; // B
                         pixels[index++] = pixel.A; // Alpha
                     }
                 }

                 GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, imgWidth, imgHeight, 0,
                               PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

             } // Dispose image

             // 5. Render the Textured Quad
             _uiShader.Use();
             _uiShader.SetMatrix4("projection", projection);
             _uiShader.SetBool("useTexture", true); // Use texture rendering path
             _uiShader.SetInt("textSampler", 0); // Texture unit 0
             _uiShader.SetVector4("objectColor", new Vector4(color.R, color.G, color.B, color.A));

             // Adjust model matrix for text position and scale (using image dimensions)
             Matrix4 model = Matrix4.Identity;
             // Scale the quad to the size of the rendered text image
             model *= Matrix4.CreateScale(imgWidth, imgHeight, 1.0f);
             // Position the quad. Note that SixLabors Origin (0,0) is top-left,
             // but our quad's origin (0,0) is bottom-left before scaling/translation.
             // The orthographic projection handles the Y-axis direction.
             // We need to translate so the top-left corner aligns with the requested 'position'.
             // Since the quad spans 0->1 before scaling, translating by 'position' puts the bottom-left corner there.
             // To put the top-left corner at 'position', we need to adjust Y by the height.
             // However, the orthographic projection flips Y, so translating by (position.X, position.Y)
             // correctly places the top-left corner.
             model *= Matrix4.CreateTranslation(position.X, position.Y, 0.0f);

             _uiShader.SetMatrix4("model", model);

             GL.BindVertexArray(_textVao); // Use the text VAO
             // We don't need to update VBO data here if using the standard quad vertices
             // The model matrix handles positioning and scaling.
             GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

             GL.BindVertexArray(0);
             GL.BindTexture(TextureTarget.Texture2D, 0); // Unbind texture
        }


        public void Dispose()
        {
            _uiShader?.Dispose();
            GL.DeleteBuffer(_rectVbo);
            GL.DeleteVertexArray(_rectVao);
            GL.DeleteBuffer(_textVbo);
            GL.DeleteVertexArray(_textVao);
            GL.DeleteTexture(_textTexture);
            GC.SuppressFinalize(this);
        }

        ~Ui()
        {
             Console.WriteLine("Warning: UI Resource leak detected. Dispose was not called.");
             // Cannot call GL functions here.
        }
    }
}
