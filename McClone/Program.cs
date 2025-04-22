using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace VoxelGame;

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Starting Voxel Game...");

        var nativeWindowSettings = new NativeWindowSettings()
        {
            Size = new Vector2i(1280, 720),
            Title = "OpenTK Voxel Game",
            // Set API version (e.g., OpenGL 3.3)
            APIVersion = new Version(3, 3),
            Flags = OpenTK.Windowing.Common.ContextFlags.ForwardCompatible, // Important for macOS
        };

        using (var game = new Game(GameWindowSettings.Default, nativeWindowSettings))
        {
            // Optional: Set update/render frequency
            game.UpdateFrequency = 60.0;
            // game.RenderFrequency = 60.0;

            game.Run();
        }

         Console.WriteLine("Game closed.");
    }
}