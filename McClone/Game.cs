using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using SharpNoise.Modules;
using System;
using System.Collections.Generic;
using System.Linq; // Needed for .Any()

namespace VoxelGame;

public class Game : GameWindow
{
    private Shader _shader = null!;
    private Camera _camera = null!;

    private List<Vector3> _voxelPositions = new();
    private List<Vector3> _voxelColors = new();
    private int _voxelVao;
    private int _voxelVbo; // Renamed VBO for clarity
    private float[] _cubeVertices = null!;
    private int _cubeVertexCount = 0;

    private Perlin _noiseModule = new();
    private const int WorldSize = 64;
    private const float NoiseScale = 0.04f;
    private const int NoiseOctaves = 4;
    private const float TerrainAmplitude = 8f;
    private const int BaseHeight = 0;
    private Dictionary<(int x, int z), int> _terrainHeightMap = new();

    private Vector3 _playerPosition;
    private Vector3 _playerVelocity = Vector3.Zero;
    private const float Gravity = 25.0f;
    private const float JumpForce = 9.0f;
    private const float PlayerSpeed = 5.0f;
    private bool _canJump = false;
    private bool _isOnGround = false;

    private bool _firstMove = true;
    private Vector2 _lastMousePos;

    // Add constants for player dimensions here
    private const float PlayerWidth = 0.6f;
    private const float PlayerDepth = 0.6f; // Can differ from width
    private const float PlayerHeight = 1.8f; // Keep existing one

    // Add an instance of the CollisionManager
    private CollisionManager _collisionManager = null!;

    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        this.CenterWindow(new Vector2i(nativeWindowSettings.ClientSize.X, nativeWindowSettings.ClientSize.Y));
    }

    // Helper to check for errors
    private void CheckGLError(string stage)
    {
        var error = GL.GetError();
        if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
        {
            Console.WriteLine($"!!!!!!!! OpenGL Error [{stage}]: {error} !!!!!!!!");
            // Consider throwing or breaking here during debugging
            // System.Diagnostics.Debugger.Break();
        }
    }

    // In Game.cs
    protected override void OnLoad()
    {
        base.OnLoad();
        Console.WriteLine("OpenGL Version: " + GL.GetString(StringName.Version));

        // --- GLFW Framebuffer Size Test ---
        // Get logical window size and framebuffer size
        unsafe
        {
            GLFW.GetFramebufferSize(this.WindowPtr, out int fbWidth, out int fbHeight);

            Console.WriteLine($"==> OnLoad Window Logical Size: X={Size.X}, Y={Size.Y}");
            Console.WriteLine($"==> OnLoad Framebuffer Size: Width={fbWidth}, Height={fbHeight}");

            // Set viewport using the actual Framebuffer size
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

        // Setup Camera using LOGICAL size for aspect ratio calculation
        // Check for zero height before calculating aspect ratio
        float initialAspectRatio;
        if (Size.Y > 0 && Size.X > 0)
        {
            initialAspectRatio = Size.X / (float) Size.Y; // Use logical size here
            Console.WriteLine($"==> Calculated Initial Aspect Ratio: {initialAspectRatio}");
        }
        else
        {
            initialAspectRatio = 16.0f / 9.0f; // Default if size is invalid
            Console.WriteLine(
                $"==> Warning: Initial window size invalid ({Size.X}x{Size.Y}), using default aspect ratio 16:9.");
        }

        _camera = new Camera(Vector3.Zero, initialAspectRatio); // Temp position, correct aspect
        // _camera.AspectRatio = initialAspectRatio; // Camera constructor should set this

        // Setup Noise
        _noiseModule.Seed = new Random().Next();
        _noiseModule.OctaveCount = NoiseOctaves;

        // Generate Cube Vertex Data
        GenerateCubeVertices();
        CheckGLError("After Gen Cube Verts");

        // Generate World & Setup Buffers
        GenerateSimpleWorld();
        CheckGLError("After Gen World");
        SetupVoxelBuffers(); // Setup buffers *after* generating data
        CheckGLError("After Setup Buffers");

        // Instantiate the CollisionManager *after* other initializations if needed
        _collisionManager = new CollisionManager();
        CheckGLError("After CollisionManager Init"); // Optional error check

        // Set initial player height based on generated terrain
        _playerPosition = new Vector3(WorldSize / 2.0f, 15f, WorldSize / 2.0f); // Default start pos
        int startX = (int) MathF.Round(_playerPosition.X);
        int startZ = (int) MathF.Round(_playerPosition.Z);
        if (_terrainHeightMap.TryGetValue((startX, startZ), out int startHeight))
        {
            _playerPosition.Y = startHeight + PlayerHeight + 1.0f; // Start slightly above ground
            Console.WriteLine(
                $"Adjusted start height based on terrain at ({startX},{startZ}) to {startHeight}. Player Y: {_playerPosition.Y}");
        }
        else
        {
            Console.WriteLine($"Could not find terrain height at ({startX},{startZ}). Using default Y.");
        }

        _camera.Position = _playerPosition; // Sync camera AFTER calculating position
        Console.WriteLine($"Initial Player Position: {_playerPosition}");
        CheckGLError("After Initial Position Set");

        // Capture mouse
        CursorState = CursorState.Grabbed;

        CheckGLError("OnLoad Complete");
    }

    private void GenerateCubeVertices()
    {
        /* ... unchanged ... */
        _cubeVertices = new float[]
        {
            // Position          Color (using distinct face colors for debugging)
            // Front face (Red)
            -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            -0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,

            // Back face (Green)
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 0.0f,

            // Left face (Blue)
            -0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,

            // Right face (Yellow)
            0.5f, 0.5f, 0.5f, 1.0f, 1.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 1.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 1.0f, 0.0f,

            // Bottom face (Cyan)
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,
            0.5f, -0.5f, 0.5f, 0.0f, 1.0f, 1.0f,
            0.5f, -0.5f, 0.5f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f, 1.0f,

            // Top face (Magenta)
            -0.5f, 0.5f, -0.5f, 1.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 1.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, -0.5f, 1.0f, 0.0f, 1.0f
        };
        _cubeVertexCount = _cubeVertices.Length / 6;
    }

    private void GenerateSimpleWorld()
    {
        /* ... unchanged logic ... */
        Console.WriteLine("Generating world geometry...");
        _voxelPositions.Clear();
        _voxelColors.Clear();
        _terrainHeightMap.Clear();

        Vector3 grassColor = new(0.0f, 0.8f, 0.1f);
        Vector3 dirtColor = new(0.6f, 0.4f, 0.2f);
        Vector3 stoneColor = new(0.5f, 0.5f, 0.5f);

        for (int x = 0; x < WorldSize; x++)
        {
            for (int z = 0; z < WorldSize; z++)
            {
                double noiseValue = _noiseModule.GetValue(x * NoiseScale, 0, z * NoiseScale);
                int height = BaseHeight + (int) Math.Round((noiseValue + 1.0) / 2.0 * TerrainAmplitude);
                _terrainHeightMap[(x, z)] = height;

                for (int y = BaseHeight - 3; y <= height; y++)
                {
                    Vector3 blockPos = new(x, y, z);
                    _voxelPositions.Add(blockPos);
                    Vector3 color;
                    if (y == height) color = grassColor;
                    else if (y > height - 3) color = dirtColor;
                    else color = stoneColor;
                    _voxelColors.Add(color);
                }
            }
        }

        Console.WriteLine($"Generated {_voxelPositions.Count} voxel positions.");
    }

    private void SetupVoxelBuffers()
    {
        if (!_voxelPositions.Any())
        {
            Console.WriteLine("Skipping VBO setup - no voxel positions.");
            return;
        }

        List<float> allVertexData = new List<float>();
        for (int i = 0; i < _voxelPositions.Count; i++)
        {
            Vector3 pos = _voxelPositions[i];
            Vector3 color = _voxelColors[i]; // Use block-specific color
            for (int j = 0; j < _cubeVertices.Length; j += 6)
            {
                allVertexData.Add(_cubeVertices[j + 0] + pos.X);
                allVertexData.Add(_cubeVertices[j + 1] + pos.Y);
                allVertexData.Add(_cubeVertices[j + 2] + pos.Z);
                // Override cube color with block specific color:
                // allVertexData.Add(color.X);
                // allVertexData.Add(color.Y);
                // allVertexData.Add(color.Z);
                // OR Use cube face colors for debugging:
                allVertexData.Add(_cubeVertices[j + 3]);
                allVertexData.Add(_cubeVertices[j + 4]);
                allVertexData.Add(_cubeVertices[j + 5]);
            }
        }

        Console.WriteLine($"Total vertices generated for VBO: {allVertexData.Count / 6}");
        if (allVertexData.Count == 0)
        {
            Console.WriteLine("ERROR: Vertex data list is empty after processing voxels!");
            return;
        }


        // Create VAO
        _voxelVao = GL.GenVertexArray();
        GL.BindVertexArray(_voxelVao);
        CheckGLError("After BindVAO");

        // Create VBO for combined data
        _voxelVbo = GL.GenBuffer(); // Use renamed variable
        GL.BindBuffer(BufferTarget.ArrayBuffer, _voxelVbo);
        CheckGLError("After Bind VBO");
        GL.BufferData(BufferTarget.ArrayBuffer, allVertexData.Count * sizeof(float), allVertexData.ToArray(),
            BufferUsageHint.StaticDraw);
        CheckGLError("After BufferData");

        // Configure vertex attributes
        int posAttrib = _shader.GetAttribLocation("aPosition");
        if (posAttrib == -1) Console.WriteLine("Warning: aPosition attribute not found in shader!");
        GL.EnableVertexAttribArray(posAttrib);
        GL.VertexAttribPointer(posAttrib, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        CheckGLError("After AttribPointer Pos");

        int colorAttrib = _shader.GetAttribLocation("aColor");
        if (colorAttrib == -1) Console.WriteLine("Warning: aColor attribute not found in shader!");
        GL.EnableVertexAttribArray(colorAttrib);
        GL.VertexAttribPointer(colorAttrib, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float),
            3 * sizeof(float));
        CheckGLError("After AttribPointer Color");


        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);

        Console.WriteLine("Voxel buffers setup complete.");
    }


    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        // CheckGLError("RenderFrame Start"); // Keep error checks if helpful

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        // CheckGLError("After Clear");

        // Ensure we have something to draw and the VAO is valid
        if (_voxelPositions.Count == 0 || _voxelVao == 0)
        {
            SwapBuffers(); // Still swap buffers even if not drawing
            return;
        }

        _shader.Use();
        // CheckGLError("After Shader Use");

        // --- Use Camera for View and Projection ---
        Matrix4 view = _camera.GetViewMatrix(); // Use the camera's view matrix
        Matrix4 projection = _camera.GetProjectionMatrix(); // Use the camera's projection matrix
        // --- End Camera Use ---

        // Set shader uniforms
        _shader.SetMatrix4("view", view);
        // CheckGLError("After SetView");
        _shader.SetMatrix4("projection", projection); // Make sure this is not commented out
        // CheckGLError("After SetProjection");

        // Model matrix is identity since vertices are in world space
        Matrix4 model = Matrix4.Identity;
        _shader.SetMatrix4("model", model);
        // CheckGLError("After SetModel");

        // Draw the voxels
        int vertexCountToDraw = _voxelPositions.Count * _cubeVertexCount;
        if (vertexCountToDraw > 0)
        {
            GL.BindVertexArray(_voxelVao);
            // CheckGLError("After BindVAO for Draw");
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCountToDraw);
            // CheckGLError("After DrawArrays");
            GL.BindVertexArray(0); // Unbind VAO
        }

        SwapBuffers();
        // CheckGLError("After SwapBuffers");
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        float dt = (float) e.Time;

        if (!IsFocused) return;

        var input = KeyboardState;
        var mouse = MouseState;

        // --- Mouse Look and Escape/Tab (unchanged) ---
        if (input.IsKeyDown(Keys.Escape)) Close();
        if (input.IsKeyPressed(Keys.Tab))
        {
            CursorState = (CursorState == CursorState.Grabbed) ? CursorState.Normal : CursorState.Grabbed;
            _firstMove = true;
        }

        if (CursorState == CursorState.Grabbed)
        {
            if (_firstMove)
            {
                _lastMousePos = new Vector2(mouse.X, mouse.Y);
                _firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - _lastMousePos.X;
                var deltaY = mouse.Y - _lastMousePos.Y;
                _lastMousePos = new Vector2(mouse.X, mouse.Y);
                _camera.ProcessMouseMovement(deltaX, deltaY);
            }
        }

        // --- Player Movement Input (unchanged) ---
        Vector3 moveDir = Vector3.Zero;
        if (input.IsKeyDown(Keys.W)) moveDir += _camera.Front;
        if (input.IsKeyDown(Keys.S)) moveDir -= _camera.Front;
        if (input.IsKeyDown(Keys.A)) moveDir -= _camera.Right;
        if (input.IsKeyDown(Keys.D)) moveDir += _camera.Right;
        moveDir.Y = 0;
        if (moveDir.LengthSquared > 0) moveDir.Normalize();


        // --- Physics and Collision ---
        Vector3 currentPosition = _playerPosition;
        Vector3 currentVelocity = _playerVelocity;
        Vector3 playerSize = new Vector3(PlayerWidth, PlayerHeight, PlayerDepth); // Define player size AABB

        // Apply Gravity
        currentVelocity.Y -= Gravity * dt;

        // Calculate intended displacement
        Vector3 horizontalDisplacement = moveDir * PlayerSpeed * dt;
        Vector3 verticalDisplacement = new Vector3(0, currentVelocity.Y * dt, 0);

        // --- Collision Detection and Response (using CollisionManager) ---
        Vector3 nextPosition;
        Vector3 collisionNormal;

        // 1. Check X-axis movement
        nextPosition = currentPosition + new Vector3(horizontalDisplacement.X, 0, 0);
        // Pass required data to the manager's method
        if (!_collisionManager.CheckWorldCollision(nextPosition, playerSize, _voxelPositions, out collisionNormal))
        {
            currentPosition = nextPosition; // Move is valid
        }

        // 2. Check Z-axis movement
        nextPosition = currentPosition + new Vector3(0, 0, horizontalDisplacement.Z);
        if (!_collisionManager.CheckWorldCollision(nextPosition, playerSize, _voxelPositions, out collisionNormal))
        {
            currentPosition = nextPosition; // Move is valid
        }

        // 3. Check Y-axis movement
        nextPosition = currentPosition + verticalDisplacement;
        _isOnGround = false; // Assume not on ground unless collision proves otherwise
        _canJump = false; // Assume can't jump unless landed

        // Use the collision manager for the vertical check
        if (_collisionManager.CheckWorldCollision(nextPosition, playerSize, _voxelPositions, out collisionNormal))
        {
            // Collision detected vertically

            // Check if the primary collision direction is vertical
            if (MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.X) &&
                MathF.Abs(collisionNormal.Y) > MathF.Abs(collisionNormal.Z))
            {
                if (verticalDisplacement.Y < 0 &&
                    collisionNormal.Y > 0.5f) // Moving down, hit ground (normal points up)
                {
                    // --- MODIFICATION START ---
                    // Prevent downward movement instead of snapping.
                    // currentPosition.Y remains as it was before the vertical step.
                    currentVelocity.Y = 0; // Stop downward velocity
                    _isOnGround = true;
                    _canJump = true;
                    // Do NOT update currentPosition.Y to nextPosition.Y
                    // --- MODIFICATION END ---
                }
                else if (verticalDisplacement.Y > 0 &&
                         collisionNormal.Y < -0.5f) // Moving up, hit ceiling (normal points down)
                {
                    // --- MODIFICATION START ---
                    // Prevent upward movement instead of snapping.
                    // currentPosition.Y remains as it was before the vertical step.
                    currentVelocity.Y = 0; // Stop upward velocity
                    // Do NOT update currentPosition.Y to nextPosition.Y
                    // --- MODIFICATION END ---
                }
                // else: Vertical collision but conditions not met (e.g., moving up but hit ground? - shouldn't happen with separation)
            }
            // else: Collision occurred during vertical step, but it was primarily horizontal
            //       (e.g., falling onto a corner/edge). For simplicity, we currently do nothing
            //       special here, which might feel slightly sticky on edges.
            //       Keep the currentPosition as it was before the Y check in this case too.

        }
        else
        {
            // No vertical collision, apply the full vertical movement
            currentPosition = nextPosition;
            _isOnGround = false; // Definitely in the air
        }
        // --- End Collision Detection ---


        // Handle Jump Input
        if (input.IsKeyPressed(Keys.Space) && _isOnGround)
        {
            currentVelocity.Y = JumpForce;
            _isOnGround = false;
            _canJump = false;
        }

        // Final Update
        _playerPosition = currentPosition;
        _playerVelocity = currentVelocity;

        // Update Camera
        _camera.Position = _playerPosition;
    }


    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        // Log the size *before* setting viewport
        Console.WriteLine($"==> OnResize Event: New Size X={Size.X}, Y={Size.Y}"); // <<< ADD LOGGING
        GL.Viewport(0, 0, Size.X, Size.Y);
        CheckGLError("After Viewport Resize"); // Add error check here too

        if (_camera != null)
        {
            // Important: Check for division by zero if window is minimized
            if (Size.Y > 0)
            {
                _camera.AspectRatio = Size.X / (float) Size.Y; // Ensure float division
                Console.WriteLine($"==> AspectRatio updated to: {_camera.AspectRatio}"); // <<< ADD LOGGING
            }
            else
            {
                Console.WriteLine("==> Warning: Window height is zero, skipping aspect ratio update.");
            }
            // If your Camera class requires a method call to update the projection matrix, call it here.
            // e.g. _camera.UpdateProjection();
        }
    }

    protected override void OnUnload()
    {
        /* ... use renamed VBO ... */
        // Dispose of resources
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(_voxelVbo); // Use correct variable name

        GL.BindVertexArray(0);
        GL.DeleteVertexArray(_voxelVao);

        _shader?.Dispose();

        base.OnUnload();
    }
}