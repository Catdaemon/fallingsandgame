#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Parameters
float totalTime;
float2 resolution;
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

// Simple blur function
float4 SampleBlurred(float2 uv, float blurAmount) 
{
    float4 color = float4(0, 0, 0, 0);
    float2 texelSize = 1.0 / resolution;
    
    // Sample a 3x3 grid and average
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            float2 offset = float2(x, y) * texelSize * blurAmount;
            color += tex2D(ScreenTextureSampler, uv + offset);
        }
    }
    
    return color / 9.0;
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
        // Multi-layered wave distortion for more realism
        float aspectRatio = resolution.x / resolution.y;
        
        // Layer 1: Slow, large waves
        float waveSpeed1 = 0.8;
        float waveAmplitude1 = 0.007; // Slightly increased for more visible distortion
        float waveFreq1 = 6.0;
        float xDistortion1 = sin(totalTime * waveSpeed1 + uv.y * waveFreq1 * resolution.y) * waveAmplitude1;
        float yDistortion1 = cos(totalTime * waveSpeed1 + uv.x * waveFreq1 * resolution.x) * waveAmplitude1 / aspectRatio;
        
        // Layer 2: Fast, smaller ripples
        float waveSpeed2 = 1.5;
        float waveAmplitude2 = 0.004;
        float waveFreq2 = 15.0;
        float xDistortion2 = sin(totalTime * waveSpeed2 + uv.y * waveFreq2 * resolution.y + 1.3) * waveAmplitude2;
        float yDistortion2 = cos(totalTime * waveSpeed2 + uv.x * waveFreq2 * resolution.x + 0.7) * waveAmplitude2 / aspectRatio;
        
        // Layer 3: Very subtle micro-details
        float waveSpeed3 = 3.0;
        float waveAmplitude3 = 0.002;
        float waveFreq3 = 30.0;
        float xDistortion3 = sin(totalTime * waveSpeed3 + uv.y * waveFreq3 * resolution.y + 2.5) * waveAmplitude3;
        float yDistortion3 = cos(totalTime * waveSpeed3 + uv.x * waveFreq3 * resolution.x + 1.8) * waveAmplitude3 / aspectRatio;
        
        // Combine all distortion layers
        float2 distortedUV = float2(
            uv.x + xDistortion1 + xDistortion2 + xDistortion3,
            uv.y + yDistortion1 + yDistortion2 + yDistortion3
        );
        
        // Safety check to prevent sampling outside texture boundaries
        distortedUV = clamp(distortedUV, 0.01, 0.99);
        
        // Sample with distortion and blur
        float4 distortedColor = tex2D(ScreenTextureSampler, distortedUV);
        float4 blurredColor = SampleBlurred(distortedUV, 3.0); // Add blur effect
        
        // Blend distorted and blurred colors
        float4 refractedColor = lerp(distortedColor, blurredColor, 0.4);
        
        // Detect top edges for foam
        float foamFactor = TopEdgeFactor(uv);
        
        // Create a more natural water color - less blue, more transparent
        float3 waterColor = float3(
            0.1, // Small amount of red for natural water look
            0.3 + 0.05 * sin(totalTime * 0.5 + uv.x * 10.0), // More green
            0.4 + 0.08 * cos(totalTime * 0.4 + uv.y * 8.0)  // Less blue
        );
        
        // Depth simulation - slightly darker in deeper areas
        float depth = 0.7 + 0.3 * sin(uv.y * 3.14); 
        waterColor *= depth;
        
        // Add foam at the top edges
        float3 foamColor = float3(0.9, 0.95, 1.0);
        float foamAmount = foamFactor * (0.6 + 0.4 * sin(totalTime * 3.0 + uv.x * 20.0));
        
        // Final water color is a mix of refracted background, water color, and foam
        float3 finalColor = lerp(
            lerp(refractedColor.rgb, waterColor, 0.3), // Less water tint (0.3 instead of 0.4)
            foamColor,
            foamAmount
        );
        
        // Make water more transparent for better background visibility
        float alpha = 0.7 - foamFactor * 0.1; // Less transparent at foam edges
        
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