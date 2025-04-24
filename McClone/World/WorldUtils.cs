using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace VoxelGame.World
{
    public static class WorldUtils
    {
        public static Vector2i GetChunkCoords(Vector3 worldPosition, int chunkSize)
        {
            int chunkX = (int)Math.Floor(worldPosition.X / chunkSize);
            int chunkZ = (int)Math.Floor(worldPosition.Z / chunkSize);
            return new Vector2i(chunkX, chunkZ);
        }

        public static void CheckGLError(string stage)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
            }
        }
    }
}
