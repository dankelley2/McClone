using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.IO;

namespace VoxelGame.Rendering
{
    public class Texture : IDisposable
    {
        public int Handle { get; private set; }
        private bool _disposedValue = false;

        public Texture(string path)
        {
            Handle = GL.GenTexture();
            Use(); // Bind the texture unit

            // Load the image
            string fullPath = Path.Combine(AppContext.BaseDirectory, path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Texture file not found: {fullPath}");
            }

            // StbImage.stbi_set_flip_vertically_on_load(1); // Uncomment if your texture appears upside down

            using (Stream stream = File.OpenRead(fullPath))
            {
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha); // Load as RGBA

                if (image.Data == null || image.Width == 0 || image.Height == 0)
                {
                    throw new Exception($"Failed to load texture file: {fullPath}");
                }

                Console.WriteLine($"Loaded Texture: {path}, Width: {image.Width}, Height: {image.Height}");

                // Generate the texture
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            }

            // Set texture parameters
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest); // Use nearest for blocky look
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest); // Use nearest for blocky look

            // Optional: Generate Mipmaps for better performance/quality at distance (might blur blocky textures)
            // GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            // GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);

            CheckGLError($"Texture.Load ({path})");
        }

        public void Use(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                GL.DeleteTexture(Handle);
                _disposedValue = true;
                Console.WriteLine($"Disposed Texture {Handle}");
            }
        }

        ~Texture()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

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
