//A bomb going off (#389): the flash, the shock and the sparks of every blast a landing sets off, in ONE static
//vertex buffer and ONE draw call - Fireworks.fx's idiom at the scale of the arena rather than the sky. Every quad
//of every blast is animated in the vertex shader off two small per-blast uniforms, so nothing is rebuilt or
//re-uploaded per frame however long a chain is.
//
//Three parts to a blast, told apart by the part index each vertex carries:
//  0  the FLASH  - one billboard at the centre, a white-hot core swelling and gone in a quarter of a second
//  1  the SHOCK  - one billboard drawn as a thin ring, racing out past the blast's own radius
//  2  the SPARKS - streaks thrown out of the centre and stalled by drag; Fireworks' streak, smaller and faster
//
//Drawn additively, depth-read but writing no depth, in linear radiance driven well OVER the glare threshold: the
//flash is meant to bloom, and a blast that does not bloom is an orange blob. SM 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#define MAX_BLASTS 8

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 CameraRight;   //view basis, handed over rather than derived: the billboards face the lens squarely
float3 CameraUp;

//Per blast, indexed by the blast number carried on the vertex.
//  BlastCentre.xyz  where the bomb's body was when it went off, world space
//  BlastCentre.w    its AGE in seconds - the C# side never sends a negative one (a chain link still waiting
//                   is sent as a dead slot instead), so zero is the instant it went off
//  BlastShape.x     size 0..1, from how much the blast took
//  BlastShape.y     1 for a live slot, 0 for a dead one - which collapses all its quads and costs no pixels
float4 BlastCentre[MAX_BLASTS];
float4 BlastShape[MAX_BLASTS];

//The three lifetimes. The C# side frees a slot at Blasts.LIFE_SECONDS, which has to cover the longest-lived
//spark below (SPARK_SECONDS times the top of its jitter) or a spark would be cut off mid-fade.
static const float FLASH_SECONDS = 0.24;
static const float SHOCK_SECONDS = 0.34;
static const float SPARK_SECONDS = 0.6;

//Drag on a spark, per second, and gravity. The drag is far harder than a firework's: a firework's stars are
//thrown a hundred units up into open sky, these are fragments leaving a cluster, and they have to stall inside a
//couple of blast radii or they read as tracer fire.
static const float SPARK_DRAG = 4.6;
static const float GRAVITY = 9.81;

//The palette, from white heat through fire to embers. Hues normalised to a peak of 1 and driven by the radiance
//constants below, for the reason the ripple and the fireworks whiten: a saturated orange at these levels only
//clips its red channel and reads as a flat coloured disc.
static const float3 HOT = float3(1.0, 0.93, 0.80);
static const float3 FIRE = float3(1.0, 0.42, 0.10);
static const float3 EMBER = float3(0.9, 0.16, 0.04);

static const float FLASH_RADIANCE = 9.0;
static const float SHOCK_RADIANCE = 3.2;
static const float SPARK_RADIANCE = 5.0;

//World half-size of a spark at full brightness, and world units of streak per world unit per second of screen
//speed - Fireworks' SparkSize and SparkStretch, at the arena's scale.
static const float SPARK_SIZE = 0.07;
static const float SPARK_STRETCH = 0.035;

//Where the shock's band sits across its quad, and how wide it is: the quad is sized so the band's middle is at
//the shock radius.
static const float SHOCK_BAND = 0.8;
static const float SHOCK_WIDTH = 0.075;

struct BlastVertexInput
{
    //(blast index, part 0/1/2, corner x in {-1,1}, corner y in {-1,1})
    float4 Slot : TEXCOORD0;
    //(direction xyz on the unit sphere, speed 0..1) - read by the sparks only
    float4 Spark : TEXCOORD1;
    //(life jitter, size jitter, heat jitter, unused)
    float4 Random : TEXCOORD2;
};

struct BlastVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Corner : TEXCOORD0;   //xy -1..1 across the billboard, z the part - constant across a quad
    float3 Tint : TEXCOORD1;     //linear radiance at this part's brightness
};

//A degenerate quad behind the far plane: discarded before rasterization, so a dead slot or a spent part costs the
//vertex work and not one pixel.
BlastVertexOutput Collapsed()
{
    BlastVertexOutput output;
    output.Position = float4(0.0, 0.0, 2.0, 1.0);
    output.Corner = float3(0.0, 0.0, 0.0);
    output.Tint = float3(0.0, 0.0, 0.0);
    return output;
}

BlastVertexOutput BlastVS(BlastVertexInput input)
{
    int blast = (int)(input.Slot.x + 0.5);
    int part = (int)(input.Slot.y + 0.5);

    float4 centre = BlastCentre[blast];
    float4 shape = BlastShape[blast];

    float age = centre.w;

    if (shape.y <= 0.0 || age < 0.0) return Collapsed();

    //The blast's own scale: a bomb that took next to nothing still goes off plainly, one that took a full sphere
    //goes off at not quite twice the size.
    float scale = 0.55 + 0.45 * shape.x;

    float2 corner = input.Slot.zw;
    float2 along = float2(1.0, 0.0);
    float halfAlong;
    float halfAcross;
    float3 position;
    float3 radiance;

    if (part == 0)
    {
        if (age > FLASH_SECONDS) return Collapsed();

        float fade = 1.0 - age / FLASH_SECONDS;
        fade = fade * fade * fade;

        //Swells fast and stops: most of its size is there in the first few frames, which is what a flash is.
        float radius = scale * lerp(0.8, 2.6, 1.0 - exp(-age * 16.0));

        //PULLED TOWARDS THE LENS by its own radius. The flash stands in the hole the blast has just opened, and a
        //billboard left at the centre is cut by the balls round the rim of that hole along hard, flat lines -
        //which reads as a sprite rather than as light. In front of the hole it is only ever hidden by what is
        //genuinely nearer the camera than the blast.
        position = centre.xyz + normalize(CameraPosition - centre.xyz) * radius;

        radiance = lerp(FIRE, HOT, fade) * (FLASH_RADIANCE * fade);
        halfAlong = radius;
        halfAcross = radius;
    }
    else if (part == 1)
    {
        if (age > SHOCK_SECONDS) return Collapsed();

        float fade = 1.0 - age / SHOCK_SECONDS;
        fade *= fade;

        //Out to about two and a half blast radii and slowing: the shock has to be seen to leave the region the
        //blast took, or it reads as the flash's own edge.
        float radius = scale * lerp(0.9, 5.2, 1.0 - exp(-age * 8.5));

        //Pulled forward for the flash's reason, but never by more than the blast radius: pulled by its full
        //radius a wide ring would stand in front of the gun.
        position = centre.xyz + normalize(CameraPosition - centre.xyz) * min(radius, 2.0);

        radiance = lerp(FIRE, HOT, fade * 0.6) * (SHOCK_RADIANCE * fade);
        halfAlong = radius / SHOCK_BAND;
        halfAcross = halfAlong;
    }
    else
    {
        float life = SPARK_SECONDS * (0.55 + 0.9 * input.Random.x);
        if (age > life) return Collapsed();

        float u = age / life;

        //Exponential drag in closed form, so a spark's path and its VELOCITY - which the streak needs - are a pure
        //function of its age, which is what lets the buffer be static.
        float speed = scale * (5.5 + 10.5 * input.Spark.w);
        float decay = exp(-SPARK_DRAG * age);

        position = centre.xyz + input.Spark.xyz * (speed * (1.0 - decay) / SPARK_DRAG);
        position.y -= 0.5 * GRAVITY * age * age;

        float3 velocity = input.Spark.xyz * (speed * decay);
        velocity.y -= GRAVITY * age;

        //White at the flash, through fire, to a dull ember as it dies; per-spark heat so the spray is not one
        //colour changing in unison.
        float fade = (1.0 - u) * (1.0 - u);
        float heat = saturate(fade * (1.2 + 0.4 * input.Random.z) - 0.2);

        radiance = lerp(EMBER, lerp(FIRE, HOT, heat), saturate(fade * 1.5)) * (SPARK_RADIANCE * fade);

        float size = SPARK_SIZE * scale * (0.7 + 0.6 * input.Random.y) * (0.5 + 0.5 * fade);

        //Stretched along its own screen-projected motion, exactly as a firework's star is: a fragment crossing
        //the frame faster than a shutter resolves is a line, and lines radiating from a point are what the eye
        //reads as something blowing apart. A spark coming straight at the lens has no screen motion and stays
        //a round dot rather than being stretched along an arbitrary axis.
        float2 screenVelocity = float2(dot(velocity, CameraRight), dot(velocity, CameraUp));
        float screenSpeed = length(screenVelocity);

        along = screenSpeed > 1e-4 ? screenVelocity / screenSpeed : float2(1.0, 0.0);
        halfAlong = size + screenSpeed * SPARK_STRETCH;
        halfAcross = size;
    }

    float2 across = float2(-along.y, along.x);
    float2 offset = along * (corner.x * halfAlong) + across * (corner.y * halfAcross);
    position += CameraRight * offset.x + CameraUp * offset.y;

    BlastVertexOutput output;
    output.Position = mul(mul(float4(position, 1.0), View), Projection);
    output.Corner = float3(corner, part);
    output.Tint = radiance;

    return output;
}

float4 BlastPS(BlastVertexOutput input) : COLOR
{
    float2 corner = input.Corner.xy;
    float r2 = dot(corner, corner);
    float falloff;

    if (input.Corner.z < 0.5)
    {
        //THE FLASH: a gaussian, so it is a hot core inside a wide glow rather than a disc with an edge, and cut
        //to nothing at the quad's rim so no square is ever seen.
        falloff = exp(-r2 * 4.0) * saturate(1.0 - r2);
    }
    else if (input.Corner.z < 1.5)
    {
        //THE SHOCK: a soft band at SHOCK_BAND of the quad.
        float band = (sqrt(r2) - SHOCK_BAND) / SHOCK_WIDTH;
        falloff = exp(-band * band);
    }
    else
    {
        //A SPARK: Fireworks' profile - a small hot core in a wide halo, stretched into a streak by the quad, and
        //brightest at its leading end so it reads as a comet and not as a stick.
        falloff = saturate(1.0 - r2);
        falloff *= falloff;
        falloff *= 0.45 + 0.55 * saturate(corner.x * 0.5 + 0.5);
    }

    //No clip: additive blending makes a dark pixel free, so every part fades to nothing smoothly.
    return float4(input.Tint * falloff, falloff);
}

technique Blast
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BlastVS();
        PixelShader = compile PS_SHADERMODEL BlastPS();
    }
};
