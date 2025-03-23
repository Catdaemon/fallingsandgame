#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Parameters
float totalTime;
texture ScreenTexture;

sampler2D ScreenTextureSampler = sampler_state
{
    Texture = <ScreenTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

// Helper function to detect if a pixel is water
bool IsWater(float4 color)
{
    // Water color detection with a bit of tolerance
    return (color.r < 0.05 && color.g < 0.05 && color.b > 0.95);
}

// Helper function to detect top edges of water
float TopEdgeFactor(float2 uv)
{
    float edgeThreshold = 0.01;
    float2 upOffset = float2(0, -edgeThreshold); // Check pixel above
    
    float4 center = tex2D(ScreenTextureSampler, uv);
    bool centerIsWater = IsWater(center);
    
    // Only continue if this is water
    if (!centerIsWater) return 0;
    
    // Check if the pixel above is not water (meaning this is a top edge)
    float4 above = tex2D(ScreenTextureSampler, uv + upOffset);
    if (!IsWater(above)) {
        // Calculate how close to the very top edge we are (for gradient foam effect)
        float edge = 1.0;
        
        // Check pixels slightly further up to create a gradient
        for (int i = 2; i <= 4; i++) {
            float2 gradientOffset = float2(0, -edgeThreshold * i);
            float4 gradientPixel = tex2D(ScreenTextureSampler, uv + gradientOffset);
            if (!IsWater(gradientPixel)) {
                edge += 0.25;
            }
        }
        
        return edge;
    }
    
    // Also check diagonal top pixels for a more complete foam effect
    float diagonalFactor = 0;
    float2 topLeftOffset = float2(-edgeThreshold, -edgeThreshold);
    float2 topRightOffset = float2(edgeThreshold, -edgeThreshold);
    
    float4 topLeft = tex2D(ScreenTextureSampler, uv + topLeftOffset);
    float4 topRight = tex2D(ScreenTextureSampler, uv + topRightOffset);
    
    if (!IsWater(topLeft)) diagonalFactor += 0.3;
    if (!IsWater(topRight)) diagonalFactor += 0.3;
    
    return diagonalFactor;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    float4 originalColor = tex2D(ScreenTextureSampler, uv);
    
    // Only apply effects to water pixels
    if (IsWater(originalColor))
    {
        // Detect top edges for foam
        float foamFactor = TopEdgeFactor(uv);
        
        // Create a simple water color
        float3 waterColor = float3(0.1, 0.3, 0.4);
        
        // Add foam at the top edges
        float3 foamColor = float3(0.9, 0.95, 1.0);
        float foamAmount = foamFactor * (0.6 + 0.4 * sin(totalTime * 3.0 + uv.x * 20.0));
        
        // Final water color is a mix of water color and foam
        float3 finalColor = lerp(waterColor, foamColor, foamAmount);
        
        // Make water slightly transparent
        float alpha = 0.7 - foamFactor * 0.1;
        
        return float4(finalColor, alpha);
    }
    
    return originalColor;
}

technique WaterEffect
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}