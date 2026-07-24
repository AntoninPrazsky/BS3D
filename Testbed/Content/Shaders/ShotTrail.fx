//The launch smear of a freshly shot ball: the ball leaves the cannon very fast (~200 u/s, several ball
//diameters a frame), so a colour streak is drawn to sell it. The C# side anchors it at the muzzle and points
//it along the shot (the ball attaches within ~0.075 s, too brief to trail), passing the two ends as TrailHead
//and TrailTail; one billboard per live smear, turned to face the camera about that axis. The pixel shader is
//the streak itself - a soft comet, brightest and full width at the head, tapering and fading to the tail; the
//caller puts the bright head at the *leading* (far) end out in the open and the faint tail at the muzzle,
//since the muzzle end is hidden behind the barrel. Drawn additively and in linear radiance boosted over 1, so
//it glows and blooms through the glare pass like the emissive balls; depth-read (the cluster/platform/cannon
//in front hide it) but writes no depth. Its overall alpha fades over the shot's first fraction of a second
//(TrailAlpha, set on the CPU), so it is a launch burst that leaves the ball crisp. Testbed-only. SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 TrailHead;       //world position of the flying ball
float3 TrailTail;       //TrailLength behind it, along -velocity
float TrailHeadWidth;   //half-width at the head
float TrailTailWidth;   //half-width at the tail
float3 TrailColor;      //linear radiance, already boosted and hue-floored on the CPU
float TrailAlpha;       //overall launch fade, 1 at the shot down to 0

struct TrailVertexInput
{
	float3 Position : POSITION0; //ignored; the quad is placed from TrailHead/TrailTail
	float2 Data : TEXCOORD0;     //(side in {-1,1}, along in {0 tail, 1 head})
};

struct TrailVertexOutput
{
	float4 Position : SV_POSITION;
	float2 UV : TEXCOORD0;       //(side, along)
};

TrailVertexOutput TrailVS(TrailVertexInput input)
{
	TrailVertexOutput output;

	float along = input.Data.y;
	float3 pos = lerp(TrailTail, TrailHead, along);

	float3 axis = TrailHead - TrailTail;
	float axisLen = length(axis);
	float3 dir = axisLen > 1e-4 ? axis / axisLen : float3(0.0, 1.0, 0.0);

	//Billboard about the streak axis: the width runs perpendicular to both the axis and the view ray, so the
	//streak keeps its thickness from any angle and collapses edge-on to a line (as a real thin smear would).
	float3 toCam = CameraPosition - pos;
	float3 side = cross(dir, toCam);
	float sideLen = length(side);
	side = sideLen > 1e-4 ? side / sideLen : float3(1.0, 0.0, 0.0);

	float width = lerp(TrailTailWidth, TrailHeadWidth, along);
	pos += side * (input.Data.x * width);

	output.Position = mul(mul(float4(pos, 1.0), View), Projection);
	output.UV = float2(input.Data.x, along);

	return output;
}

float4 TrailPS(TrailVertexOutput input) : COLOR
{
	float across = 1.0 - abs(input.UV.x); //1 at the core, 0 at the edges
	float along = input.UV.y;             //0 at the tail, 1 at the head

	float profile = across * across;      //soft, round-ish falloff across the streak
	float lengthFade = along;             //faint at the tail, full at the head

	float a = profile * lengthFade * TrailAlpha;
	clip(a - 0.003);

	//Premultiplied, so additive blending fades the streak out towards its edges and tail
	return float4(TrailColor * a, a);
}

technique ShotTrail
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL TrailVS();
		PixelShader = compile PS_SHADERMODEL TrailPS();
	}
};
