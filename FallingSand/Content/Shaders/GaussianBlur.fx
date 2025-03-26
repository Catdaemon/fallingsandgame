#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Blur parameters
float BlurAmount;
float2 Resolution;

// Texture and sampler
sampler2D TextureSampler : register(s0);

// Vertex shader input/output structures
struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
	float4 Color    : COLOR0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float4 Color    : COLOR0;
};

// Vertex shader (same for both passes)
VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;
	
	output.Position = input.Position;
	output.TexCoord = input.TexCoord;
	output.Color = input.Color;
	
	return output;
}

// Horizontal blur pixel shader
float4 HorizontalBlurPS(VertexShaderOutput input) : COLOR0
{
	float texelSize = 1.0 / Resolution.x;
	float4 color = float4(0, 0, 0, 0);
	float totalWeight = 0;
	
	// Simple 9-tap blur with weights that approximate a gaussian
	const int samples = 9;
	float weights[samples] = {0.05, 0.09, 0.12, 0.15, 0.18, 0.15, 0.12, 0.09, 0.05};
	int halfSamples = samples / 2;
	
	for (int i = -halfSamples; i <= halfSamples; i++)
	{
		float2 texCoord = input.TexCoord + float2(texelSize * i * BlurAmount, 0);
		float weight = weights[i + halfSamples];
		color += tex2D(TextureSampler, texCoord) * weight;
		totalWeight += weight;
	}
	
	return color / totalWeight;
}

// Vertical blur pixel shader
float4 VerticalBlurPS(VertexShaderOutput input) : COLOR0
{
	float texelSize = 1.0 / Resolution.y;
	float4 color = float4(0, 0, 0, 0);
	float totalWeight = 0;
	
	// Simple 9-tap blur with weights that approximate a gaussian
	const int samples = 9;
	float weights[samples] = {0.05, 0.09, 0.12, 0.15, 0.18, 0.15, 0.12, 0.09, 0.05};
	int halfSamples = samples / 2;
	
	for (int i = -halfSamples; i <= halfSamples; i++)
	{
		float2 texCoord = input.TexCoord + float2(0, texelSize * i * BlurAmount);
		float weight = weights[i + halfSamples];
		color += tex2D(TextureSampler, texCoord) * weight;
		totalWeight += weight;
	}
	
	return color / totalWeight;
}

technique HorizontalBlur
{
	pass Pass1
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL HorizontalBlurPS();
	}
}

technique VerticalBlur
{
	pass Pass1
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL VerticalBlurPS();
	}
}
