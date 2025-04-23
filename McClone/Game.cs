using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System;
using System.IO; // Added for Path.Combine and AppContext
using System.Threading; // For Thread.Sleep

namespace VoxelGame;

public class Game : GameWindow
{
    private Shader _shader = null!;
    private Texture _blockTexture = null!; // Add Texture field
    private Camera _camera => _player.PlayerCamera; // Get camera from Player
    private World _world = null!;
    private Player _player = null!;
    private CollisionManager _collisionManager = null!;

    // Input state variables
    private bool _firstMove = true;
    private Vector2 _lastMousePos;

    // Flag to indicate initial chunk loading is complete
    private bool _initialLoadComplete = false;
    private Vector3 _initialPlayerPosition; // Store calculated start position

    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        this.CenterWindow(new Vector2i(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y));
    }

    private void CheckGLError(string stage)
    {
        var error = GL.GetError();
        if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
        {
            Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        Console.WriteLine("OpenGL Version: " + GL.GetString(StringName.Version));

        // --- GLFW Framebuffer Size ---
        unsafe
        {
            GLFW.GetFramebufferSize(this.WindowPtr, out int fbWidth, out int fbHeight);
            Console.WriteLine($"==> OnLoad Window Logical Size: X={Size.X}, Y={Size.Y}");
            Console.WriteLine($"==> OnLoad Framebuffer Size: Width={fbWidth}, Height={fbHeight}");
            GL.Viewport(0, 0, fbWidth, fbHeight);
        }
        CheckGLError("After Viewport Load");

        // --- GL Setup ---
        GL.ClearColor(0.5f, 0.75f, 0.9f, 1.0f); // Match fog color
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Multisample); // Enable Anti-Aliasing
        CheckGLError("After DepthTest/Multisample Enable");

        // --- Shader Setup ---
        string baseDirectory = AppContext.BaseDirectory;
        string vertexPath = Path.Combine(baseDirectory, "Shaders", "shader.vert");
        string fragmentPath = Path.Combine(baseDirectory, "Shaders", "shader.frag");
        _shader = new Shader(vertexPath, fragmentPath);
        CheckGLError("After Shader Load");

        // --- Texture Loading ---
        // Assuming you have a "grass_block.png" in a "Textures" folder next to your executable
        string texturePath = Path.Combine("Textures", "grass_block.png");
        _blockTexture = new Texture(texturePath);
        CheckGLError("After Texture Load");

        // --- World and Managers Initialization ---
        _collisionManager = new CollisionManager();
        _world = new World();
        _world.Initialize(); // Initializes world structure, cube data, starts background task
        CheckGLError("After World Init");

        // --- Calculate Player Start Position ---
        const int StartX = 0;
        const int StartZ = 0;
        int startHeight = _world.GetHeight(StartX, StartZ);

        // --- Instantiate Player (needed for Size.Y, use temp aspect ratio) ---
        float tempAspectRatio = (Size.X > 0 && Size.Y > 0) ? Size.X / (float)Size.Y : 16.0f / 9.0f;
        _player = new Player(Vector3.Zero, tempAspectRatio, _collisionManager, _world); // Temp position

        // Calculate correct start Y
        float feetStartY = startHeight + 0.5f; // Top surface of the block
        float headStartY = feetStartY + _player.Size.Y + 0.01f; // Add player height + epsilon
        _initialPlayerPosition = new Vector3(StartX, headStartY, StartZ);
        Console.WriteLine($"Calculated ground height at ({StartX},{StartZ}) to {startHeight}. Player Start: {_initialPlayerPosition}");

        // --- Queue Initial Chunks ---
        _world.QueueInitialChunks(_initialPlayerPosition);

        Console.WriteLine("OnLoad Complete. Initial chunk loading initiated.");
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        // Don't render anything until initial chunks are loaded
        if (!_initialLoadComplete)
        {
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f); // Dark loading screen
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            SwapBuffers();
            return; // Skip rendering world
        }

        GL.ClearColor(0.5f, 0.75f, 0.9f, 1.0f); // Match fog color
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        CheckGLError("RenderFrame Clear");

        // Pass the texture to the world draw call
        _world.Draw(_shader, _camera, _blockTexture);
        CheckGLError("RenderFrame World Draw");

        SwapBuffers();
        CheckGLError("RenderFrame SwapBuffers");
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        float dt = (float)e.Time;

        // --- Initial Chunk Loading Check ---
        if (!_initialLoadComplete)
        {
            _world.ProcessBuildQueue();

            if (_world.AreInitialChunksReady(_initialPlayerPosition))
            {
                Console.WriteLine("Initial chunk load complete! Starting game.");
                _initialLoadComplete = true;

                _player.Position = _initialPlayerPosition;
                _player.PlayerCamera.Position = _initialPlayerPosition;
                _player.UpdateAspectRatio(Size.X / (float)Size.Y);

                CursorState = CursorState.Grabbed;
                _firstMove = true;
                _lastMousePos = new Vector2(MouseState.X, MouseState.Y);

                CheckGLError("After Initial Load Completion");
            }
            else
            {
                return;
            }
        }

        if (!IsFocused) return;

        var input = KeyboardState;
        var mouse = MouseState;

        if (input.IsKeyDown(Keys.Escape)) Close();
        if (input.IsKeyPressed(Keys.Tab))
        {
            CursorState = (CursorState == CursorState.Grabbed) ? CursorState.Normal : CursorState.Grabbed;
            if (CursorState == CursorState.Grabbed)
            {
                _firstMove = true;
                _lastMousePos = new Vector2(mouse.X, mouse.Y);
            }
        }

        if (CursorState == CursorState.Grabbed)
        {
            _player.Update(dt, input, mouse, _firstMove, _lastMousePos);
            _lastMousePos = new Vector2(mouse.X, mouse.Y);
            if (_firstMove) _firstMove = false;
        }
        else
        {
            _firstMove = true;
        }

        _world.Update(_player.Position);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Console.WriteLine($"==> OnResize Event: New Size X={Size.X}, Y={Size.Y}");

        if (Size.X > 0 && Size.Y > 0)
        {
            GL.Viewport(0, 0, Size.X, Size.Y);
            CheckGLError("After Viewport Resize");

            if (_player != null)
            {
                float newAspectRatio = Size.X / (float)Size.Y;
                Console.WriteLine($"==> AspectRatio updated to: {newAspectRatio}");
                _player.UpdateAspectRatio(newAspectRatio);
            }
        }
        else
        {
            Console.WriteLine("==> Warning: Window size invalid during resize, viewport/aspect ratio not updated.");
        }
    }

    protected override void OnUnload()
    {
        Console.WriteLine("Starting OnUnload...");
        _world?.Dispose();
        _shader?.Dispose();
        _blockTexture?.Dispose(); // Dispose the texture

        base.OnUnload();
        CheckGLError("OnUnload");
        Console.WriteLine("OnUnload Complete.");
    }
}