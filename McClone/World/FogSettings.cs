using OpenTK.Mathematics;

namespace VoxelGame.World
{
    public class FogSettings
    {
        public Vector3 FogColor { get; set; } = new Vector3(0.5f, 0.75f, 0.9f);
        public float FogDensity { get; set; } = 0.01f;
        public float FogGradient { get; set; } = 2.5f;
    }
}
