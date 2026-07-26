//Falling snow over the mountain scene: a boxful of flakes around the camera, drifting down on the wind.
//The whole flake set lives in a static vertex buffer - one quad per flake, its base position a fixed
//random point in a unit cube - and the vertex shader animates it: the flake falls and drifts, wrapping
//within a box that follows the camera, so the snowfall is endless and always around you. The pixel shader
//is a soft round white speck. Drawn only in the mountain scene, alpha-blended into the HDR scene target
//(before glare and tonemap — which is why the flake colour has to mind GLARE_THRESHOLD), depth-read.
//
//The box follows the camera rather than being pinned to the world, which trades a little translational
//parallax for never popping as the camera crosses a box boundary - the right trade for a uniform veil of
//small flakes. Drawn in both executables through the shared SceneRenderer, Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

//The camera, and its right/up in world space for billboarding the flakes towards it
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

float SnowTime;

//The volume the flakes fill around the camera, how fast they fall, the wind that drifts them sideways,
//how far a flake sways as it falls, and the flake size in world units
float3 SnowBoxSize;
float SnowFallSpeed;
float2 SnowWind;
float SnowSway;
float FlakeSize;

float3 SnowColor;
float SnowOpacity;

struct SnowVertexInput
{
	float4 Position : POSITION0; //Base position of the flake, a fixed random point in the unit cube
	float3 Data : TEXCOORD0;     //(corner x, corner y in {-1,1}, per-flake random)
};

struct SnowVertexOutput
{
	float4 Position : SV_POSITION;
	float2 Corner : TEXCOORD0;
};

SnowVertexOutput SnowVS(SnowVertexInput input)
{
	SnowVertexOutput output;

	float3 b = input.Position.xyz;

	//Animate the base point within [0,1): it falls (y decreases) and drifts on the wind, wrapping with frac
	float fall = SnowTime * SnowFallSpeed / SnowBoxSize.y;
	float2 drift = SnowTime * SnowWind / SnowBoxSize.xz;

	float3 o;
	o.x = frac(b.x + drift.x);
	o.y = frac(b.y - fall);
	o.z = frac(b.z + drift.y);

	//Into a box centred on the camera, with a gentle per-flake sway
	float3 boxPosition = (o - 0.5) * SnowBoxSize;
	boxPosition.x += sin(SnowTime * 1.3 + input.Data.z * 40.0) * SnowSway;

	float3 center = CameraPosition + boxPosition;

	//Billboard the corner towards the camera, with a little per-flake size variation
	float size = FlakeSize * (0.6 + 0.8 * input.Data.z);
	float3 world = center + CameraRight * (input.Data.x * size) + CameraUp * (input.Data.y * size);

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.Corner = input.Data.xy;

	return output;
}

float4 SnowPS(SnowVertexOutput input) : COLOR
{
	//Soft round flake: opaque at the centre, feathered to nothing at the rim
	float mask = 1.0 - smoothstep(0.5, 1.0, length(input.Corner));

	clip(mask - 0.01);

	return float4(SnowColor, mask * SnowOpacity);
}

technique Snow
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SnowVS();
		PixelShader = compile PS_SHADERMODEL SnowPS();
	}
};
