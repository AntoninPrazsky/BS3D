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
//Drawn as added light over a share of covered sky (premultiplied, see the pixel shader), depth-read but
//writing no depth (the cluster, the island and the towers in front hide a burst behind them), in linear
//radiance driven well OVER the glare threshold - a firework is supposed to bloom. Driven over it in its OWN
//hue, though (#612): the palette keeps one channel at zero so the tonemap's shoulder cannot bleach it. SM 5.0.

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

float SparkSize;      //world half-size of a rising shell's comet sparks
float TrailWidth;     //world half-width of a burst spark's trail (Fireworks.TRAIL_WIDTH)
float TrailSeconds;   //how far back along its own path a burst spark's trail reaches, in seconds (Fireworks.TRAIL_SECONDS)
float PixelAngle;     //world units a pixel spans per world unit of distance, for the trail's one-pixel floor
float WhiteShare;     //the share of a shell's sparks that burn white rather than in its colours (Fireworks.WHITE_SHARE)
float HeadWhite;      //how far towards white a trail's head burns (Fireworks.HEAD_WHITE)
float TrailGain;      //how much brighter a burst spark's trail burns than the round spark it replaced (Fireworks.TRAIL_GAIN)
float Gravity;        //world units per second squared, positive downwards
float HotCore;        //how far towards white a spark goes at its brightest (Fireworks.HOT_CORE)
float HotCoreFrom;    //the brightness the white starts at, and over how much more it arrives (HOT_CORE_FROM/_WIDTH)
float HotCoreWidth;
float SkyCover;       //how much of what is behind it a spark hides per unit of its own weight squared (Fireworks.SKY_COVER)
float Visibility;     //1 with the lens in the air, down to 0 as it sinks under the sea (#761): the spark's light and cover together

struct FireworkVertexInput
{
    //(shell index, spark index normalized 0..1, corner x in {-1,1}, corner y in {-1,1})
    float4 Slot : TEXCOORD0;
    //(direction xyz on the unit sphere, speed multiplier)
    float4 Spark : TEXCOORD1;
    //(twinkle phase, size jitter, trail rank 0..1, which colour: white under WhiteShare, then the shell's two)
    float4 Random : TEXCOORD2;
};

struct FireworkVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;   //-1..1 over the quad: x from the tail's end to the head's, y across
    float4 Tint : TEXCOORD1;     //the trail's radiance, already scaled by this spark's brightness; a = that brightness
    float4 Head : TEXCOORD2;     //rgb the head's radiance (white-hot), a = the segment's half-length in half-widths
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
        output.Head = float4(0.0, 0.0, 0.0, 0.0);   //X3578 (#713): the whole output is written, though nothing reads it - the triangle is gone
        return output;
    }

    //Every spark is drawn as a SEGMENT from its tail to its head, and the two ends are where it is and where it
    //was: the rising comet's sparks are dots (tail = head), a burst spark's tail is its own position TrailSeconds
    //ago (#612's reference pass).
    float3 head, tail;
    float brightness;
    float halfWidth;

    if (age < 0.0)
    {
        //RISING. The shell is one comet: every spark is bunched onto the flight path, strung out behind the
        //head by its trail rank, so the same buffer that becomes a burst is a tail on the way up and nothing
        //has to be drawn twice. u runs 0 (launch) to 1 (burst).
        float rise = max(origin.w, 1e-3);
        float u = saturate(1.0 + age / rise);

        //Eased, so it leaves fast and slows into the burst the way a shell against gravity does.
        float climb = 1.0 - (1.0 - u) * (1.0 - u);

        //The tail lags the head, and lags further the faster the shell is going.
        float lag = input.Random.z * 0.09 * (1.0 - climb * 0.55);
        float3 onPath = lerp(origin.xyz, burst.xyz, saturate(climb - lag));

        //A little sideways scatter so the tail is a spray of sparks rather than a drawn line.
        head = onPath + input.Spark.xyz * input.Random.y * 0.35;
        tail = head;

        //Dim at the tip of the tail, and the whole comet brightens as it climbs towards going off.
        brightness = (1.0 - input.Random.z) * (0.35 + 0.65 * climb);
        halfWidth = SparkSize * 0.55;
    }
    else
    {
        //BURST. Each spark leaves along its own direction and is slowed by drag, which is what gives a
        //firework its shape: an almost instant expansion that stalls, and only then does gravity take over and
        //comb the sparks downwards into the willow.
        float t = age;
        float u = saturate(t / life);

        //Exponential drag, in closed form, so a spark's whole path is a pure function of its age - which is
        //what lets the buffer be static, and what lets the trail below ask where the spark WAS.
        const float DRAG = 2.35;

        //A small head start, so the sparks are not all mathematically coincident on the burst frame. It is
        //deliberately tiny: an eighth of the radius pre-arranged the whole shell into a formed sphere that then
        //inflated rigidly - a bottle brush on a wire rather than an explosion.
        const float INITIAL_SPREAD = 0.015;

        float3 direction = input.Spark.xyz;
        direction.y *= shape.z;   //flattened shells read as rings when the lens is off their plane
        float radius = shape.x * input.Spark.w;

        //THE TRAIL (#612's reference pass). Every reference of a real shell - the peony, the chrysanthemum, the
        //willow, a display over a field in daylight - is hundreds of THIN LINES: a burning star leaves light
        //behind it faster than a shutter or an eye resolves, so what reads is the path, not the star. So the
        //tail is the same path TrailSeconds earlier (never before the burst): on the flash the trails reach
        //back to the centre and a shell is a ball of radial lines, as the drag stalls the stars they shorten
        //and fall behind, and as gravity takes over they hang and droop - the willow, with no code of its own.
        //Until #612's pass a spark was a billboard stretched along its instantaneous velocity - a short, fat
        //streak centred on the star, reaching as far ahead of it as behind, gone to a dot within a few tenths.
        float tailAge = max(t - TrailSeconds * (0.7 + 0.6 * input.Random.y), 0.0);
        head = burst.xyz + direction * (radius * lerp(INITIAL_SPREAD, 1.0, 1.0 - exp(-DRAG * t)));
        head.y -= 0.5 * Gravity * t * t;
        tail = burst.xyz + direction * (radius * lerp(INITIAL_SPREAD, 1.0, 1.0 - exp(-DRAG * tailAge)));
        tail.y -= 0.5 * Gravity * tailAge * tailAge;

        //Fades over its life, fastest at the end. Squared, because a linear fade on something this bright
        //holds near-full for most of the life and then drops off a cliff.
        float fade = (1.0 - u) * (1.0 - u);

        //Twinkle: the stars are burning, not glowing steadily. Fast, per-spark phase, and it only ever takes
        //brightness AWAY (a spark that flares brighter than its own birth reads as a second firework).
        float twinkle = 1.0 - shape.w * (0.5 + 0.5 * sin(t * 46.0 + input.Random.x * 6.2831853));

        brightness = fade * twinkle;

        //Thin, and thinner as it burns out, never to nothing while it still gives light
        halfWidth = TrailWidth * (0.6 + 0.4 * fade) * (0.75 + 0.5 * input.Random.y);
    }

    //THE ONE-PIXEL FLOOR. A trail a fifth of a unit wide is under a pixel from most of where the camera stands,
    //and a quad under a pixel is sampled rather than drawn - it breaks into a dotted, crawling line. So it is never
    //drawn narrower than a pixel, and its light is scaled by how much it was widened, so a far trail gives the
    //same light it would if it could be drawn (Snow.fx's floor, #654).
    float3 centre = 0.5 * (head + tail);
    float pixel = PixelAngle * distance(centre, CameraPosition);
    float drawnWidth = max(halfWidth, pixel);
    float widthGain = halfWidth / drawnWidth;

    //A camera-facing quad along the segment's screen direction. A segment seen end-on has no screen length
    //and correctly draws as a round dot rather than being stretched along an arbitrary axis.
    float3 segment = head - tail;
    float2 screenSegment = float2(dot(segment, CameraRight), dot(segment, CameraUp));
    float screenLength = length(screenSegment);
    float2 along = screenLength > 1e-4 ? screenSegment / screenLength : float2(1.0, 0.0);
    float2 across = float2(-along.y, along.x);

    float halfLength = 0.5 * screenLength + drawnWidth;
    float2 corner = input.Slot.zw;
    float2 offset = along * (corner.x * halfLength) + across * (corner.y * drawnWidth);
    float3 position = centre + CameraRight * offset.x + CameraUp * offset.y;

    output.Position = mul(mul(float4(position, 1.0), View), Projection);
    output.Corner = corner;

    //TWO colours per shell, split per spark, and since #612's reference pass a share of WHITE ones among them:
    //every reference display has its bright white streaks among the coloured ones - the owner's "some very light
    //rays" - and a shell that is all hue was, in his word, too much. The split is hard rather than a blend, so the
    //kinds are seen AS kinds. White at the palette's own luminance (SPARK_LUMINANCE), so it blooms as they do.
    float kind = input.Random.w;
    float3 shellColour = kind < WhiteShare ? float3(1.5, 1.5, 1.5)
        : ((kind - WhiteShare) < 0.5 * (1.0 - WhiteShare) ? colour.rgb : ShellColorB[shell].rgb);

    //The hot core: a FRESH spark flashes towards white and shows its own colour as it cools - only a flash, and
    //only part of the way (#612): the palette's own zero channel is what keeps the rest of the life coloured.
    float heat = saturate((brightness - HotCoreFrom) / HotCoreWidth);
    float peak = max(max(shellColour.r, shellColour.g), shellColour.b);
    float3 radiance = lerp(shellColour, float3(peak, peak, peak), heat * HotCore);

    //And the HEAD burns white-hot all its life (#612's reference pass): in every reference the star at the end of
    //a coloured trail is its brightest, whitest point.
    float3 headRadiance = lerp(shellColour, float3(peak, peak, peak), HeadWhite);

    //Light by the brightness squared, as the additive blend this replaced weighted it (its colour by the brightness,
    //its alpha by the brightness again), and by the width's gain once: the floor spreads the same light wider
    float lightScale = brightness * brightness * widthGain * (age < 0.0 ? 1.0 : TrailGain);
    output.Tint = float4(radiance * lightScale, brightness * widthGain);
    output.Head = float4(headRadiance * lightScale, 0.5 * screenLength / drawnWidth);

    return output;
}

//How the light runs down a trail from its head: the share left at the tail's end, and the power it falls by
static const float TRAIL_TAIL = 0.08;
static const float TRAIL_FALL = 1.6;

float4 FireworkPS(FireworkVertexOutput input) : COLOR
{
    //A CAPSULE round the segment, in half-widths: x along it (the segment runs from -segment to +segment), y
    //across. Squared falloff off the line gives a thin hot core inside a soft edge - a line of light through a
    //lens - and the round ends mean a dot (a rising comet's spark, segment 0) is the round spark it always was.
    float segmentHalf = input.Head.a;
    float x = input.Corner.x * (segmentHalf + 1.0);
    float y = input.Corner.y;

    float2 toLine = float2(max(abs(x) - segmentHalf, 0.0), y);
    float profile = saturate(1.0 - dot(toLine, toLine));
    profile *= profile;

    //Brightest at the head (+x) and falling off towards the tail, so a trail reads as the path of the star at its
    //end and says which way it went. A dot has no length and takes the head's own light.
    float along = segmentHalf > 1e-3 ? saturate((x + segmentHalf) / (2.0 * segmentHalf)) : 1.0;
    float trail = TRAIL_TAIL + (1.0 - TRAIL_TAIL) * pow(along, TRAIL_FALL);

    //The white-hot head: the same profile round the head's point alone
    float2 toHead = float2(x - segmentHalf, y);
    float head = saturate(1.0 - dot(toHead, toHead));
    head *= head;

    //No clip. A zero-alpha pixel adds nothing and covers nothing, so the spark can fade to nothing smoothly
    //rather than being cut with a hard edge that sweeps inward as it dims - the trap ShotTrail.fx documents.
    //
    //PREMULTIPLIED (Fireworks.SparkBlend, #612): rgb is the light the spark adds, and alpha how much of the
    //frame behind it the spark covers - its weight squared, so a spark never covers more than in proportion to
    //the light it gives back, and reads as the colour rgb / alpha laid over the frame at that opacity. Over a dark
    //sky the cover takes nothing and the display is additive; over a bright one it is what lets a red spark be
    //red instead of the sky with some red added to it.
    float bodyWeight = profile * trail;
    float weight = max(bodyWeight, head);
    float3 light = (input.Tint.rgb * bodyWeight + input.Head.rgb * head) * weight;
    float a = weight * input.Tint.a;
    //Under the water the whole spark goes, light and cover together - premultiplied, so one factor fades both (#761)
    return float4(light, saturate(a * a * SkyCover)) * Visibility;
}

technique Fireworks
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FireworkVS();
        PixelShader = compile PS_SHADERMODEL FireworkPS();
    }
};
