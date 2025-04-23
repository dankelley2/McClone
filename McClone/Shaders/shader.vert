#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor; // Keep for now, might remove later
layout (location = 2) in vec3 aNormal;
layout (location = 3) in vec2 aTexCoord; // Add texture coordinate attribute

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 Normal;      // Pass normal (in world space) to fragment shader
out vec3 FragPos;     // Pass fragment position (in world space) to fragment shader
out vec2 TexCoord;    // Pass texture coordinate to fragment shader

void main()
{
    FragPos = vec3(model * vec4(aPosition, 1.0));
    Normal = mat3(model) * aNormal;
    gl_Position = projection * view * vec4(FragPos, 1.0);
    TexCoord = aTexCoord; // Pass the texture coordinate through
}