float4 PixelShaderFunction(float4 position : SV_Position, float4 color : COLOR0) : SV_Target0
{
    return color;
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_4_0_level_9_1 PixelShaderFunction();
    }
}
