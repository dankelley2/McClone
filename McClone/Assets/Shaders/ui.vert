#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoord; // Added texture coordinates

uniform mat4 projection;
uniform mat4 model;

out vec2 TexCoord; // Pass tex coords to fragment shader

void main()
{
    gl_Position = projection * model * vec4(aPos.x, aPos.y, 0.0, 1.0);
    TexCoord = aTexCoord; // Pass through tex coords
}
