#version 330 core
out vec4 FragColor;

in vec3 Normal;      // Receive world space normal from vertex shader
in vec3 FragPos;     // Receive world space fragment position from vertex shader
in vec2 TexCoord;    // Receive texture coordinate from vertex shader

uniform vec3 viewPos; // Camera position
uniform sampler2D textureSampler; // Texture sampler uniform

// Fog Uniforms
uniform vec3 fogColor;
uniform float fogDensity;
uniform float fogGradient;

// Highlight Uniforms
uniform int isBlockHighlighted; // Use int (0 or 1) for bool
uniform vec3 highlightedBlockPos; // Integer coords of the highlighted block (e.g., 5.0, 10.0, 3.0)

void main()
{
    // --- Lighting Parameters ---
    vec3 lightPos = vec3(32.0, 50.0, 32.0); // Example light position
    vec3 lightColor = vec3(1.0, 1.0, 1.0);
    float ambientStrength = 0.3; // Slightly higher ambient for textures
    float diffuseStrength = 0.7;

    // --- Ambient Light ---
    vec3 ambient = ambientStrength * lightColor;

    // --- Diffuse Light ---
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diffuseStrength * diff * lightColor;

    // --- Simple Edge/Fresnel Effect ---
    vec3 viewDir = normalize(viewPos - FragPos);
    float fresnelFactor = pow(1.0 - max(dot(norm, viewDir), 0.0), 2.0);
    float edgeDarkening = mix(1.0, 0.4, fresnelFactor);

    // --- Texture Sampling ---
    vec4 texColor = texture(textureSampler, TexCoord);
    // Discard transparent fragments (optional, useful for non-cube textures)
    // if(texColor.a < 0.1) discard;

    // --- Base Color Calculation ---
    vec3 lighting = (ambient + diffuse) * edgeDarkening;
    vec3 baseColor = lighting * texColor.rgb; // Modulate texture color by light

    // --- Highlight Calculation ---
    if (isBlockHighlighted == 1)
    {
        // Check if the fragment's block coordinate matches the highlighted block coordinate
        // floor(FragPos) gives the integer coordinate of the block this fragment belongs to.
        // This works because FragPos for a block at (5,10,3) will be between (4.5, 9.5, 2.5) and (5.5, 10.5, 3.5)
        vec3 fragBlockCoord = floor(FragPos);
        bool isFragInHighlightBlock = fragBlockCoord == highlightedBlockPos;

        if (isFragInHighlightBlock)
        {
            // Apply highlight effect - e.g., add white tint
            //baseColor = mix(baseColor, vec3(1.0, 1.0, 1.0), 0.25); // Mix 25% white

            // --- Alternative: Edge Highlighting (using distance from center) ---
            vec3 blockCenter = highlightedBlockPos + vec3(0.5);
            vec3 distToCenter = abs(FragPos - blockCenter);
            float maxDist = max(distToCenter.x, max(distToCenter.y, distToCenter.z));
            float edgeFactor = smoothstep(0.45, 0.5, maxDist); // Highlight near the 0.5 boundary
            baseColor = mix(baseColor, vec3(1.0, 1.0, 1.0), edgeFactor * 0.5); // Mix white at edges
        }
    }

    // --- Fog Calculation ---
    float dist = length(viewPos - FragPos);
    float fogFactor = exp(-pow(dist * fogDensity, fogGradient));
    fogFactor = clamp(fogFactor, 0.0, 1.0);

    // --- Final Color (Mix base color with fog color) ---
    vec3 finalColor = mix(fogColor, baseColor, fogFactor);

    FragColor = vec4(finalColor, 1.0); // Use texture alpha if needed: texColor.a
}