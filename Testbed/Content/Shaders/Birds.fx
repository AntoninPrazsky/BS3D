//Birds circling over the dunes: a small flock of camera-facing billboards, each drawn as a dark
//flapping-wing silhouette against the sky. There are no living things in this project's world otherwise,
//so this is the one moving creature - and like everything else here it is procedural, no sprite sheet:
//the wing shape and its flap are a couple of lines of math in the pixel shader, and the C# side circles
//the quads on slow orbits over the desert. Drawn only in the desert scene.
//
//Testbed-only, Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

//The silhouette color, in linear radiance. Nearly black: a distant bird against a bright sky is a
//cut-out, not a shaded model.
float3 BirdColor;

//Resting wing dihedral (the shallow V a gliding raptor holds) and how far the wings beat around it
static const float WingDihedral = 0.16;
static const float FlapAmplitude = 0.42;

//Half-thickness of a wing at the shoulder, and the rounder body blob at the centre
static const float WingThickness = 0.11;
static const float BodyThickness = 0.15;
static const float BodyWidth = 0.14;

struct BirdVertexInput
{
	float4 Position : POSITION0;
	float3 Data : TEXCOORD0; //(u across the wingspan, v vertical, flap phase) - all in [-1,1] but phase
};

struct BirdVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Data : TEXCOORD0;
};

BirdVertexOutput BirdVS(BirdVertexInput input)
{
	BirdVertexOutput output;

	output.Position = mul(mul(input.Position, View), Projection);
	output.Data = input.Data;

	return output;
}

float4 BirdPS(BirdVertexOutput input) : COLOR
{
	float u = input.Data.x; //-1..1 along the wingspan
	float v = input.Data.y; //-1..1 vertical
	float phase = input.Data.z;

	float au = abs(u);

	//The wing's vertical line at this point along the span: a resting dihedral plus the beat, with the
	//tips travelling furthest (they scale with |u|). sin drives the flap; the whole flock never beats in
	//time because each bird carries its own phase.
	float wing = au * (WingDihedral + FlapAmplitude * sin(phase));

	//Thin wings tapering to the tips, with a rounder body swelling at the centre
	float thickness = WingThickness * (1.0 - 0.55 * au) + BodyThickness * exp(-(u * u) / (BodyWidth * BodyWidth));

	float d = abs(v - wing);
	float aa = fwidth(v) * 1.5 + 1e-4; //One-pixel-soft edge so distant birds do not crawl

	float mask = 1.0 - smoothstep(thickness - aa, thickness + aa, d);
	mask *= 1.0 - smoothstep(0.92, 1.0, au); //fade the very wing tips out inside the quad

	clip(mask - 0.01);

	return float4(BirdColor, mask);
}

technique Birds
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL BirdVS();
		PixelShader = compile PS_SHADERMODEL BirdPS();
	}
};
