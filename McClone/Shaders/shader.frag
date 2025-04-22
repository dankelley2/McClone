#version 330 core
out vec4 FragColor;

in vec3 vertexColor; // Receive color from vertex shader

void main()
{
    // Basic lighting effect (multiply color by simulated light direction)
    // This is very simple, proper lighting needs normals.
    // vec3 lightDir = normalize(vec3(0.5, 1.0, 0.7));
    // float diff = max(dot(vec3(0.0, 0.0, 1.0), lightDir), 0.2); // Simulate facing direction dot light

    FragColor = vec4(vertexColor, 1.0); // Use the interpolated vertex color
}