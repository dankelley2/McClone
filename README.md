# McClone

A simple Minecraft-inspired voxel game clone built with C# and OpenTK.

## Features

- **Procedural Terrain Generation**: Uses Perlin noise (SharpNoise) for infinite, varied terrain.
- **Chunk System**: World is divided into 16x128x16 chunks for efficient rendering and memory use.
- **Block Rendering**: Only exposed faces of blocks are rendered for performance.
- **Textured Blocks**: Uses OpenGL shaders and texture atlases for block appearance (e.g., grass block).
- **Player Movement**: First-person controls with walking, jumping, and collision detection.
- **Block Interaction**: Break blocks with left mouse click (raycast targeting, cooldown included).
- **Camera**: Mouse look, adjustable FOV, and smooth movement.
- **Fog and Lighting**: Simple lighting and distance fog for atmosphere.
- **Cross-platform**: Runs on Windows, macOS, and Linux (OpenTK backend).

## Controls

- **WASD**: Move
- **Space**: Jump
- **Mouse**: Look around
- **Left Click**: Break block
- **Tab**: Toggle mouse grab
- **Esc**: Exit game
- **Shift**: Sprint

## Project Structure

- `Core/` — Game entry point and main loop
- `Player/` — Player movement, camera, and controls
- `Rendering/` — OpenGL rendering, shaders, textures
- `World/` — Chunk management, terrain generation, collision
- `Shaders/` — GLSL vertex and fragment shaders
- `Textures/` — Block textures (e.g., grass)

## Requirements

- .NET 8.0 SDK
- OpenTK 4.9+
- SharpNoise
- StbImageSharp

## Building & Running

1. Install .NET 8.0 SDK
2. Restore NuGet packages: `dotnet restore`
3. Build: `dotnet build`
4. Run: `dotnet run --project McClone/McClone.csproj`

## Assets

- Place block textures in `McClone/Textures/`
- Place shaders in `McClone/Shaders/`

## Notes

- Only basic block breaking is implemented (no block placement yet).
- Only one block type (grass/dirt) is currently supported.
- No mobs, crafting, or inventory (yet).
- World is generated on the fly as you move.

---

This project is for learning and experimentation. Contributions and suggestions are welcome!
