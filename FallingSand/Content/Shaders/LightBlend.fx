// Basic lighting pixel shader
texture SceneTexture : register(t1);
sampler2D SceneTextureSampler = sampler_state {
    Texture = <SceneTexture>;
};

texture LightMap : register(t0);
sampler2D LightMapSampler = sampler_state {
    Texture = <LightMap>;
};


float4 PixelShaderFunction(float2 uv : TEXCOORD0) : COLOR0
{
    // Sample the original scene color
    float4 sceneColor = tex2D(SceneTextureSampler, uv);
    
    // Sample the light map color
    float4 lightColor = tex2D(LightMapSampler, uv);
    
    // Multiply scene color by light color
    // This will darken/lighten based on light map
    return sceneColor * lightColor;
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}