//The victory fireworks: shells that rise from around the arena, burst high over it and rain down. One static
//vertex buffer for the whole display and ONE draw call - every shell and every spark of it is animated in the
//vertex shader off a small set of per-shell uniforms, exactly as Snow.fx and Spray.fx animate their particles,
//so nothing is rebuilt or re-uploaded per frame however many shells are in the air.
//
//The buffer holds MAX_SHELLS * SPARKS_PER_SHELL quads. A vertex knows which shell it belongs to, which spark
//of that shell it is, which corner of the billboard it is, and - baked in at build time - the unit direction
//that spark flies in and a couple of per-spark randoms. The C++-side-of-the-fence half is tiny: a position, a
//colour and an age per shell.
//
//Drawn additively, depth-read but writing no depth (the cluster, the island and the towers in front hide a
//burst behind them), in linear radiance driven well OVER the glare threshold - a firework is supposed to
//bloom, and this is the one effect in the game where blowing out the highlight is the entire point. SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#define MAX_SHELLS 32

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 CameraRight;   //view basis, handed over rather than derived: the billboards face the lens squarely
float3 CameraUp;

//Per shell, indexed by the shell number carried on the vertex.
//  ShellOrigin.xyz  where it was fired from, .w how long the rise takes (seconds)
//  ShellBurst.xyz   where it goes off, .w its AGE: negative while rising, 0 at the burst, positive after
//  ShellColor.rgb   linear radiance, already boosted past the glare threshold on the CPU
//  ShellColor.a     0 for a dead slot - the whole shell collapses to a degenerate quad and costs no pixels
//  ShellShape.x     burst radius, .y spark life, .z flatten (1 = a sphere, <1 = a disc, seen edge-on as a ring)
//  ShellShape.w     twinkle strength
float4 ShellOrigin[MAX_SHELLS];
float4 ShellBurst[MAX_SHELLS];
float4 ShellColor[MAX_SHELLS];
float4 ShellShape[MAX_SHELLS];

float SparkSize;      //world half-size of one spark billboard at full brightness
float Gravity;        //world units per second squared, positive downwards

struct FireworkVertexInput
{
	//(shell index, spark index normalized 0..1, corner x in {-1,1}, corner y in {-1,1})
	float4 Slot : TEXCOORD0;
	//(direction xyz on the unit sphere, speed multiplier)
	float4 Spark : TEXCOORD1;
	//(twinkle phase, size jitter, trail rank 0..1, unused)
	float4 Random : TEXCOORD2;
};

struct FireworkVertexOutput
{
	float4 Position : SV_POSITION;
	float2 Corner : TEXCOORD0;   //-1..1 across the billboard, for the round falloff
	float4 Tint : TEXCOORD1;     //rgb radiance already scaled by this spark's brightness, a = alpha
};

FireworkVertexOutput FireworkVS(FireworkVertexInput input)
{
	FireworkVertexOutput output;

	int shell = (int)input.Slot.x;

	float4 origin = ShellOrigin[shell];
	float4 burst = ShellBurst[shell];
	float4 colour = ShellColor[shell];
	float4 shape = ShellShape[shell];

	float age = burst.w;
	float life = shape.y;

	//A dead slot, or one whose sparks have burnt out: collapse the quad. A degenerate triangle is discarded
	//before rasterization, so an idle display costs the vertex work of the buffer and not one pixel.
	if (colour.a <= 0.0 || age > life)
	{
		output.Position = float4(0.0, 0.0, 2.0, 1.0);   //behind the far plane
		output.Corner = float2(0.0, 0.0);
		output.Tint = float4(0.0, 0.0, 0.0, 0.0);
		return output;
	}

	float3 position;
	float brightness;
	float size;

	if (age < 0.0)
	{
		//RISING. The shell is one comet: every spark is bunched onto the flight path, strung out behind the
		//head by its trail rank, so the same buffer that becomes a burst is a tail on the way up and nothing
		//has to be drawn twice. u runs 0 (launch) to 1 (burst).
		float rise = max(origin.w, 1e-3);
		float u = saturate(1.0 + age / rise);

		//Eased, so it leaves fast and slows into the burst the way a shell against gravity does.
		float climb = 1.0 - (1.0 - u) * (1.0 - u);
		float3 head = lerp(origin.xyz, burst.xyz, climb);

		//The tail lags the head, and lags further the faster the shell is going.
		float lag = input.Random.z * 0.09 * (1.0 - climb * 0.55);
		float3 tail = lerp(origin.xyz, burst.xyz, saturate(climb - lag));

		//A little sideways scatter so the tail is a spray of sparks rather than a drawn line.
		position = tail + input.Spark.xyz * input.Random.y * 0.35;

		//Dim at the tip of the tail, and the whole comet brightens as it climbs towards going off.
		brightness = (1.0 - input.Random.z) * (0.35 + 0.65 * climb);
		size = SparkSize * 0.55;
	}
	else
	{
		//BURST. Each spark leaves along its own direction and is slowed by drag, which is what gives a
		//firework its shape: an almost instant expansion that stalls, and only then does gravity take over and
		//comb the sparks downwards into the willow.
		float t = age;
		float u = saturate(t / life);

		//Exponential drag: the sparks expand fast and stall rather than flying off for ever. In closed form,
		//so a spark's whole path is a pure function of its age - which is what lets the buffer be static.
		const float DRAG = 2.35;
		float expand = 1.0 - exp(-DRAG * t);

		//And they start ALREADY SPREAD, which matters far more than it sounds. With every spark leaving from
		//one point, all of them are coincident on the frame the shell goes off: 320 sparks stack additively
		//into a single pixel-wide sample so bright that the glare pass turns it into a six-armed star, and the
		//burst reads as a lens artifact rather than as an explosion. Starting them a tenth of the radius out
		//spreads that same energy over an area from the first frame.
		const float INITIAL_SPREAD = 0.12;
		float reach = lerp(INITIAL_SPREAD, 1.0, expand);

		float3 direction = input.Spark.xyz;
		direction.y *= shape.z;   //flattened shells read as rings when the lens is off their plane

		position = burst.xyz + direction * (shape.x * input.Spark.w * reach);
		position.y -= 0.5 * Gravity * t * t;

		//Fades over its life, fastest at the end. Squared, because a linear fade on something this bright
		//holds near-full for most of the life and then drops off a cliff.
		float fade = (1.0 - u) * (1.0 - u);

		//Twinkle: the stars are burning, not glowing steadily. Fast, per-spark phase, and it only ever takes
		//brightness AWAY (a spark that flares brighter than its own birth reads as a second firework).
		float twinkle = 1.0 - shape.w * (0.5 + 0.5 * sin(t * 46.0 + input.Random.x * 6.2831853));

		brightness = fade * twinkle;

		//Sparks shrink as they burn out, but never to nothing while they are still bright.
		size = SparkSize * (0.45 + 0.55 * fade) * (0.7 + 0.6 * input.Random.y);
	}

	//Camera-facing billboard. The basis comes from the CPU, so this is two multiply-adds rather than a cross
	//product per vertex.
	float2 corner = input.Slot.zw;
	position += CameraRight * (corner.x * size) + CameraUp * (corner.y * size);

	output.Position = mul(mul(float4(position, 1.0), View), Projection);
	output.Corner = corner;

	//The hot core: a spark is white at its brightest and only shows its own colour as it cools. Carrying the
	//peak to white is what makes a firework read as burning rather than as a coloured dot - the same reason
	//the cluster's ripple whitens (see "The ripple" in CLAUDE.md), and it matters more here because these
	//are driven hard into the glare and a saturated hue at that level just clips one channel.
	float heat = saturate(brightness * 1.35 - 0.35);
	float3 radiance = lerp(colour.rgb, float3(1.0, 1.0, 1.0) * max(max(colour.r, colour.g), colour.b), heat * 0.7);

	output.Tint = float4(radiance * brightness, brightness);

	return output;
}

float4 FireworkPS(FireworkVertexOutput input) : COLOR
{
	//A round, soft spark. Squared falloff off the centre gives a small hot core inside a wide halo, which is
	//what a point of light looks like through any lens - and what blooms convincingly when the glare pass
	//takes it.
	float r2 = dot(input.Corner, input.Corner);
	float falloff = saturate(1.0 - r2);
	falloff *= falloff;

	//No clip. Additive blending makes a zero-alpha pixel free, so the spark can fade to nothing smoothly
	//rather than being cut with a hard edge that sweeps inward as it dims - the trap ShotTrail.fx documents.
	float a = falloff * input.Tint.a;
	return float4(input.Tint.rgb * falloff, a);
}

technique Fireworks
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL FireworkVS();
		PixelShader = compile PS_SHADERMODEL FireworkPS();
	}
};
