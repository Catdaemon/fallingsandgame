// Water effect shader for MonoGame

// For macOS/OpenGL compatibility
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0

// Matrix definitions
matrix World;
matrix View;
matrix Projection;

// Time parameter for water animation
float TotalTime;

// Distortion parameters
float DistortionAmount = 0.005;
float WaveSpeed = 2.0; 
float WaveFrequency = 10.0;

// Texture and sampler
texture ScreenTexture;
sampler2D ScreenSampler = sampler_state
{
    Texture = <ScreenTexture>;
};

// Structures
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

// Vertex shader
VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    output.Position = mul(mul(mul(input.Position, World), View), Projection);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    
    return output;
}

// Pixel shader
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    // Calculate distortion based on time and position
    float2 distortion;
    distortion.x = sin(input.TexCoord.y * WaveFrequency + TotalTime * WaveSpeed) * DistortionAmount;
    distortion.y = cos(input.TexCoord.x * WaveFrequency + TotalTime * WaveSpeed) * DistortionAmount;
    
    // Sample with the distorted coordinates
    float4 color = tex2D(ScreenSampler, input.TexCoord + distortion);
    
    // Blend with water color (blue tint)
    float4 waterColor = float4(0.2, 0.4, 0.8, 0.7);
    return lerp(color, waterColor, 0.3) * input.Color;
}

// Technique
technique WaterDistortion
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}