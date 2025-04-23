using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System;

namespace VoxelGame;

public class Game : GameWindow
{
    private Shader _shader = null!;
    private Camera _camera => _player.PlayerCamera; // Get camera from Player
    private World _world = null!;
    private Player _player = null!;
    private CollisionManager _collisionManager = null!;

    // Input state variables
    private bool _firstMove = true;
    private Vector2 _lastMousePos;

    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        this.CenterWindow(new Vector2i(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y));
    }

    // Helper to check for errors (Keep or move to a static utility class)
    private void CheckGLError(string stage)
    {
        var error = GL.GetError();
        // Specify the correct ErrorCode namespace
        if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
        {
            Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        Console.WriteLine("OpenGL Version: " + GL.GetString(StringName.Version));

        // --- GLFW Framebuffer Size Test ---
        unsafe
        {
            GLFW.GetFramebufferSize(this.WindowPtr, out int fbWidth, out int fbHeight);
            Console.WriteLine($"==> OnLoad Window Logical Size: X={Size.X}, Y={Size.Y}");
            Console.WriteLine($"==> OnLoad Framebuffer Size: Width={fbWidth}, Height={fbHeight}");
            GL.Viewport(0, 0, fbWidth, fbHeight);
        }
        CheckGLError("After Viewport Load");
        // --- End Test ---

        GL.ClearColor(0.5f, 0.75f, 0.9f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        CheckGLError("After DepthTest Enable");

        // Setup Shader
        _shader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");
        CheckGLError("After Shader Load");

        // Calculate initial aspect ratio
        float initialAspectRatio = 16.0f / 9.0f; // Default
        if (Size.Y > 0 && Size.X > 0)
        {
            initialAspectRatio = Size.X / (float)Size.Y;
            Console.WriteLine($"==> Calculated Initial Aspect Ratio: {initialAspectRatio}");
        }
        else
        {
            Console.WriteLine($"==> Warning: Initial window size invalid ({Size.X}x{Size.Y}), using default aspect ratio 16:9.");
        }

        // Instantiate Managers and World
        _collisionManager = new CollisionManager();
        _world = new World();
        _world.Initialize(); // Generate world data and buffers
        CheckGLError("After World Init");

        // Determine Player Start Position
        const int StartX = 32; // Example fixed start X (center of 64 world)
        const int StartZ = 32; // Example fixed start Z
        int startHeight = _world.GetHeight(StartX, StartZ);

        // Instantiate Player (needed for Size.Y)
        // Use a temporary aspect ratio, it will be corrected in OnResize if needed
        float tempAspectRatio = (Size.X > 0 && Size.Y > 0) ? Size.X / (float)Size.Y : 16.0f / 9.0f;
        // Instantiate player at a temporary position first to access Size
        _player = new Player(Vector3.Zero, tempAspectRatio, _collisionManager, _world);

        // Calculate correct start Y: feet slightly above the ground block's top surface
        float feetStartY = startHeight + 0.5f; // Top surface of the block at startHeight
        float headStartY = feetStartY + _player.Size.Y + 0.01f; // Add player height and a small epsilon

        Vector3 startPosition = new Vector3(StartX, headStartY, StartZ);
        Console.WriteLine($"Calculated ground height at ({StartX},{StartZ}) to {startHeight}. Player Start: {startPosition}");

        // Set the final player position and update the camera
        _player.Position = startPosition;
        _player.PlayerCamera.Position = startPosition; // Ensure camera starts at the correct head position

        CheckGLError("After Player Init");

        // Capture mouse
        CursorState = CursorState.Grabbed;

        CheckGLError("OnLoad Complete");
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        CheckGLError("RenderFrame Clear");

        // Delegate drawing to the World class
        _world.Draw(_shader, _camera); // Pass shader and camera
        CheckGLError("RenderFrame World Draw");

        SwapBuffers();
        CheckGLError("RenderFrame SwapBuffers");
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        float dt = (float)e.Time;

        if (!IsFocused) return;

        var input = KeyboardState;
        var mouse = MouseState;

        // --- Handle Global Input (Escape/Tab) ---
        if (input.IsKeyDown(Keys.Escape)) Close();
        if (input.IsKeyPressed(Keys.Tab))
        {
            CursorState = (CursorState == CursorState.Grabbed) ? CursorState.Normal : CursorState.Grabbed;
            if (CursorState == CursorState.Grabbed)
            {
                _firstMove = true; // Reset first move when grabbing cursor
            }
        }

        // --- Update Player --- (Handles movement, physics, camera look)
        if (CursorState == CursorState.Grabbed)
        {
            _player.Update(dt, input, mouse, _firstMove, _lastMousePos);
            // Update mouse state for next frame AFTER player update
            _lastMousePos = new Vector2(mouse.X, mouse.Y);
            if (_firstMove) _firstMove = false;
        }
        else
        {
             // If cursor is not grabbed, ensure _firstMove is reset for when it is grabbed again
             _firstMove = true;
             // Optionally, you might want to update player physics (gravity) even if not moving
             // _player.ApplyGravity(dt); // Example method if separated
        }

        // Camera position is updated within Player.Update
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Console.WriteLine($"==> OnResize Event: New Size X={Size.X}, Y={Size.Y}");
        GL.Viewport(0, 0, Size.X, Size.Y);
        CheckGLError("After Viewport Resize");

        // Update Player's Camera Aspect Ratio
        if (_player != null)
        {
            float newAspectRatio = 16.0f / 9.0f; // Default
             if (Size.Y > 0)
             {
                 newAspectRatio = Size.X / (float)Size.Y;
                 Console.WriteLine($"==> AspectRatio updated to: {newAspectRatio}");
             }
             else
             {
                 Console.WriteLine("==> Warning: Window height is zero, using default aspect ratio.");
             }
            _player.UpdateAspectRatio(newAspectRatio);
        }
    }

    protected override void OnUnload()
    {
        // Dispose managed resources
        _world?.Dispose(); // Dispose world (handles VBO/VAO)
        _shader?.Dispose();

        base.OnUnload();
        CheckGLError("OnUnload");
    }
}