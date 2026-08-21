//Falling confetti: the campaign's closing celebration (#215). A boxful of paper rectangles around the
//camera, tumbling as they fall. The whole set lives in a static vertex buffer - one quad per piece, its base
//position a fixed random point in a unit cube - and the vertex shader animates it, wrapping within a box
//that follows the camera, so the fall is endless and always around you. Owned by Game/Effects/Confetti.cs,
//alpha-blended into the HDR scene target (before glare and tonemap - which is why the paper colours have to
//mind GLARE_THRESHOLD), depth-read.
//
//The box follows the camera rather than being pinned to the world, which trades a little translational
//parallax for never popping as the camera crosses a box boundary: Snow.fx's trade, taken for the same reason
//and against the same kind of subject.
//
//WHAT MAKES THIS CONFETTI AND NOT COLOURED SNOW is that the quad is NOT a billboard. Snow.fx turns its flake
//in its own plane and keeps it facing the lens, which is right for a speck with no orientation to read. A
//piece of paper has one: it flips end over end, so it is seen broadside, then foreshortened, then edge-on as
//a bright line, then broadside again. That flashing between a wide chip and a thin line is the single thing
//that says *paper* rather than *dot*, and it falls out of building the quad on a real world-space basis that
//rotates about the piece's own tumble axis. It costs one cross product and one sincos over a billboard.
//
//The second thing is the light. A flat chip is bright broadside to the light and dark edge-on, so the lambert
//term is taken on the piece's own normal and made TWO-SIDED with abs(): confetti is printed on both faces and
//is seen from both, and a one-sided term would blink half the pieces to black for half of every turn.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

float ConfettiTime;

//The volume the pieces fill around the camera, how fast they fall and how far they zigzag doing it. Paper
//has an enormous drag-to-mass ratio, which is why the fall is slow and the sideways wander is wide: those
//two together are what separates a drifting chip of paper from a dropped stone.
float3 ConfettiBoxSize;

//How far the box's centre sits ABOVE the lens. Confetti is a thing that comes down past you, so the camera
//belongs low in the volume rather than in the middle of it — and this camera in particular looks UP at the
//hanging cluster, so a box centred on the lens would spend most of its pieces under the bottom of the frame.
float ConfettiBoxLift;

float ConfettiFallSpeed;
float ConfettiFlutter;
float ConfettiFlutterRate;
float2 ConfettiDrift;

//Half-extents of one piece in world units (x across the tumble axis, y along it), how fast it tumbles, and
//how close to the lens a piece may come before it is faded out rather than drawn across half the screen.
float2 ConfettiSize;
float ConfettiSpin;
float ConfettiNearFade;

//The light the paper is lit by, and how much of the colour survives edge-on. Not the scene's rig: this is a
//celebration effect that runs over any of thirteen scenes and eighteen domes, and a piece that went black in
//the cavern would be a hole in the party. One direction, one ambient floor, everywhere.
float3 ConfettiLight;
float ConfettiAmbient;

//Global fade, ramped by the C# side so the fall starts and ends rather than being switched on and off.
float ConfettiIntensity;

struct ConfettiVertexInput
{
	float4 Base : TEXCOORD0;   //(base position in the unit cube, per-piece random)
	float4 Corner : TEXCOORD1; //(corner x, corner y in {-1,1}, size scale, spin rate)
	float4 Axis : TEXCOORD2;   //(tumble axis xyz — unit, biased horizontal — and the tumble's phase)
	float4 Tint : TEXCOORD3;   //(linear rgb of the paper, fall-speed scale)
};

struct ConfettiVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Color : TEXCOORD0;
	float2 Corner : TEXCOORD1;
	float Alpha : TEXCOORD2;
};

ConfettiVertexOutput ConfettiVS(ConfettiVertexInput input)
{
	ConfettiVertexOutput output;

	float3 b = input.Base.xyz;
	float rand = input.Base.w;

	//Animate the base point within [0,1): it falls (y decreases) and drifts, wrapping with frac. The
	//per-piece fall scale is what keeps the field from descending as one sheet.
	float fall = ConfettiTime * ConfettiFallSpeed * input.Tint.w / ConfettiBoxSize.y;
	float2 drift = ConfettiTime * ConfettiDrift / ConfettiBoxSize.xz;

	float3 o;
	o.x = frac(b.x + drift.x);
	o.y = frac(b.y - fall);
	o.z = frac(b.z + drift.y);

	//Into a box centred on the camera, then the ZIGZAG. A falling chip of paper does not drop straight: it
	//stalls, slips sideways, catches and slips the other way. Two sways at unrelated rates on the two
	//horizontal axes is enough to read as that without any state to carry.
	float3 boxPosition = (o - 0.5) * ConfettiBoxSize;

	float flutter = ConfettiTime * ConfettiFlutterRate * (0.7 + 0.6 * rand) + rand * 40.0;
	boxPosition.x += sin(flutter) * ConfettiFlutter;
	boxPosition.z += cos(flutter * 0.83 + rand * 11.0) * ConfettiFlutter;

	//Everything below measures from the LENS, so the lift goes in here once and the fade reads the same
	//vector the piece is actually placed on - taking the fade off the unlifted offset would fade a ring of
	//pieces that are nowhere near the camera and leave the ones overhead at full size.
	float3 offset = boxPosition + float3(0.0, ConfettiBoxLift, 0.0);
	float3 center = CameraPosition + offset;

	//THE TUMBLE. The piece's plane contains its own axis, and the second in-plane direction swings around it
	//- so the quad is broadside when that direction faces the lens and collapses to a line a quarter turn
	//later. A reference vector perpendicular to the axis is derived here rather than baked into the buffer:
	//it costs a cross and a normalize against sixteen bytes on every vertex, and picking the cardinal the
	//axis is LEAST aligned with is what keeps that cross well conditioned however the axis points.
	float3 axis = input.Axis.xyz;
	float3 pick = abs(axis.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
	float3 ref0 = normalize(cross(axis, pick));
	float3 ref1 = cross(axis, ref0);

	float angle = ConfettiTime * ConfettiSpin * input.Corner.w + input.Axis.w;
	float s, c;
	sincos(angle, s, c);

	float3 inPlane = ref0 * c + ref1 * s;   //the paper's second in-plane axis
	float3 normal = ref1 * c - ref0 * s;    //and its face normal, a quarter turn behind it

	float2 halfSize = ConfettiSize * input.Corner.z;
	float3 world = center + axis * (input.Corner.x * halfSize.x) + inPlane * (input.Corner.y * halfSize.y);

	//The box is centred on the camera, so this is the distance from the lens. A piece close enough to cover
	//a quarter of the frame is a smear rather than a chip; fade it instead. Held far tighter than Snow.fx's
	//equivalent on purpose - a big near piece rushing past the lens is most of what sells the effect, and
	//fading them out as eagerly as snow does throws the best of it away.
	float fade = smoothstep(ConfettiNearFade * 0.2, ConfettiNearFade, length(offset));

	//TWO-SIDED lambert on the piece's own normal: bright broadside, dark edge-on, twice a turn either way.
	float lambert = abs(dot(normal, ConfettiLight));
	float shade = ConfettiAmbient + (1.0 - ConfettiAmbient) * lambert;

	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.Color = input.Tint.rgb * shade;
	output.Corner = input.Corner.xy;
	output.Alpha = fade * ConfettiIntensity;

	return output;
}

float4 ConfettiPS(ConfettiVertexOutput input) : COLOR
{
	//The quad IS the paper, so there is no silhouette to cut - only the rim, feathered a little because
	//several thousand chips a few pixels across with hard edges crawl and shimmer as they turn. Fed from the
	//corner coordinates, so the feather is a constant fraction of the piece rather than of the screen.
	float2 a = abs(input.Corner);
	float edge = max(a.x, a.y);
	float mask = 1.0 - smoothstep(0.86, 1.0, edge);

	float alpha = saturate(mask * input.Alpha);
	clip(alpha - 0.004);

	//PREMULTIPLIED since #242, because the confetti moved out of the HDR scene and into the pipeline's sharp
	//foreground layer (the result page's defocus takes the scene and must not take the paper). That layer is
	//cleared to transparent and composited over the resolved frame by premultiplied alpha, so what is written
	//into it has to BE premultiplied and its alpha has to accumulate as coverage. Straight alpha worked while
	//this drew over an opaque scene and would not here: BlendState.NonPremultiplied squares the alpha
	//(a·a + dst·(1−a)), so every partly covered chip would report less coverage than it has and come out
	//washed against whatever the composite put behind it. Confetti.cs blends with AlphaBlend to match.
	return float4(input.Color * alpha, alpha);
}

technique Confetti
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL ConfettiVS();
		PixelShader = compile PS_SHADERMODEL ConfettiPS();
	}
};
