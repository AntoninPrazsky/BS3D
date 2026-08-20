//A coloured halo around ONE ball, in that ball's own colour: the cannon saying which round fires next (#236).
//One camera-facing billboard centred on the ball, additive, in linear radiance boosted over 1 so it blooms
//through the glare pass like the emissive balls do. Depth-READ but writing no depth, exactly as ShotTrail.fx
//is - and that is not only politeness towards the frame, it is what shapes this effect:
//
//  THE HOLE IN THE MIDDLE IS THE DEPTH BUFFER'S DOING, not a figure in here. The quad passes through the
//  ball's centre, so the ball's front hemisphere is nearer the lens than every texel of the quad behind it and
//  the depth test throws that part away. What survives is the annulus outside the silhouette - a halo AROUND
//  the ball rather than a wash OVER it. Which is the whole reason this mechanism was reached for: #236 records
//  two measured dead ends before it, and both fail by adding light to the ball itself. A same-hue flare
//  through InstancedModel.fx's positive RippleStrength branch was tried at full strength and "could not be
//  seen on screen at all", because it piles energy into a channel already near the top of the ACES curve; and
//  the negative branch, which replaces a ball's shading with a flat colour, is one uniform per draw call and
//  so cannot carry a single slot's own hue. A halo puts the colour where there was NONE - the dark bore, the
//  sky behind the muzzle - and that is why it reads where those did not.
//
//  The same test is what makes it read as light escaping the loading window: the barrel is in front of the
//  round from the game camera, so most of the halo is rejected by the tube and what is left comes out of the
//  notch. The gun is lit from inside by the colour it is about to fire.
//
//Anchored, sized and coloured entirely from uniforms - the vertex buffer is one unit quad whose positions are
//ignored, like ShotTrail's, so nothing is written per frame. Built by the GAME alone for now: the Testbed
//shares the gun and the magazine (#76) but marks no round, and the editor loads no cannon. It lives in this
//directory anyway, with every other shader, so there is one copy of it the day a second executable wants it.
//SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 View;
float4x4 Projection;

//The view basis, handed over rather than derived from the matrix: the billboard has to face the lens squarely
//and Fireworks.fx passes it the same way for the same reason.
float3 CameraRight;
float3 CameraUp;

float3 GlowCenter;      //the ball's world position - the halo is concentric with it
float GlowRadius;       //world half-size of the billboard: comfortably over the ball's own radius, or the
                        //annulus the depth test leaves would be too thin to read
float3 GlowColor;       //linear radiance, already boosted and hue-floored on the CPU
float GlowStrength;     //the breath, 0..1 - what pulses, in place of #175's white ripple on the ball itself

struct GlowVertexInput
{
	float3 Position : POSITION0; //ignored; the quad is placed from GlowCenter and the view basis
	float2 Corner : TEXCOORD0;   //-1..1 across the billboard, for the round falloff
};

struct GlowVertexOutput
{
	float4 Position : SV_POSITION;
	float2 Corner : TEXCOORD0;
};

GlowVertexOutput BallGlowVS(GlowVertexInput input)
{
	float3 world = GlowCenter
		+ CameraRight * input.Corner.x * GlowRadius
		+ CameraUp * input.Corner.y * GlowRadius;

	GlowVertexOutput output;
	output.Position = mul(mul(float4(world, 1.0), View), Projection);
	output.Corner = input.Corner;

	return output;
}

float4 BallGlowPS(GlowVertexOutput input) : COLOR
{
	//Round, and zero at the rim: squared so there is a hot centre inside a wide soft skirt, which is what a
	//glow looks like (Fireworks.fx's sparks are the same curve, for the same reason). Squaring also keeps the
	//quad's corners at exactly nothing, so the billboard never shows its own square edge.
	float r2 = dot(input.Corner, input.Corner);
	float falloff = saturate(1.0 - r2);
	falloff *= falloff;

	//No clip: additive blending makes a zero-alpha pixel add nothing, so this can fade to nothing everywhere
	//rather than being cut off at a threshold that would sweep inward as the breath dims (ShotTrail's note).
	float a = falloff * GlowStrength;
	return float4(GlowColor * a, a);
}

technique BallGlow
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL BallGlowVS();
		PixelShader = compile PS_SHADERMODEL BallGlowPS();
	}
};
