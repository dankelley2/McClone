#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;
layout (location = 2) in vec3 aNormal; // Add normal attribute

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec3 vertexColor; // Pass color to fragment shader
out vec3 Normal;      // Pass normal (in world space) to fragment shader
out vec3 FragPos;     // Pass fragment position (in world space) to fragment shader

void main()
{
    FragPos = vec3(model * vec4(aPosition, 1.0)); // Calculate world space position
    // Calculate world space normal (assuming model matrix doesn't have non-uniform scaling)
    // For correctness with non-uniform scaling, use: transpose(inverse(mat3(model))) * aNormal
    Normal = mat3(model) * aNormal; // Simpler version for uniform scaling/rotation/translation

    gl_Position = projection * view * vec4(FragPos, 1.0); // Use world space position
    vertexColor = aColor; // Pass the vertex color through
}