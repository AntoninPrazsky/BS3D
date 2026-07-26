//Draws the sky dome with procedural clouds over its baked gradient.
//
//The dome models carry the sky as vertex colors and were drawn through BasicEffect until now; this
//keeps that gradient exactly as it was and composites a cloud layer on top of it. The gradient arrives
//already linear - SkyDome rewrites the vertex buffers once at load - so nothing here decodes anything,
//and every cloud color below is authored as linear radiance rather than as an sRGB paint color.
//
//Built by the Testbed and the Game; the map editor builds InstancedModel.fx out of this directory but
//not this file, so it gets no drawn clouds.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

float4x4 World;
float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, normalized
float3 SunDirection;

//Lit tops and shadowed undersides, in linear radiance. The sunlit color runs above 1 on purpose: a
//cloud edge with the sun behind it is genuinely brighter than white paper, and letting it say so is
//what puts it through the same glare and the same highlight roll-off as everything else in the frame.
float3 CloudSunColor;
float3 CloudShadowColor;

//How opaque the densest cloud gets, and the elevation over which cloud fades out into horizon haze
float CloudOpacity;
float CloudHorizonFade;

//How far along the sun direction the shading looks to decide whether this bit of cloud is lit through
float CloudSunStep;

//How much light a piece of cloud swallows on the way through its own body, and how much the cloud
//standing between it and the sun swallows first
float CloudSelfAbsorption;
float CloudSunAbsorption;

//Forward scattering towards the sun: the silver lining, and how tightly it hugs the sun
float CloudSilverStrength;
float CloudSilverPower;

struct SkyVertexInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
};

struct SkyVertexOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float3 WorldPosition : TEXCOORD0;
};

SkyVertexOutput SkyVS(SkyVertexInput input)
{
	SkyVertexOutput output;

	float4 world = mul(input.Position, World);

	output.WorldPosition = world.xyz;
	output.Position = mul(mul(world, View), Projection);
	output.Color = input.Color;

	return output;
}

float4 SkyPS(SkyVertexOutput input) : COLOR
{
	float3 sky = input.Color.rgb;

	//The dome is translated to the camera every frame, so this is the view ray and nothing else
	float3 direction = normalize(input.WorldPosition - CameraPosition);

	//Where this ray crosses the cloud plane. Guarded rather than branched, and the guard is generous:
	//by the time the ray is this shallow the horizon fade below has taken the cloud out anyway.
	float climb = max(direction.y, 0.02);
	float distanceToPlane = (CloudPlaneY - CameraPosition.y) / climb;

	float2 hit = CameraPosition.xz + direction.xz * distanceToPlane;

	//This pixel's footprint on the cloud plane, in world units - the same measure the ball relief and
	//the city windows band-limit against. It grows without bound towards the horizon, which is what
	//silently retires the fine octaves exactly where they would otherwise alias into a shimmer.
	float footprint = length(fwidth(hit));

	//Thickness for the shading, coverage for the compositing. They are the same number until the cloud
	//goes solid, and everything interesting about a cloud's interior happens after that.
	float thickness = CloudThickness(hit, footprint);
	float density = saturate(thickness);

	//What the sun had to come through to reach this piece of cloud: the cloud's own body, plus whatever
	//stands between here and the sun a step along the plane. Beer-Lambert on the sum - a crude stand-in
	//for a scattering march, and one tap rather than a march is all a cloud this stylised can justify.
	//
	//The body term is what gives the layer a form at all. This is a flat field seen from underneath, so
	//the thick parts are undersides with the light used up before it got down here and the thin parts
	//are where it comes through: dark cores, bright edges. Shading off the neighbouring coverage alone -
	//which is what this did first - leaves every cloud the same flat white, because out in the open sky
	//there is nothing next to them to cast anything.
	float2 towardsSun = SunDirection.xz * CloudSunStep;
	float swallowed = thickness * CloudSelfAbsorption + CloudCover(hit + towardsSun) * CloudSunAbsorption;

	float sunlight = exp(-swallowed);
	float3 lit = lerp(CloudShadowColor, CloudSunColor, sunlight);

	//Forward scattering. Looking towards the sun through a thin edge is the whole reason a cloud ever
	//looks like anything other than grey wool, so it gets its own term rather than being left to the
	//tonemapper to invent. Weighted by the light that got here, so it lands on the edges and not on the
	//cores it would otherwise wash straight back out.
	float alignment = saturate(dot(direction, SunDirection));
	lit += CloudSunColor * pow(alignment, CloudSilverPower) * CloudSilverStrength * sunlight;

	//Cloud never reaches the horizon: the ray runs out along the plane long before it gets there, and
	//a sky that stays cloudy right down to the skyline reads as a lid rather than as weather.
	float coverage = saturate(density * CloudOpacity) * smoothstep(0.0, CloudHorizonFade, direction.y);

	return float4(lerp(sky, lit, coverage), 1.0);
}

technique Sky
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SkyVS();
		PixelShader = compile PS_SHADERMODEL SkyPS();
	}
};
