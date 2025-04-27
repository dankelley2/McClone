using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework; // Needed for GLFW and Monitors
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace VoxelGame.Core
{
    public static class Program
    {
        public static void Main()
        {
            Console.WriteLine("Starting Voxel Game...");

            if (!GLFW.Init())
            {
                Console.WriteLine("Failed to initialize GLFW");
                return;
            }

            // Request multisampling for Anti-Aliasing (e.g., 4 samples)
            GLFW.WindowHint(WindowHintInt.Samples, 4);
            Console.WriteLine("==> Requested 4x MSAA");

            // Keep the hint you found worked best or the recommended one
            GLFW.WindowHint(WindowHintBool.ScaleFramebuffer, true); // Or CocoaRetinaFramebuffer if you switched back
            // Console.WriteLine("==> Set WindowHintBool.ScaleFramebuffer hint to TRUE");

            // --- Configure for Fullscreen ---
            // Get the primary monitor
            var primaryMonitor = Monitors.GetPrimaryMonitor();
            var resolution = primaryMonitor.ClientArea.Size; // Use ClientArea for full resolution

            var nativeWindowSettings = new NativeWindowSettings()
            {
                // Use the monitor's resolution
                ClientSize = resolution,
                Title = "OpenTK Voxel Game (Fullscreen)",
                APIVersion = new Version(3, 3),
                Flags = ContextFlags.ForwardCompatible,

                // Set the window state to Fullscreen
                WindowState = WindowState.Fullscreen,

                // Explicitly set the monitor for fullscreen
                CurrentMonitor = primaryMonitor.Handle
            };
            Console.WriteLine($"==> Attempting to start in {nativeWindowSettings.WindowState} mode at {resolution.X}x{resolution.Y}.");
            // --- End Fullscreen Configuration ---


            using (var game = new Game(GameWindowSettings.Default, nativeWindowSettings))
            {
                // Setting UpdateFrequency is fine
                game.UpdateFrequency = 120.0;
                // RenderFrequency is often determined by VSync in fullscreen

                game.Run();
            }

            GLFW.Terminate();
            Console.WriteLine("Game closed.");
        }
    }
}