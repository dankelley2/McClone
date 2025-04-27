#version 330 core
out vec4 FragColor;

in vec2 TexCoord; // Receive tex coords from vertex shader

uniform vec4 objectColor;
uniform sampler2D textSampler; // Texture sampler for font glyphs
uniform bool useTexture; // Flag to determine rendering mode

void main()
{
    if (useTexture)
    {
        vec4 texColor = texture(textSampler, TexCoord);

        // Output pre-multiplied alpha
        FragColor = vec4(objectColor.rgb * texColor.a, texColor.a);
    }
    else
    {
        // Original solid color rendering
        FragColor = objectColor;
    }
}
