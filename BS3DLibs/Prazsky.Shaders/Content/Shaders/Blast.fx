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
//A BLADE (#767): the same slot drawn in a second style, the impact of a power-up round that cuts rather than bursts -
//today the Cutter severing a storey (BlastShape.z is 1, BlastShape.w the slash's half-length, BlastAxis its line). Its
//parts are its own, and since #767's verdict ("not very prominent - bigger, a bit longer - the sparks barely perceptible")
//twice the size and life of the first cut:
//  0  the FLASH    - white-hot, cooling to the cutter's amber rather than to fire
//  1  the SLASH    - one long streak along the storey the blade cut: level, through the struck ball, square to the
//                    SHOT (Game/Effects/CutSlash.cs has the derivation) and spanning what the cut severed. It shoots out
//                    from the struck ball to its ends in about a tenth of a second, holds white-hot, thins, cools to amber
//                    and burns through into torn lengths as it goes (the design references, both models: a long white-hot
//                    blade line through the cluster, a grinder's fan of sparks, a cooling line breaking into amber fragments)
//  2  the SPARKS   - a grinder's spray: thrown off the cut, along the line and towards the gun, flat and fast, white-hot
//  3  and as many  cooling to amber, living about a second; part 3 is the blade's extra half, which a bomb collapses
//No ring and no disc in either style: nothing here is a perfect circle (#389, #640).
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
//  BlastShape.z     the style: 0 a bomb, 1 a blade (#767)
//  BlastShape.w     a blade's half-length in world units: how far the slash reaches either side of its middle
//  BlastAxis.xyz    a blade's line (#767's verdict): level, unit, square to the shot, and cross(up, it) points back at
//                   the gun (CutSlash.Axis)
//  BlastAxis.w      where the slash's middle is along that line from the struck ball's centre, world units
float4 BlastCentre[MAX_BLASTS];
float4 BlastShape[MAX_BLASTS];
float4 BlastAxis[MAX_BLASTS];

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
//speed - Fireworks' SparkSize and the SparkStretch its bursts had until #612's reference pass, at the arena's scale.
static const float SPARK_SIZE = 0.08;
static const float SPARK_STRETCH = 0.035;

//THE BLADE (#767). Its palette is the cutter's own light (BasicEffectParamsProvider.CutterAccent, white-hot) cooling to
//the amber the cutter's teeth cool to (BallCutter.fxh's CutterEmber): hot metal, not fire.
static const float3 BLADE_HOT = float3(1.0, 0.96, 0.88);
static const float3 BLADE_EMBER = float3(1.0, 0.55, 0.22);

//The flash: a cut's, nearly a bomb's size, and lingering a little longer than a bomb's - cubed as it fades, it is under a
//tenth of its light by the end of the shortest slow motion (CutBeats.FLASH_SECONDS, which a test holds this to)
static const float BLADE_FLASH_SCALE = 0.9;
static const float BLADE_FLASH_SECONDS = 0.3;

//The slash's life (CutBeats.SLASH_SECONDS, held by a test), how fast it shoots out to its length (most of it in the first
//tenth of a second, so the eye sees it run out from the struck ball), and how thick its quad is (the glow round the core;
//the core is a fraction of it, in the pixel shader). Twice the first cut's on both: "it should be bigger and last a bit
//longer" (#767's verdict).
static const float SLASH_SECONDS = 1.8;
static const float SLASH_GROW_RATE = 25.0;
static const float SLASH_HALF_THICK = 1.5;
static const float SLASH_RADIANCE = 16.0;

//How far the slash's DEPTH is pulled towards the lens: by half its own reach, up to a cap, and never past half the way to
//the lens. Only its depth: it is drawn exactly where the storey was cut, and the storey's own balls either side of it,
//released and still in place for the first frames, would hide all but a few glints (photographed, Elephant), so it is
//depth-tested as if it stood in front of them.
static const float SLASH_PULL = 0.7;
static const float SLASH_PULL_SHARE = 0.5;
static const float SLASH_PULL_MOST = 4.0;

//The grinder's spray: thrown off the cut along its line and towards the gun (never back into the cluster, which would only
//hide it), flat, faster and with less drag than a blast's fragments, so it fans out along the cut and streaks; each spark
//comes off the line where Random.w puts it, once the slash has shot out that far (BLADE_SPARK_RUN, CutBeats'
//SPARK_RUN_SECONDS). It lives about a second (CutBeats.SPARK_SECONDS), holding its light most of the way.
static const float BLADE_SPARK_ALONG = 1.0;
static const float BLADE_SPARK_TOWARDS = 0.85;
static const float BLADE_SPARK_FLATTEN = 0.3;
static const float BLADE_SPARK_RISE = 0.12;
static const float BLADE_SPARK_REACH = 0.85;
static const float BLADE_SPARK_DRAG = 2.4;
static const float BLADE_SPARK_SECONDS = 1.25;
static const float BLADE_SPARK_RUN = 0.1;

//And brighter and bigger than a blast's: the spray is most of what a cut throws, where a blast has its fireball. It was
//1.5 for both, and the owner could barely see it.
static const float BLADE_SPARK_BOOST = 2.0;
static const float BLADE_SPARK_GROW = 2.2;

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

    //A blade's impact (#767) rather than a bomb's: see the header
    bool blade = shape.z > 0.5;

    float2 corner = input.Slot.zw;
    float2 along = float2(1.0, 0.0);
    //Every path that is drawn sets these, but the slash's early exit (#767) lets fxc see a path that does not
    float halfAlong = 0.0;
    float halfAcross = 0.0;
    float3 position;
    float3 radiance = float3(0.0, 0.0, 0.0);
    float2 fire = float2(0.0, blast * 7.31);

    //A bomb has no part 3: those are the blade's extra sparks
    if (part == 3 && !blade) return Collapsed();

    if (part == 0)
    {
        float flashSeconds = blade ? BLADE_FLASH_SECONDS : FLASH_SECONDS;
        if (age > flashSeconds) return Collapsed();

        float fade = 1.0 - age / flashSeconds;
        fade = fade * fade * fade;

        //Swells almost at once and stops: most of its size is there on the first frame, which is what a flash is.
        float radius = scale * lerp(1.0, 3.0, 1.0 - exp(-age * 25.0)) * (blade ? BLADE_FLASH_SCALE : 1.0);

        //PULLED TOWARDS THE LENS by its own radius. The flash stands in the hole the blast has just opened, and a
        //billboard left at the centre is cut by the balls round the rim of that hole along hard, flat lines -
        //which reads as a sprite rather than as light. In front of the hole it is only ever hidden by what is
        //genuinely nearer the camera than the blast.
        position = centre.xyz + normalize(CameraPosition - centre.xyz) * radius;

        radiance = (blade ? lerp(BLADE_EMBER, BLADE_HOT, fade) : lerp(FIRE, HOT, fade)) * (FLASH_RADIANCE * fade);
        halfAlong = radius;
        halfAcross = radius;
    }
    else if (part == 1 && blade)
    {
        //THE SLASH (#767): one long streak along the storey the blade cut, built in the world - level, through the struck
        //ball, square to the shot (BlastAxis, CutSlash.cs) - so it lies where the cut is from any bearing, and the camera's
        //shake rolls the frame round it rather than it with the frame. Its ribbon faces the lens about the line.
        if (age > SLASH_SECONDS) return Collapsed();

        float4 axis = BlastAxis[blast];
        float3 lineAxis = axis.xyz;

        //It shoots out from the struck ball to both its ends, which need not be equally far (the storey's spread either side)
        float grow = 1.0 - exp(-age * SLASH_GROW_RATE);
        float reach = 0.12 + 0.88 * grow;
        float alongLine = lerp(axis.w - shape.w, axis.w + shape.w, input.Slot.z * 0.5 + 0.5) * reach;
        float3 onLine = centre.xyz + lineAxis * alongLine;

        float3 toLens = normalize(CameraPosition - onLine);
        float3 acrossLine = cross(toLens, lineAxis);
        float acrossLength = length(acrossLine);

        //Seen straight along its own line there is no direction across it facing the lens; the screen's own up then
        float3 thickAxis = acrossLength > 1e-3 ? acrossLine / acrossLength : CameraUp;

        //Thinning as it cools: full at the cut, a third of that by the end
        float halfThick = SLASH_HALF_THICK * scale * lerp(1.0, 0.35, saturate(age / SLASH_SECONDS));

        position = onLine + thickAxis * (input.Slot.w * halfThick);

        fire.x = age / SLASH_SECONDS;

        //Drawn where it is, depth-tested where it would be pulled towards the lens (see SLASH_PULL): the pulled point's
        //depth over this one's w puts the pulled depth under this point's own screen position
        float lensDistance = length(CameraPosition - position);
        float pull = min(min(SLASH_PULL + SLASH_PULL_SHARE * shape.w, SLASH_PULL_MOST), 0.5 * lensDistance);
        float4 drawn = mul(mul(float4(position, 1.0), View), Projection);
        float4 pulled = mul(mul(float4(position + (CameraPosition - position) * (pull / max(lensDistance, 1e-3)), 1.0), View), Projection);
        drawn.z = pulled.z / pulled.w * drawn.w;

        BlastVertexOutput slash;
        slash.Position = drawn;
        slash.Corner = float3(corner, 3.0);
        slash.Tint = float3(0.0, 0.0, 0.0);
        slash.Fire = fire;
        return slash;
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
        float life = (blade ? BLADE_SPARK_SECONDS : SPARK_SECONDS) * (0.55 + 0.9 * input.Random.x);

        //A blade's spark comes off the slash where Random.w puts it along the line, once the slash has shot out that far
        float3 origin = centre.xyz;
        float3 direction = input.Spark.xyz;

        if (blade)
        {
            float4 axis = BlastAxis[blast];
            float place = input.Random.w;

            origin += axis.xyz * ((axis.w + place * shape.w) * BLADE_SPARK_REACH);
            age -= abs(place) * BLADE_SPARK_RUN;
            if (age < 0.0) return Collapsed();

            //A grinder's spray (#767): along the cut and towards the gun (cross(up, line) points at it), flat, and lifted a
            //little so the arcs rise before they fall
            float3 towards = cross(float3(0.0, 1.0, 0.0), axis.xyz);
            direction = normalize(axis.xyz * (direction.x * BLADE_SPARK_ALONG) + towards * (abs(direction.z) * BLADE_SPARK_TOWARDS)
                + float3(0.0, direction.y * BLADE_SPARK_FLATTEN + BLADE_SPARK_RISE, 0.0));
        }

        if (age > life) return Collapsed();

        float u = age / life;

        float drag = blade ? BLADE_SPARK_DRAG : SPARK_DRAG;

        //Exponential drag in closed form, so a spark's path and its VELOCITY - which the streak needs - are a pure
        //function of its age, which is what lets the buffer be static.
        float speed = scale * (blade ? (9.0 + 15.0 * input.Spark.w) : (7.0 + 13.0 * input.Spark.w));
        float decay = exp(-drag * age);

        position = origin + direction * (speed * (1.0 - decay) / drag);
        position.y -= 0.5 * GRAVITY * age * age;

        float3 velocity = direction * (speed * decay);
        velocity.y -= GRAVITY * age;

        //Through fire to a dull ember as it dies, and only the hottest few are white: the first capture had most
        //of them white, and a spray of white streaks reads as glitter rather than as something burning. A blade's are
        //hot metal: white-hot cooling to the cutter's amber.
        float fade = (1.0 - u) * (1.0 - u);
        float heat = saturate(fade * (0.9 + 0.5 * input.Random.z) - 0.35);

        //A blade's spark holds its light most of the way and goes at the end (#767's verdict: the sparks have to read for
        //a second), while its colour still cools along the bomb's curve
        float shine = blade ? 1.0 - u * u : fade;

        radiance = blade
            ? lerp(BLADE_EMBER, BLADE_HOT, saturate(heat * 1.4)) * (SPARK_RADIANCE * BLADE_SPARK_BOOST * shine)
            : lerp(EMBER, lerp(FIRE, HOT, heat), saturate(fade * 1.5)) * (SPARK_RADIANCE * fade);

        float size = SPARK_SIZE * scale * (0.7 + 0.6 * input.Random.y) * (0.5 + 0.5 * shine) * (blade ? BLADE_SPARK_GROW : 1.0);

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
    //Part 3 is a spark like part 2 (the slash returned above with its own 3)
    output.Corner = float3(corner, min(part, 2));
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

    if (input.Corner.z > 2.5)
    {
        //THE SLASH (#767): x runs along the cut, y across it. A thin white-hot core in a soft glow, tapered to points at
        //its ends like a blade's edge, and torn along its length: a static noise along x decides where it burns through
        //as it cools (the threshold climbs with the cooling, so the thin places part first and what is left is lengths
        //of amber, the references' last stage), never a pattern that slides along it.
        float cool = input.Fire.x;
        float seed = input.Fire.y;
        float x = corner.x;
        float y = corner.y;

        float taper = saturate(1.0 - x * x);
        taper = sqrt(taper) * taper;

        float torn = 0.65 * ValueNoise(float2(x * 7.0 + seed, seed * 0.37))
                   + 0.35 * ValueNoise(float2(x * 19.0 - seed * 1.3, seed * 2.1));
        float intact = saturate((torn - cool * 0.95 + 0.25) * 4.0);

        //The core is thin against the quad (whose half-thickness is the glow's reach), and the glow soft
        float core = exp(-y * y * 16.0);
        float glow = exp(-y * y * 3.0) * saturate(1.0 - y * y);

        float heat = 1.0 - cool;
        float3 colour = lerp(BLADE_EMBER, BLADE_HOT, saturate(heat * 1.6 - 0.3));

        float strength = SLASH_RADIANCE * saturate(1.0 - cool);
        float falloff = saturate((core + 0.4 * glow) * taper * intact);

        return float4(colour * (strength * falloff), falloff);
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

        //X3571 left on purpose (#713): cool is the fireball's age over its life, 0..1 from the CPU, so 1 - cool is not negative. It
        //is a fact about the data and not about this file, which is why it is not made a saturate() the build could not show
        //unchanged (the blast is time-driven); a cool over 1 would be the CPU's fault and would surface as a NaN here.
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
