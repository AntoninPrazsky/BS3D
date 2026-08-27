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
//  ShellColorB.rgb  the shell's SECOND colour; each spark takes one or the other (see Random.w)
float4 ShellOrigin[MAX_SHELLS];
float4 ShellBurst[MAX_SHELLS];
float4 ShellColor[MAX_SHELLS];
float4 ShellColorB[MAX_SHELLS];
float4 ShellShape[MAX_SHELLS];

float SparkSize;      //world half-size of one spark billboard at full brightness
float SparkStretch;   //how many world units of streak per world unit per second of spark speed
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
    float3 velocity = float3(0.0, 0.0, 0.0);   //world units per second, for the motion streak

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

        //Exponential drag: the sparks leave hard and stall rather than flying off for ever. In closed form, so
        //a spark's whole path - and its VELOCITY, which the streak below needs - is a pure function of its
        //age, which is what lets the buffer be static.
        const float DRAG = 2.35;
        float expand = 1.0 - exp(-DRAG * t);

        //A small head start, so the sparks are not all mathematically coincident on the burst frame. It is
        //deliberately tiny now: it used to be an eighth of the radius, which pre-arranged the whole shell into
        //a formed sphere that then inflated rigidly - a bottle brush on a wire rather than an explosion. The
        //streak below is what actually spreads the flash's energy now.
        const float INITIAL_SPREAD = 0.015;
        float reach = lerp(INITIAL_SPREAD, 1.0, expand);

        float3 direction = input.Spark.xyz;
        direction.y *= shape.z;   //flattened shells read as rings when the lens is off their plane

        float radius = shape.x * input.Spark.w;
        position = burst.xyz + direction * (radius * reach);
        position.y -= 0.5 * Gravity * t * t;

        //The derivative of the line above. d(reach)/dt = (1 - INITIAL_SPREAD) * DRAG * e^(-DRAG t), so a spark
        //leaves at its fastest and slows hard - which is exactly the shape a streak wants, long at the flash
        //and gone by the time the stars are drifting.
        velocity = direction * (radius * (1.0 - INITIAL_SPREAD) * DRAG * exp(-DRAG * t));
        velocity.y -= Gravity * t;

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

    //Camera-facing billboard, STRETCHED ALONG ITS OWN MOTION. This is the difference between an explosion and
    //a cloud of dots drifting outwards: a burning star crossing the sky faster than the eye or a shutter can
    //resolve is seen as a LINE, and it is those lines radiating from a point that the eye reads as something
    //blowing apart. A round spark, however many there are, only ever reads as a swarm.
    //
    //It also happens to solve the flash: on the burst frame every spark is nearly coincident but moving at its
    //fastest, so each is drawn at its longest, and the energy that used to stack into one blown-out point is
    //spread down a hundred separate streaks instead.
    float2 corner = input.Slot.zw;

    //The velocity projected onto the screen plane. Its LENGTH is what the streak is scaled by, so a spark
    //coming straight at the lens has no screen motion and correctly stays a round dot instead of being
    //stretched along an arbitrary axis.
    float2 screenVelocity = float2(dot(velocity, CameraRight), dot(velocity, CameraUp));
    float screenSpeed = length(screenVelocity);

    float2 along = screenSpeed > 1e-4 ? screenVelocity / screenSpeed : float2(1.0, 0.0);
    float2 across = float2(-along.y, along.x);

    float halfLength = size + screenSpeed * SparkStretch;
    float halfWidth = size;

    float2 offset = along * (corner.x * halfLength) + across * (corner.y * halfWidth);
    position += CameraRight * offset.x + CameraUp * offset.y;

    output.Position = mul(mul(float4(position, 1.0), View), Projection);
    output.Corner = corner;

    //TWO colours per shell, split per spark. A real shell is one chemistry and one colour; a display is not,
    //and a burst that is half magenta and half gold reads as far more of an event than either alone. The
    //split is hard rather than a blend, so the two are seen AS two.
    float3 shellColour = input.Random.w < 0.5 ? colour.rgb : ShellColorB[shell].rgb;

    //The hot core: a spark is white at its brightest and only shows its own colour as it cools. Carrying the
    //peak to white is what makes a firework read as burning rather than as a coloured dot - the same reason
    //the cluster's ripple whitens (see "The ripple" in CLAUDE.md), and it matters more here because these
    //are driven hard into the glare and a saturated hue at that level just clips one channel.
    float heat = saturate(brightness * 1.35 - 0.35);
    float3 radiance = lerp(shellColour, float3(1.0, 1.0, 1.0) * max(max(shellColour.r, shellColour.g), shellColour.b), heat * 0.7);

    output.Tint = float4(radiance * brightness, brightness);

    return output;
}

float4 FireworkPS(FireworkVertexOutput input) : COLOR
{
    //A soft spark. Squared falloff off the centre gives a small hot core inside a wide halo, which is what a
    //point of light looks like through any lens - and what blooms convincingly when the glare pass takes it.
    //The quad is stretched along the spark's motion, so in the stretched frame this same round profile draws
    //an elongated streak with soft ends rather than a rectangle with hard ones.
    float r2 = dot(input.Corner, input.Corner);
    float falloff = saturate(1.0 - r2);
    falloff *= falloff;

    //And the streak is a comet, not a capsule: corner.x runs along the direction of travel, so biasing the
    //brightness towards +1 puts the hot end at the FRONT and trails it behind. A streak that is equally bright
    //at both ends reads as a stick; the taper is what says which way it is going.
    falloff *= 0.45 + 0.55 * saturate(input.Corner.x * 0.5 + 0.5);

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
