using SharpNoise.Modules;

namespace VoxelGame.World
{
    public class WorldGeneration
    {
        public Perlin NoiseModule { get; }
        public const float NoiseScale = 0.03f;
        public const int NoiseOctaves = 6;
        public const float TerrainAmplitude = 18f;
        public const int BaseHeight = 50;
        public const int WaterLevel = 55; // Define the water level

        public WorldGeneration(int seed = 5)
        {
            NoiseModule = new Perlin
            {
                Seed = seed,
                OctaveCount = NoiseOctaves,
                Persistence = 0.5,
                Lacunarity = 1.7
            };
        }

        public double GetNoiseValue(int worldX, int worldZ)
        {
            return NoiseModule.GetValue(worldX * NoiseScale, 0, worldZ * NoiseScale);
        }
    }
}
