#version 330 core
out vec4 FragColor;

in vec3 vertexColor; // Receive color from vertex shader
in vec3 Normal;      // Receive world space normal from vertex shader
in vec3 FragPos;     // Receive world space fragment position from vertex shader

uniform vec3 viewPos; // Camera position (needs to be passed from C#) - Assuming Camera class has Position

void main()
{
    // --- Lighting Parameters ---
    vec3 lightPos = vec3(32.0, 50.0, 32.0); // Example light position (above world center)
    vec3 lightColor = vec3(1.0, 1.0, 1.0);
    float ambientStrength = 0.2; // Ambient light intensity
    float diffuseStrength = 0.8; // Diffuse light intensity

    // --- Ambient Light ---
    vec3 ambient = ambientStrength * lightColor;

    // --- Diffuse Light ---
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0); // Lambertian factor
    vec3 diffuse = diffuseStrength * diff * lightColor;

    // --- Simple Edge/Fresnel Effect ---
    // Darken pixels viewed at a grazing angle
    vec3 viewDir = normalize(viewPos - FragPos);
    float fresnelFactor = pow(1.0 - max(dot(norm, viewDir), 0.0), 2.0); // Higher power = sharper edge effect
    float edgeDarkening = mix(1.0, 0.4, fresnelFactor); // Mix between full brightness and 40% brightness based on angle

    // --- Final Color ---
    vec3 lighting = (ambient + diffuse) * edgeDarkening; // Apply edge darkening to combined light
    vec3 result = lighting * vertexColor; // Modulate object color by light

    FragColor = vec4(result, 1.0);
}