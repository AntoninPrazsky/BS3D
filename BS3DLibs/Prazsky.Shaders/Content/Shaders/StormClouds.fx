//Broken cumulus for the storm scene (#219): cloud MASSES standing in open air around and below the arena,
//with sky between them, rather than a surface anyone stands on.
//
//⚠ WHY THIS REPLACED A HEIGHT FIELD, and it is the whole point of the scene. The first build drew the storm
//as a displaced grid - the same camera-centred terrain mesh every ground scene uses - and it read as
//landscape from every camera the game has. Three rounds of fixing its SHAPE (a mesa profile made bulbous,
//ridged noise turned back into billow noise, the relief coarsened) each made it a better landscape and none
//of them made it cloud, because the fault was never the shape:
//
//  * a height field is a SURFACE. It has one height per XZ, so it can be lumpy but it can never be broken,
//    and a white lumpy opaque surface is a snowfield. "Torn" cumulus with sky showing between the cells is
//    not expressible in it at all;
//  * seen from a camera five units above it, any such surface is being looked at from ON it, which is the
//    geometry of standing on ground - no shading undoes that;
//  * and its SILHOUETTE is a geometric edge, hard against the sky. Cloud reads as cloud because its edge
//    dissolves. That is a property of the medium, not of the outline.
//
//So the clouds are volume now: soft-edged billboard puffs clustered into cumulus cells, alpha-blended, with
//real gaps between the cells. Each puff shades as a little SPHERE rather than as a flat sprite (the disc's
//own offset gives the normal), so a mass lit from one side has a bright flank and a shaded one and reads as
//a body with volume. The pattern - a static vertex buffer of quads turned to face the camera in the vertex
//shader - is the sea's spray and the mountain's snow, and #151 measured 2000 of those at exactly nothing.
//
//Everything is written in LINEAR RADIANCE into the HDR target. Built by all three executables out of this
//directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

//The camera, and its right/up in world space for turning each quad to face it.
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

//Towards the sun, and the sun's own radiance, tinted by the dome like every other scene's.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline.
float3 ZenithColor;
float3 HorizonColor;

//Cloud reflectance (linear): a puff's sunward crown, and the deep blue-grey its underside carries.
float3 TopColor;
float3 BaseColor;

//How much of the sky's hemisphere light fills the cloud, how strongly a rim lit from behind silvers, and
//how opaque a single puff is at its middle.
float AmbientStrength;
float SilverStrength;
float PuffOpacity;

//How hard a puff's edge falls off. Low is a hard-edged ball (a sprite); high is a wisp with no body left.
float EdgeSoftness;

//The vertical extent the field occupies, so a puff can be shaded by where it stands in the layer: the tops
//take the sun, the bottoms sit in their own shadow. This is what a storm's own darkness is made of.
float LayerBottomY;
float LayerTopY;
float UnderShade;

//The lightning, as the host solved it this frame: the strike's 0..1 envelope, where its cell stands in XZ,
//its colour, how brightly it lights the cloud from inside and how far that reaches.
float FlashEnvelope;
float2 FlashCenterXZ;
float3 FlashColor;
float FlashGlow;
float FlashReach;

//Which strike is running, as the period's own index. The channel's whole path is hashed off it, so the bolt
//is frozen for the length of one flash and is a different one the next time - the same rule the envelope's
//size and the strike's placement already follow.
float FlashStrikeIndex;

//How far the field melts into the skyline, and what the haze is MADE of. Not the dome's horizon colour by
//itself: the storm's own note records that fading to a sandy horizon painted the whole scene as desert.
float3 HazeTint;
float HorizonHazeDistance;

//Where the field thins out towards the band's edges (#551): in from the narrowest one, the band's 560 across the
//wind, by more than any Game camera stands off the arena.
static const float STORM_FAR_THIN_START = 380.0;
static const float STORM_FAR_THIN_END = 460.0;
float HazeStrength;

//The wind the field drifts on (unit length, the host normalises it), and the clock it runs off.
float2 WindDirection;
float CloudTime;
float DriftSpeed;

//The band the field is built on and drifts along - half its length along the wind, both ends past the far
//plane so the wrap at them is never in frame - and how close to the arena a cell's MIDDLE may ever come,
//which the drift steers every cell round (#532). The host adds the largest cell's radius to the config's
//InnerRadius, so it is the puffs that clear the arena and not just the point they stand round.
float FieldHalfLength;
float FieldClearance;

//How much a puff shades as part of its CELL rather than as its own sphere. See StormCloudsConfig.
float MassNormalMix;

struct CloudVertexInput
{
    float3 Centre : POSITION0;

    //(corner x, corner y, radius, seed). The corner is the unit quad's own -1..1 offset, which the pixel
    //shader reads back as the puff's disc coordinate - so one attribute carries both the billboard and the
    //shading frame.
    float4 Data : TEXCOORD0;

    //The middle of the cell this puff belongs to. The bolt technique reuses this layout and leaves it zero.
    float3 MassCentre : NORMAL0;
};

struct CloudVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldCentre : TEXCOORD0;
    float3 WorldPosition : TEXCOORD1;
    float3 Corner : TEXCOORD2;   //(corner x, corner y, seed)
    float2 Depth : TEXCOORD3;    //(distance to camera, the puff's own place in the layer 0..1)
    float3 MassNormal : TEXCOORD4;
};

//Where a CELL stands this frame, as an offset from where it was built (#532). Three things happen to it, all
//off the cell's own middle so that every puff of the cell gets the same answer and the cell moves as one
//body - SceneRenderer.StormCellPosition is this function's host copy, kept in step by hand, and is what puts
//the strike inside the cell it names:
//  * it is carried downwind at DriftSpeed;
//  * it wraps: the field is a band aligned with the wind, 2 * FieldHalfLength long, and a cell carried off
//    the downwind end comes back in at the upwind one. Both ends are past where the field thins out to nothing
//    (STORM_FAR_THIN_*), so the jump is never in frame, and because the whole cell jumps at once nothing is torn. Until #532 there was no wrap
//    at all - "a wrap would tear a mass in half" - and the annulus the field was built in emptied its
//    upwind half within three minutes of a session and stood entirely downwind of the arena inside ten:
//    "the clouds are in one small part";
//  * it is steered round the arena: a Gaussian bump on the along-wind coordinate (width the clearance R,
//    so it is gone two clearances up- or downwind) pushes the cross-wind coordinate outwards - by the whole
//    of R on the arena's own lane, fading to nothing three clearances abeam - so a cell whose lane runs
//    through the island flows round it instead, and the cells that would have stood in the island's disc
//    are spread over the ring out to 3R rather than piled on its rim. The distance is never under R: the
//    lane's own cell is at R exp(-a^2 / 2R^2) abeam, and a^2 + R^2 exp(-a^2 / R^2) has its minimum, R^2,
//    at a = 0, while every other cell stands further out than that one. The band is built with no hole in
//    it, so this is what holds the clearance at launch as well as after an hour.
float2 StormCellOffset(float2 built)
{
    float2 across = float2(-WindDirection.y, WindDirection.x);
    float a = dot(built, WindDirection) + CloudTime * DriftSpeed;
    float c = dot(built, across);

    float span = 2.0 * FieldHalfLength;
    a -= span * floor((a + FieldHalfLength) / span);

    float bump = exp(-(a * a) / (2.0 * FieldClearance * FieldClearance));
    float side = c < 0.0 ? -1.0 : 1.0;
    float abeam = abs(c);
    c = side * (abeam + FieldClearance * bump * max(0.0, 1.0 - abeam / (3.0 * FieldClearance)));

    return WindDirection * a + across * c - built;
}

CloudVertexOutput CloudVS(CloudVertexInput input)
{
    CloudVertexOutput output;

    float2 corner = input.Data.xy;
    float radius = input.Data.z;
    float seed = input.Data.w;

    //The field drifts downwind, and every puff moves by what its CELL's middle moves by (#532), so a cell
    //travels as one body: its wrap through the far end of the band and its swing round the arena are one
    //offset for all of its puffs, and no puff can be left behind by either.
    float2 offset = StormCellOffset(input.MassCentre.xz);
    float3 centre = input.Centre;
    centre.xz += offset;

    //And it breathes: each puff rises and falls on its own phase, by a tenth of its own radius. Enough that
    //a mass is never quite still, far too little to read as motion.
    centre.y += sin(CloudTime * 0.21 + seed * 37.0) * radius * 0.10;

    float3 world = centre + (CameraRight * corner.x + CameraUp * corner.y) * radius;

    //The direction out of the CELL this puff belongs to. Computed here, per puff, rather than in the pixel
    //shader: it is constant across a quad, and it is what lets the cell shade as one body.
    float3 fromMass = centre - (input.MassCentre + float3(offset.x, 0.0, offset.y));
    output.MassNormal = normalize(fromMass + float3(0.0, 0.001, 0.0));

    output.WorldCentre = centre;
    output.WorldPosition = world;
    output.Corner = float3(corner, seed);
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Depth = float2(distance(CameraPosition, world),
        saturate((centre.y - LayerBottomY) / max(LayerTopY - LayerBottomY, 1e-3)));

    return output;
}

float4 CloudPS(CloudVertexOutput input) : COLOR
{
    float2 corner = input.Corner.xy;
    float seed = input.Corner.z;

    //⚠ THE OUTLINE HAS TO BE BROKEN OR EVERY PUFF IS A DISC, and a field of discs reads as sprites however
    //well it is shaded. One turn of gradient noise around the quad, on the puff's own seed, so no two are
    //cut the same way. It runs on the CORNER and not on world space: the noise then rides with the billboard
    //instead of swimming across it as the camera turns.
    float wobble = GradientNoise2(corner * 1.9 + seed * 53.0) * 0.55
                 + GradientNoise2(corner * 4.7 + seed * 91.0) * 0.22;
    float r2 = dot(corner, corner) * (1.0 + wobble);

    //The soft edge. `EdgeSoftness` is where the falloff STARTS, so under it the puff has a solid middle and
    //over it it is all fringe: this is the dial that decides body against wisp.
    float body = smoothstep(1.0, EdgeSoftness, r2);
    clip(body - 0.004);

    //--- The puff as a sphere -------------------------------------------------------------------------
    //A billboard has no normal of its own, so a flat one shades as a coin and a field of them reads as
    //paper. Reconstructing the SPHERE the disc is a section of costs one square root and is the whole
    //difference: the offset from the middle is the normal's own tangential part, and what is left over
    //along the view axis is the rest of it.
    float3 towardsEye = normalize(CameraPosition - input.WorldCentre);
    float z = sqrt(saturate(1.0 - saturate(r2)));
    float3 sphere = normalize(CameraRight * corner.x + CameraUp * corner.y + towardsEye * z);

    //⚠ AND THEN BLENDED TOWARDS THE CELL'S OWN NORMAL, which is what stops the field reading as bubble
    //wrap. A puff shaded from its own disc alone gets a full light-to-dark gradient across it and a crisp
    //circular edge, so a cell built of them is a heap of glossy balls - the first thing the eye names.
    //Mixing in the direction out of the cell's middle makes the CELL the thing being lit, which is what it
    //is, and lets the individual puffs disappear into it.
    float3 normal = normalize(lerp(sphere, input.MassNormal, MassNormalMix));

    //--- Colour --------------------------------------------------------------------------------------
    float upFacing = saturate(normal.y * 0.5 + 0.5);

    //A cloud has no colour of its own: its crown is the colour of the sun and its underside the colour of
    //the sky. Both splits are WIDE, for the reason the sky's own deck records - ACES eats cloud contrast,
    //so two linear values close together in the highlights tonemap to the same white.
    float3 cloud = lerp(BaseColor, TopColor, upFacing * upFacing);

    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, upFacing);

    float3 color = cloud * (SunColor * ndotl + skyAmbient * AmbientStrength);

    //Where the puff stands in the layer. A storm is dark UNDERNEATH because the cloud above it is in the
    //way, and that is a property of the whole field rather than of any one puff - so it is taken from the
    //puff's own height and not from its normal.
    color *= lerp(UnderShade, 1.0, input.Depth.y * input.Depth.y);

    //THE SILVER LINING. Cloud is strongly forward-scattering, so a rim with the sun behind it is the
    //brightest thing in the sky - the single cue that separates a cloud from a hill of grey stone. It rides
    //the puff's own edge, which is where a billboard's optical depth is least.
    //
    //⚠ But it belongs to the CELL's edge and not to every puff's, and until #510 every puff inside the cell
    //drew its own bright ring - which is precisely the glossy-ball signature `MassNormalMix` was added to
    //kill in the diffuse term, arriving again through the additive one. The rim is gated on how near the
    //puff stands to the cell's own silhouette: `MassNormal` is the direction out of the cell's middle, so a
    //puff square-on to the eye has |dot| near 1 and takes none of it, and one out at the cell's edge has
    //|dot| near 0 and takes it all. One dot and an abs, on a value the vertex shader already hands over.
    float towardsSun = saturate(dot(-towardsEye, SunDirection));
    float cellRim = saturate(1.0 - abs(dot(input.MassNormal, towardsEye)));
    color += TopColor * SunColor * (SilverStrength * saturate(r2) * pow(towardsSun, 4.0) * cellRim);

    //--- The flash -----------------------------------------------------------------------------------
    //Lightning lights cloud FROM INSIDE: a whole cell goes translucent for a beat. So this is emissive and
    //gated on distance from the strike, never a light with a normal - a normal-lit flash reads as a second
    //sun. Behind a [branch] on a uniform: non-divergent, and no gradient operation inside, so it is
    //derivative-safe (BestPractices.md's rule).
    [branch]
    if (FlashEnvelope > 0.0)
    {
        float toStrike = length(input.WorldCentre.xz - FlashCenterXZ);
        float near = saturate(1.0 - toStrike / max(FlashReach, 1e-3));

        //Squared, so it is a cell lighting up rather than the whole sky brightening, and weighted onto the
        //part the sun is NOT already lighting - which is where a discharge inside the cloud shows.
        float shaded = 0.30 + 0.70 * (1.0 - ndotl);

        color += FlashColor * (FlashGlow * FlashEnvelope * near * near * shaded);
    }

    //--- The air -------------------------------------------------------------------------------------
    //Aerial perspective, so the far cells sit behind air instead of being cut out of the sky. The eighth
    //power is the tropical beach's own correction: a scene whose horizon is itself the subject would be
    //half hazed at the skyline under a fourth.
    float haze = saturate(input.Depth.x / max(HorizonHazeDistance, 1e-3));
    color = lerp(color, HazeTint, pow(haze, 8.0) * HazeStrength);

    //And the far edge of the field thins out to nothing before the band's own edges (#551). Until then the camera's
    //500-unit far plane did this job, as a hard cut: the band reaches 560 across the wind and wraps 780 along it,
    //and the far plane went to 2000 when the open-ground scenes got their far field, so every wrap and both of
    //the band's sides would otherwise be in frame. Faded by the puff's distance from the lens over the stretch the
    //clip used to fall in, so the storm's horizon stands where it always stood - it only stopped being a line.
    float farThin = 1.0 - smoothstep(STORM_FAR_THIN_START, STORM_FAR_THIN_END, input.Depth.x);

    //⚠ PREMULTIPLIED (#675). MonoGame's BlendState.AlphaBlend is (One, InverseSourceAlpha), so this returned its
    //straight colour ADDED at full strength for every puff and let the alpha only darken what lay behind: a
    //cell of overlapping puffs summed its colours into a white that the tonemap clipped, every puff kept a
    //bright outline whatever `EdgeSoftness` said, and a thin wisp was as bright as the cloud's heart. The
    //alpha now weights the colour it adds, so a wisp is a faint veil over the sky and the heart converges on
    //the lit colour instead of exceeding it.
    float alpha = body * PuffOpacity * farThin;

    return float4(color * alpha, alpha);
}

//=====================================================================================================
//The discharge
//=====================================================================================================
//
//The bolt itself, which #219 asked for in as many words: without it the flash is light with no source in
//the frame, and that reads as the sun blinking rather than as lightning.
//
//The channel is generated ENTIRELY IN THE VERTEX SHADER off the strike's own period index. Nothing is
//uploaded per frame but where the channel starts and which way it runs (the host latches both when the
//strike begins, #750), and nothing is stored: the buffer carries only which bolt a vertex belongs to, how far
//along it stands and which side of the channel it is - the kinks and the forks are hashed, the same way
//every other thing about a strike in this scene already is.
//
//⚠ WHY IT RUNS WHERE IT RUNS (#750, measured in the Game). It used to drop straight down from inside its cell
//to well under the field, and the owner reported seeing the flash's light and never the bolt. All three
//reasons held: a channel inside its cell was drawn over the cell the glow had just lit white, and an additive
//channel over white adds nothing the tonemap can show; below the cells it stood behind the island, since from
//the play camera everything under the island's own level is; and half the strikes were outside the frame.
//So the host picks a strike the camera sees, the channel leaves its cell sideways and dives across the open
//gap beside it, and the cloud lights a beat after the channel's first stroke (StormBackdrop.GLOW_LAG_SECONDS).
//
//The vertex data is packed into the puffs' own layout, since the two differ in nothing but meaning:
//  Centre = (bolt index, t along the channel 0..1, side -1/+1), Data unused.

//The channel's two parts (#750): a hair-thin white CORE, whose radiance is far over the glare threshold so
//the glare pass turns it into a stroke of light, and the violet-blue HALO round it, which is what a
//photograph of a daylight strike shows the channel by where it crosses bright cloud. Both are drawn in
//SCREEN space, as shares of the target's height - a channel is a filament far thinner than a pixel at any
//distance in this scene, and what is drawn is its glare, which has the same width near or far.
float3 BoltColor;
float3 BoltHaloColor;
float BoltHalo;
float BoltCore;

//The channel's own envelope, which is not the cloud's: it holds bright for most of the strike and goes
//dark between its strokes, where the glow in the cloud only dims (StormBackdrop.BoltFlash).
float BoltEnvelope;

//Where this strike's channel runs, as the host latched it when the strike began (#750): from BoltOrigin,
//inside its cell, out through the cell's flank along BoltRun (unit, horizontal) for BoltReach units and
//down BoltDrop units, leaving flat and diving - across the gap beside the cell, which is the one place a
//channel can be seen from: inside the cell its own glow drowns it, and below the cells the island hides it.
float3 BoltOrigin;
float3 BoltRun;
float BoltReach;
float BoltDrop;

//The bound target's size in pixels, so the widths above are a share of the frame at any resolution.
float2 ViewportSize;

//Joints along one channel; StormBackdrop.STORM_BOLT_SEGMENTS, kept in step by hand.
#define BOLT_SEGMENTS 26.0

//The main channel at joint k (0..BOLT_SEGMENTS): the run and the dive, a slow meander across four knots, a
//walk across eight finer ones, and a jump of its own at every joint, uneven from joint to joint - so each
//segment is a straight stroke between two kinks the way a stepped leader is, and the kinks do not repeat as
//a sawtooth (the first cut gave every joint the same throw and read as a chart line, not as lightning).
//Everything is hashed off the strike's index and frozen for its length.
float3 BoltMainPoint(float k, float strike)
{
    float t = k / BOLT_SEGMENTS;
    float2 seed = float2(strike * 3.1, strike * 7.7);

    float3 up = float3(0.0, 1.0, 0.0);
    float3 depth = float3(-BoltRun.z, 0.0, BoltRun.x);

    //Leaves the cell flat and dives once out of it: a channel that came straight down would stand behind
    //the island from the play camera for all but its first few units.
    float3 p = BoltOrigin + BoltRun * (BoltReach * t) - up * (BoltDrop * pow(t, 1.4));

    //The local direction, for the kinks to be thrown across it in the plane the channel runs in (the one
    //facing the camera) rather than along the line of sight, where they would not show.
    float3 heading = normalize(BoltRun * BoltReach - up * (BoltDrop * 1.4 * pow(max(t, 0.02), 0.4)));
    float3 side = cross(heading, depth);

    float segment = (BoltReach + BoltDrop) / BOLT_SEGMENTS;

    float knot = t * 4.0;
    float knotIndex = floor(knot);
    float2 meander = lerp(NoiseHash22(seed + knotIndex * 13.7 + 5.0), NoiseHash22(seed + (knotIndex + 1.0) * 13.7 + 5.0),
        smoothstep(0.0, 1.0, frac(knot)));
    p += (side * meander.x + depth * meander.y) * (segment * 1.4 * t);

    float walk = NoiseHash22(seed + floor(t * 8.0 + 0.5) * 7.9 + 2.0).x;
    p += side * walk * (segment * 0.9 * step(0.5, k));

    float2 jump = NoiseHash22(seed + k * 29.3 + 11.0);
    float throw_ = 0.08 + 0.4 * abs(NoiseHash22(seed + k * 5.1 + 17.0).x);
    p += (side * jump.x * throw_ + depth * jump.y * 0.3) * (segment * step(0.5, k));

    return p;
}

//Joint k of channel `bolt`: channel 0 is the main one, every other a branch off one of its joints - turned
//down and to a side, a fraction of its length, kinked in turn - so the forks leave the channel where a fork
//does instead of all fanning out of one point at its top.
float3 BoltPoint(float bolt, float k, float strike)
{
    if (bolt < 0.5) return BoltMainPoint(k, strike);

    float2 seed = float2(bolt * 17.3 + strike * 3.1, strike * 7.7 + bolt * 5.3);
    float2 pick = NoiseHash22(seed + 41.0) * 0.5 + 0.5;
    float2 turn = NoiseHash22(seed + 73.0);

    float fork = floor(lerp(3.0, BOLT_SEGMENTS * 0.78, pick.x));
    float3 from = BoltMainPoint(fork, strike);
    float3 along = normalize(BoltMainPoint(fork + 1.0, strike) - BoltMainPoint(fork - 1.0, strike));

    float3 up = float3(0.0, 1.0, 0.0);
    float3 depth = float3(-BoltRun.z, 0.0, BoltRun.x);
    float3 side = normalize(cross(along, depth));

    float3 heading = normalize(along + up * (-0.55 - 0.6 * abs(turn.x)) + side * (turn.y * 0.8) + depth * (turn.x * 0.35));
    float reach = (BoltReach + BoltDrop) * (0.12 + 0.22 * pick.y);
    float segment = reach / BOLT_SEGMENTS;

    float t = k / BOLT_SEGMENTS;
    float3 kinkSide = normalize(cross(heading, depth) + 1e-4);
    float2 jump = NoiseHash22(seed + k * 23.9 + 3.0);
    float throw_ = 0.25 + 0.9 * abs(NoiseHash22(seed + k * 6.7 + 9.0).x);

    return from + heading * (reach * t) + (kinkSide * jump.x * throw_ + depth * jump.y * 0.5) * (segment * step(0.5, k));
}

struct BoltVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Along : TEXCOORD0;   //(t along the channel, side -1..1)
    float3 Shape : TEXCOORD1;   //(the core's share of the half-width, the brightness here, 1 on the main channel)
};

BoltVertexOutput BoltVS(CloudVertexInput input)
{
    BoltVertexOutput output;

    float bolt = input.Centre.x;
    float t = input.Centre.y;
    float side = input.Centre.z;

    //The same period index the envelope and the strike's own placement are hashed off. Floor of time over
    //the period is all it is; the host cannot pass an integer through a float uniform any more cheaply.
    float strike = FlashStrikeIndex;

    //The joint this vertex stands on. The buffer writes t as s / segments, so this is exact, and the two
    //quads meeting at a joint compute the same point and the same screen direction - no gap, no overlap
    //cut, at any kink.
    float k = round(t * BOLT_SEGMENTS);

    float3 here = BoltPoint(bolt, k, strike);
    float3 before = BoltPoint(bolt, max(k - 1.0, 0.0), strike);
    float3 after = BoltPoint(bolt, min(k + 1.0, BOLT_SEGMENTS), strike);

    float4 clip = mul(mul(float4(here, 1.0), View), Projection);
    float4 clipBefore = mul(mul(float4(before, 1.0), View), Projection);
    float4 clipAfter = mul(mul(float4(after, 1.0), View), Projection);

    //Widened across the channel's run ON SCREEN, in pixels, so the core is a hair at every distance and at
    //every resolution alike. A joint behind the lens has no screen direction; the ribbon collapses there.
    float2 run = (clipAfter.xy / max(clipAfter.w, 1e-3) - clipBefore.xy / max(clipBefore.w, 1e-3)) * ViewportSize;
    float2 across = normalize(float2(-run.y, run.x) + float2(1e-6, 0.0));

    //A branch thins to a point; the main channel thins a little along its run.
    float taper = bolt < 0.5 ? lerp(1.0, 0.6, t) : 0.6 * pow(1.0 - t, 0.8);
    float halfPixels = BoltHalo * ViewportSize.y * (0.3 + 0.7 * taper);
    halfPixels *= step(1e-3, clip.w) * step(1e-3, clipBefore.w) * step(1e-3, clipAfter.w);

    clip.xy += across * (side * halfPixels) * (2.0 / ViewportSize) * clip.w;

    output.Position = clip;
    output.Along = float2(t, side);

    //The core never narrower than about a pixel across (three quarters each side), or it would crawl as it
    //fell between pixel centres; a branch's tip fades rather than going thinner than that.
    output.Shape = float3(max(BoltCore, 0.75 / max(halfPixels, 1e-3)), (bolt < 0.5 ? 1.0 : 0.5) * saturate(taper * 2.5),
        bolt < 0.5 ? 1.0 : 0.0);

    return output;
}

float4 BoltPS(BoltVertexOutput input) : COLOR
{
    float across = abs(input.Along.y);

    //The core: a hard-shouldered line, white and far over the glare threshold.
    float core = saturate(1.0 - across / input.Shape.x);
    core *= core;

    //The halo: the channel's ionised air and the lens's own spread, falling to nothing at the ribbon's edge.
    float halo = 1.0 - across;
    halo *= halo;

    //The main channel's far end dies into the air rather than stopping at a line, and its first stretch, still
    //inside its cell, comes up out of the cloud rather than starting at a point in front of it.
    float fade = saturate((1.0 - input.Along.x) * 4.0) * lerp(1.0, 0.15 + 0.85 * smoothstep(0.0, 0.3, input.Along.x), input.Shape.z);

    return float4((BoltColor * core + BoltHaloColor * halo) * (input.Shape.y * fade * BoltEnvelope), 1.0);
}

technique StormClouds
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CloudVS();
        PixelShader = compile PS_SHADERMODEL CloudPS();
    }
}

technique StormBolts
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BoltVS();
        PixelShader = compile PS_SHADERMODEL BoltPS();
    }
}
