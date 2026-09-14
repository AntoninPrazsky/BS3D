//A bomb going off (#389): the flash, the fireball and the sparks of every blast a landing sets off, in ONE static
//vertex buffer and ONE draw call - Fireworks.fx's idiom at the scale of the arena rather than the sky. Every quad
//of every blast is animated in the vertex shader off two small per-blast uniforms, so nothing is rebuilt or
//re-uploaded per frame however long a chain is.
//
//Three parts to a blast, told apart by the part index each vertex carries:
//  0  the FLASH    - one billboard at the centre, a white-hot core that swells and is gone in a sixth of a second
//  1  the FIREBALL - one billboard whose outline and body are torn by noise, burning from white through orange to
//                    a dull red and breaking up into rags as it cools
//  2  the SPARKS   - streaks thrown out of the centre and stalled by drag; Fireworks' streak, smaller and faster
//
//⚠ There was a fourth part and the first capture threw it out: a SHOCK RING, a thin annulus racing out past the
//blast radius. In the running game it read as a halo drawn over the cluster - a clean cream-white hoop with an
//exact circular edge, and on a chain two of them, like an icon. Nothing about an explosion is a perfect circle,
//and the one thing that says "fire" is an outline that is not one; so that quad became the fireball.
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
//part below (a spark at the top of its jitter, SPARK_SECONDS * 1.45) or it would be cut off mid-fade.
static const float FLASH_SECONDS = 0.16;
static const float FIRE_SECONDS = 0.5;
static const float SPARK_SECONDS = 0.6;

//Drag on a spark, per second, and gravity. The drag is far harder than a firework's: a firework's stars are
//thrown a hundred units up into open sky, these are fragments leaving a cluster, and they have to stall inside a
//couple of blast radii or they read as tracer fire.
static const float SPARK_DRAG = 4.6;
static const float GRAVITY = 9.81;

//How fast the fireball's hot gas climbs, world units a second. A little: enough that the rags drift up off the
//hole as they cool rather than hanging where they burnt.
static const float FIRE_RISE = 1.4;

//The palette, from white heat through fire to embers. Hues normalised to a peak of 1 and driven by the radiance
//constants below, for the reason the ripple and the fireworks whiten: a saturated orange at these levels only
//clips its red channel and reads as a flat coloured disc.
static const float3 HOT = float3(1.0, 0.93, 0.80);
static const float3 FIRE = float3(1.0, 0.42, 0.10);
static const float3 EMBER = float3(0.9, 0.16, 0.04);

static const float FLASH_RADIANCE = 12.0;
static const float FIRE_RADIANCE = 4.5;
static const float SPARK_RADIANCE = 5.0;

//World half-size of a spark at full brightness, and world units of streak per world unit per second of screen
//speed - Fireworks' SparkSize and SparkStretch, at the arena's scale.
static const float SPARK_SIZE = 0.08;
static const float SPARK_STRETCH = 0.035;

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
    float3 Tint : TEXCOORD1;     //linear radiance at this part's brightness (the flash and the sparks)
    float2 Fire : TEXCOORD2;     //the fireball's own: x how far it has cooled 0..1, y a per-blast seed
};

//A degenerate quad behind the far plane: discarded before rasterization, so a dead slot or a spent part costs the
//vertex work and not one pixel.
BlastVertexOutput Collapsed()
{
    BlastVertexOutput output;
    output.Position = float4(0.0, 0.0, 2.0, 1.0);
    output.Corner = float3(0.0, 0.0, 0.0);
    output.Tint = float3(0.0, 0.0, 0.0);
    output.Fire = float2(0.0, 0.0);
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
    float3 radiance = float3(0.0, 0.0, 0.0);
    float2 fire = float2(0.0, blast * 7.31);

    if (part == 0)
    {
        if (age > FLASH_SECONDS) return Collapsed();

        float fade = 1.0 - age / FLASH_SECONDS;
        fade = fade * fade * fade;

        //Swells almost at once and stops: most of its size is there on the first frame, which is what a flash is.
        float radius = scale * lerp(1.0, 3.0, 1.0 - exp(-age * 25.0));

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
        if (age > FIRE_SECONDS) return Collapsed();

        //Billows out fast and then hangs, climbing a little as it burns out.
        float radius = scale * lerp(0.7, 2.4, 1.0 - exp(-age * 7.0));

        //Pulled forward by half its radius, not all of it: the fireball is a body in the hole, and a little of it
        //going behind the rim is what sits it IN the cluster rather than on the glass of the lens.
        position = centre.xyz + normalize(CameraPosition - centre.xyz) * (radius * 0.5);
        position.y += FIRE_RISE * age;

        fire.x = age / FIRE_SECONDS;
        halfAlong = radius;
        halfAcross = radius;
    }
    else
    {
        float life = SPARK_SECONDS * (0.55 + 0.9 * input.Random.x);
        if (age > life) return Collapsed();

        float u = age / life;

        //Exponential drag in closed form, so a spark's path and its VELOCITY - which the streak needs - are a pure
        //function of its age, which is what lets the buffer be static.
        float speed = scale * (7.0 + 13.0 * input.Spark.w);
        float decay = exp(-SPARK_DRAG * age);

        position = centre.xyz + input.Spark.xyz * (speed * (1.0 - decay) / SPARK_DRAG);
        position.y -= 0.5 * GRAVITY * age * age;

        float3 velocity = input.Spark.xyz * (speed * decay);
        velocity.y -= GRAVITY * age;

        //Through fire to a dull ember as it dies, and only the hottest few are white: the first capture had most
        //of them white, and a spray of white streaks reads as glitter rather than as something burning.
        float fade = (1.0 - u) * (1.0 - u);
        float heat = saturate(fade * (0.9 + 0.5 * input.Random.z) - 0.35);

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
    output.Fire = fire;

    return output;
}

//Cheap value noise for the fireball's outline and body. No gradient ops anywhere in it, so it is safe inside the
//branch below; the quad is a few dozen pixels across at play distance, so its cost is a rounding error.
float Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = Hash(cell);
    float b = Hash(cell + float2(1.0, 0.0));
    float c = Hash(cell + float2(0.0, 1.0));
    float d = Hash(cell + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 BlastPS(BlastVertexOutput input) : COLOR
{
    float2 corner = input.Corner.xy;
    float r2 = dot(corner, corner);

    if (input.Corner.z < 0.5)
    {
        //THE FLASH: a gaussian, so it is a hot core inside a wide glow rather than a disc with an edge, and cut
        //to nothing at the quad's rim so no square is ever seen.
        float falloff = exp(-r2 * 4.0) * saturate(1.0 - r2);
        return float4(input.Tint * falloff, falloff);
    }

    if (input.Corner.z < 1.5)
    {
        //THE FIREBALL. Three octaves of noise, the middle one churning with the cooling, decide both how far out
        //the body reaches at each angle - so the outline is torn rather than round - and where it has burnt
        //through: as it cools the threshold climbs, the thin parts go first and what is left is rags.
        float cool = input.Fire.x;
        float seed = input.Fire.y;

        float billow = 0.55 * ValueNoise(corner * 2.6 + seed)
                     + 0.30 * ValueNoise(corner * 5.5 - seed * 1.7 + cool * 2.5)
                     + 0.15 * ValueNoise(corner * 11.0 + seed * 3.1);

        float r = sqrt(r2);
        float body = saturate(1.0 - r / (0.45 + 0.55 * billow));

        float intact = saturate((billow - cool * 0.75) * 4.0);
        float falloff = pow(body, 0.7) * intact;

        //Hot in the thick of it and at the start; the edges and the end are fire and then embers.
        float heat = (1.0 - cool) * (0.55 + 0.45 * body);
        float3 colour = lerp(EMBER, lerp(FIRE, HOT, saturate(heat * 1.6 - 0.7)), saturate(heat * 2.2));

        float strength = FIRE_RADIANCE * pow(1.0 - cool, 1.5);

        return float4(colour * (strength * falloff), falloff);
    }

    //A SPARK: Fireworks' profile - a small hot core in a wide halo, stretched into a streak by the quad, and
    //brightest at its leading end so it reads as a comet and not as a stick.
    float spark = saturate(1.0 - r2);
    spark *= spark;
    spark *= 0.45 + 0.55 * saturate(corner.x * 0.5 + 0.5);

    //No clip anywhere: additive blending makes a dark pixel free, so every part fades to nothing smoothly.
    return float4(input.Tint * spark, spark);
}

technique Blast
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BlastVS();
        PixelShader = compile PS_SHADERMODEL BlastPS();
    }
};
