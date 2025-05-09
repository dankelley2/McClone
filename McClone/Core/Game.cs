using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System;
using System.IO; // Added for Path.Combine and AppContext
using System.Threading; // For Thread.Sleep
using VoxelGame.Rendering;
using VoxelGame.World; // Ensure World namespace is included for ChunkEdits
using VoxelGame.Player;
using VoxelGame.Audio; // Add Audio namespace

namespace VoxelGame.Core
{

    public class Game : GameWindow
    {
        private Shader _shader = null!;
        private Texture _blockTexture = null!; // Add Texture field
        private Camera _camera => _player.PlayerCamera; // Get camera from Player
        private World.World _world = null!;
        private Player.Player _player = null!;
        private CollisionManager _collisionManager = null!;
        private AudioManager _audioManager = null!; // Add AudioManager field
        private int _keySoundBuffer = 0; // Store the buffer handle for the sound

        // Input state variables
        private bool _firstMove = true;
        private Vector2 _lastMousePos;

        // Flag to indicate initial chunk loading is complete
        private bool _initialLoadComplete = false;
        private Vector3 _initialPlayerPosition; // Store calculated start position
        private float? _initialPlayerPitch; // Store loaded pitch
        private float? _initialPlayerYaw; // Store loaded yaw
        private int _worldSeed; // Store the world seed

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

            // --- Initialize Audio Manager ---
            _audioManager = new AudioManager();
            if (!_audioManager.Initialize())
            {
                Console.WriteLine("!!! Audio Manager failed to initialize. Proceeding without audio. !!!");
                // Optionally handle this more gracefully, maybe close the game?
                // Set _audioManager to null to prevent further calls if initialization failed
                _audioManager = null!;
            }
            CheckGLError("After Audio Init"); // Check GL error just in case, though unlikely from audio

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
            string vertexPath = Path.Combine(baseDirectory, "Assets", "Shaders", "shader.vert");
            string fragmentPath = Path.Combine(baseDirectory, "Assets", "Shaders", "shader.frag");
            _shader = new Shader(vertexPath, fragmentPath);
            CheckGLError("After Shader Load");

            // --- Texture Loading ---
            // Assuming you have a "grass_block.png" in a "Textures" folder next to your executable
            string texturePath = Path.Combine(baseDirectory, "Assets", "Textures", "grass-block.png"); // Use baseDirectory
            _blockTexture = new Texture(texturePath);
            CheckGLError("After Texture Load");

            // --- Load Startup Sound ---
            if (_audioManager != null) // Only load if manager initialized successfully
            {
                string audioPath = Path.Combine(baseDirectory, "Assets", "Audio", "Key.wav"); // Use baseDirectory
                // Note: The file tree showed Key.ogg, but the prompt requested Key.wav.
                // The current AudioManager only supports WAV.
                _keySoundBuffer = _audioManager.LoadWav(audioPath);
                if (_keySoundBuffer != 0)
                {
                    _audioManager.PlayMusic(_keySoundBuffer, true); // Play looping
                }
                else
                {
                    Console.WriteLine($"Warning: Failed to load or play startup sound: {audioPath}");
                }
            }

            // --- Load World Edits & Determine Seed ---
            var (loadedSeed, loadedPosition, loadedPitch, loadedYaw) = ChunkEdits.LoadAllEdits(); // Load edits, position, and orientation
            _worldSeed = loadedSeed;
            _initialPlayerPitch = loadedPitch; // Store loaded orientation
            _initialPlayerYaw = loadedYaw;

            if (_worldSeed == 0) // If loading failed or no file, use a default/random seed
            {
                _worldSeed = new Random().Next(); // Or use a fixed default like 12345
                Console.WriteLine($"No saved seed found or load failed. Using new seed: {_worldSeed}");
            }
            else
            {
                Console.WriteLine($"Loaded world seed: {_worldSeed}");
            }

            // --- World and Managers Initialization ---
            _collisionManager = new CollisionManager();
            _world = new World.World(_worldSeed); // Pass the determined seed to the World
            _world.Initialize(); // Initializes world structure, cube data, starts background task
            CheckGLError("After World Init");

            // --- Instantiate Player (needed for Size.Y, use temp aspect ratio) ---
            float tempAspectRatio = (Size.X > 0 && Size.Y > 0) ? Size.X / (float)Size.Y : 16.0f / 9.0f;
            // Player constructor doesn't need pitch/yaw yet, set later
            _player = new Player.Player(Vector3.Zero, tempAspectRatio, _collisionManager, _world, _audioManager); // Temp position

            // --- Determine Player Start Position ---
            if (loadedPosition.HasValue)
            {
                _initialPlayerPosition = loadedPosition.Value;
                Console.WriteLine($"Using saved player position: {_initialPlayerPosition}");
            }
            else
            {
                // Calculate default start position if none was loaded
                const int StartX = 0;
                const int StartZ = 0;
                int startHeight = _world.GetHeight(StartX, StartZ) + 2; // Add buffer height
                float feetStartY = startHeight + 0.5f; // Top surface of the block below feet
                float headStartY = feetStartY + _player.Size.Y + 0.01f; // Add player height + epsilon
                _initialPlayerPosition = new Vector3(StartX, headStartY, StartZ);
                Console.WriteLine($"Calculated default ground height at ({StartX},{StartZ}) to {startHeight}. Player Start: {_initialPlayerPosition}");
            }


            // --- Queue Initial Chunks ---
            _world.QueueInitialChunks(_initialPlayerPosition); // Queue chunks around the final start position
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

            _world.Draw(_shader, _camera, _blockTexture);
            CheckGLError("RenderFrame World Draw");

            SwapBuffers();
            CheckGLError("RenderFrame SwapBuffers");
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            float dt = (float)e.Time;
            // Clamp delta time to prevent physics issues with large frame drops
            dt = Math.Min(dt, 1.0f / 30.0f); // Max dt = 1/30th of a second

            // --- Initial Chunk Loading Check ---
            if (!_initialLoadComplete)
            {
                _world.ProcessGLBufferUpdates();

                if (_world.AreInitialChunksReady(_initialPlayerPosition))
                {
                    Console.WriteLine("Initial chunk load complete! Starting game.");
                    _initialLoadComplete = true;

                    // Apply loaded edits AFTER initial chunks are generated but BEFORE game starts
                    // Note: Chunk.GenerateTerrain now applies edits internally if they exist

                    // Set the player's position *after* initial chunks are ready
                    _player.Position = _initialPlayerPosition;
                    // Set the player's orientation if loaded
                    if (_initialPlayerPitch.HasValue && _initialPlayerYaw.HasValue)
                    {
                        // Use the SetOrientation overload that takes player position and height
                        _player.PlayerCamera.SetOrientation(_initialPlayerPitch.Value, _initialPlayerYaw.Value, _initialPlayerPosition, _player.Size.Y);
                        Console.WriteLine($"Applied saved orientation: Pitch={_initialPlayerPitch.Value}, Yaw={_initialPlayerYaw.Value}");
                    }
                    else
                    {
                        // Ensure camera starts at the correct eye level based on the final position even if orientation wasn't loaded
                        _player.PlayerCamera.Position = _initialPlayerPosition + Vector3.UnitY * _player.Size.Y * 0.9f;
                    }

                    _player.UpdateAspectRatio(Size.X / (float)Size.Y); // Update aspect ratio now

                    CursorState = CursorState.Grabbed;
                    _firstMove = true;
                    _lastMousePos = new Vector2(MouseState.X, MouseState.Y);

                    CheckGLError("After Initial Load Completion");
                }
                else
                {
                    // Still loading...
                    return; // Skip the rest of the update
                }
            }

            // --- Game Logic (only runs after initial load) ---
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
            CheckGLError("UpdateFrame World Update");
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

            // --- Save World Edits, Player Position & Orientation ---
            if (_world != null && _player != null) // Ensure both world and player exist
            {
                ChunkEdits.SaveAllEdits(_world.WorldSeed, _player.Position, _player.PlayerCamera.Pitch, _player.PlayerCamera.Yaw); // Save state
            }
            else
            {
                Console.WriteLine("Warning: World or Player object was null during OnUnload, cannot save state.");
            }

            // --- World and Managers Cleanup ---
            _world?.Dispose();
            _shader?.Dispose();
            _blockTexture?.Dispose(); // Dispose the texture
            _audioManager?.Dispose(); // Dispose the audio manager

            base.OnUnload();
            CheckGLError("OnUnload");
            Console.WriteLine("OnUnload Complete.");
        }
    }
}