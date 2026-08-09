//Falling snow over the mountain scene: a boxful of flakes around the camera, drifting down on the wind.
//The whole flake set lives in a static vertex buffer - one quad per flake, its base position a fixed
//random point in a unit cube - and the vertex shader animates it: the flake falls and drifts, wrapping
//within a box that follows the camera, so the snowfall is endless and always around you. Drawn only in the
//mountain scene, alpha-blended into the HDR scene target (before glare and tonemap — which is why the flake
//colour has to mind GLARE_THRESHOLD), depth-read.
//
//The box follows the camera rather than being pinned to the world, which trades a little translational
//parallax for never popping as the camera crosses a box boundary - the right trade for a uniform veil of
//small flakes. Drawn in both executables through the shared SceneRenderer, Shader Model 5.0, no OPENGL branch.
//
//A flake used to be a hard, near-opaque round disc, and a near one read as a falling snow*ball* rather than
//a flake (#85). Three things separate the two, all of them here: a flake is a hexagonal crystal, not a
//circle; it tumbles as it falls, glinting when it turns edge-on to the light; and one right at the lens is
//far out of focus, so it belongs in the picture as a soft veil rather than as a crisp white coin.

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

//How fast a flake tumbles (radians per second), how deep the six arms of its silhouette are cut, the
//distance from the lens a flake has to reach before it is drawn at full strength, and how much it glints
//as it turns
float SnowSpin;
float SnowLobing;
float SnowNearFade;
float SnowTwinkle;

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
	float3 Corner : TEXCOORD0; //(flake-local x, y — the tumble is in the geometry, not here — and lobe depth)
	float Alpha : TEXCOORD1;
};

SnowVertexOutput SnowVS(SnowVertexInput input)
{
	SnowVertexOutput output;

	float3 b = input.Position.xyz;
	float rand = input.Data.z;

	//Animate the base point within [0,1): it falls (y decreases) and drifts on the wind, wrapping with frac
	float fall = SnowTime * SnowFallSpeed / SnowBoxSize.y;
	float2 drift = SnowTime * SnowWind / SnowBoxSize.xz;

	float3 o;
	o.x = frac(b.x + drift.x);
	o.y = frac(b.y - fall);
	o.z = frac(b.z + drift.y);

	//Into a box centred on the camera, with a gentle per-flake sway
	float3 boxPosition = (o - 0.5) * SnowBoxSize;
	boxPosition.x += sin(SnowTime * 1.3 + rand * 40.0) * SnowSway;

	float3 center = CameraPosition + boxPosition;

	//Billboard the corner towards the camera, with a little per-flake size variation
	float size = FlakeSize * (0.6 + 0.8 * rand);

	//Tumble: the quad turns in its own plane while the silhouette stays in flake-local space, so the crystal
	//itself rotates. Each flake starts at its own angle, so no two are in step
	float spin = SnowTime * SnowSpin * (0.6 + 0.8 * rand) + rand * 40.0;
	float2 turn;
	sincos(spin, turn.x, turn.y);
	float2 corner = float2(input.Data.x * turn.y - input.Data.y * turn.x,
	                       input.Data.x * turn.x + input.Data.y * turn.y);

	float3 world = center + CameraRight * (corner.x * size) + CameraUp * (corner.y * size);

	//The box is centred on the camera, so this is the distance from the lens. A flake within a couple of
	//units of it is far out of focus in any real shot of falling snow, and drawing it at full strength is
	//what made it read as a solid ball; fade it out instead
	float fade = smoothstep(SnowNearFade * 0.25, SnowNearFade, length(boxPosition));

	//A flat crystal catches the light broadside and loses it edge-on, twice per turn
	float twinkle = 1.0 + SnowTwinkle * sin(spin * 2.0 + rand * 17.0);

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	//Decorrelated from the size random, so a flake's spikiness says nothing about how big it is. Never zero:
	//a flake with no arms at all is the round disc this whole shape exists to get away from
	output.Corner = float3(input.Data.xy, SnowLobing * (0.35 + 0.65 * frac(rand * 7.0)));
	output.Alpha = fade * twinkle;

	return output;
}

float4 SnowPS(SnowVertexOutput input) : COLOR
{
	float2 c = input.Corner.xy;
	float r = length(c);
	float2 direction = c / max(r, 1e-5);

	//Six-armed silhouette rather than a circle: cos(6θ) built up from the direction cosines by the
	//double-angle identity, which costs a handful of ALU and - unlike atan2 - has no branch cut to seam on
	float2 angle2 = float2(direction.x * direction.x - direction.y * direction.y, 2.0 * direction.x * direction.y);
	float2 angle4 = float2(angle2.x * angle2.x - angle2.y * angle2.y, 2.0 * angle2.x * angle2.y);
	float cos6 = angle4.x * angle2.x - angle4.y * angle2.y;

	//Normalised by (1 + lobe) so the arms reach the quad's inscribed circle whatever the lobe depth: a
	//spikier flake loses area between its arms instead of growing past the quad that carries it
	float lobe = input.Corner.z;
	float edge = (1.0 + lobe * cos6) / (1.0 + lobe);

	//Feathered from well inside the arms, so the crystal is a soft speck with no hard rim anywhere on it
	float mask = 1.0 - smoothstep(edge * 0.35, edge, r);

	float alpha = saturate(mask * input.Alpha * SnowOpacity);
	clip(alpha - 0.004);

	return float4(SnowColor, alpha);
}

technique Snow
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SnowVS();
		PixelShader = compile PS_SHADERMODEL SnowPS();
	}
};
