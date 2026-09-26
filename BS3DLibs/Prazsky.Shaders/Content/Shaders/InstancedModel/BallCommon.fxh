//===================================================================================================
//THE BALL TECHNIQUES, AND THE CONTRACT ALL OF THEM OWE (#304)
//
//Everything from here to the end of InstancedModelBubble draws A BALL. A map names what its balls are
//made of and that picks one of these programs (Prazsky.BS3D.GameStructure.BallStyle ->
//Prazsky.Core.Render.BallShading -> the technique table in InstancedModelRenderer); nothing about the
//lattice, the physics, the match rule or the score reads it, so a bubble level plays move for move
//like a vinyl one.
//
//Each is a TECHNIQUE and not a branch inside another one, for the reason measured on this project's
//other big shaders: a runtime branch over a whole alternative shading model costs the union of both
//register allocations in every wavefront, and these passes are occupancy-bound.
//
//THE COST OF THAT IS NOT WHAT IT LOOKS LIKE. A frame draws ONE ball technique, because a level names
//one style - so the bubble's measured ~8-10% over vinyl is what a BUBBLE LEVEL pays, not a tax the
//other levels carry, and N styles are not N times anything per frame. What N styles cost is N programs
//to compile here and N looks to keep working across the eighteen domes.
//
//WHAT EVERY ONE OF THEM MUST CARRY. A ball technique is not "PatternPS with different lighting": most
//of what it does has nothing to do with what the ball is MADE of, and a new one that drops a line of
//the following fails silently - it looks right in a screenshot of a still cluster and is wrong in play.
//
//  1. THE DISSOLVE CLIP, on BOTH signs of input.Dissolve, over cells of DissolvePixelSize. It is the
//     magazine re-colouring a loaded ball; the sign says which direction. Screen space, and the cell
//     is a whole DISPLAY pixel or more - an object-space cell is a lumpy 3D mottling and a one-target-
//     pixel cell is averaged straight back into a smooth cross-fade by the supersample resolve.
//  2. THE HEARTBEAT, normally as the whole of BallEmission(primary, worldPosition, occlusion): the
//     position term in the phase is what makes it a wave THROUGH the cluster (without it the cluster
//     strobes in lockstep, a lamp rather than something breathing), and since #303 the RESTING emission
//     inside it follows the occlusion squared while the beat's swing rides through - see the helper's
//     own comment for why a flat resting glow was the thing keeping the pile unreadable. A style whose
//     identity is its glow may occlude linearly instead (the plasma and the lava do, each saying why),
//     but a style that skips the occlusion entirely re-breaks #303 on that style alone.
//  3. THE RIPPLE, IN BOTH OF ITS MEANINGS. RippleStrength gates it, and the SIGN of input.Ripple
//     chooses: positive is the landing wave (the ball's own colour carried RippleWhiten towards white),
//     negative is the ALARM (RippleAlarmColor, a flat colour the ball has no say in, because every ball
//     in that wave has to say the same thing). A technique that implements only the positive branch
//     loses the ceiling's alarm on that style alone and nothing anywhere reports it.
//  4. SurfaceOcclusion FROM input.OcclusionData - what the neighbours take.
//  5. ApplySeaSubmerge AND ApplyKillPlaneFade on the way out, in that order. The kill-plane fade is
//     read by the ball techniques alone and is how a ball below the line stops being drawn.
//  6. A CUE, IN OBJECT SPACE, THAT THE BALL IS ROLLING. The gores exist for this; the bubble replaces
//     them with object-space film marbling (its gravity drainage is deliberately WORLD space, because
//     gravity does not turn with the ball). A material whose whole figure is world- or view-space -
//     a mirror is the obvious trap - draws a spinning ball as a still one.
//
//Points 1-5 are mechanical and every one of them is already written below twice; point 6 is a design
//constraint on the LOOK and is the one that has to be answered before a style is worth building.
//===================================================================================================

//Procedural beach-ball pattern. Evaluated in the model's own object space, so it turns with the
//object instead of sliding over it — which is the whole point: it makes a rolling ball's rotation
//readable. Gores alternate between the two colors, with a disc of the secondary color at each pole.

//The colors the gores alternate between; the material shade multiplies both
float3 PatternPrimaryColor;
float3 PatternSecondaryColor;

//Segments around the object = 2 * PatternGoreCount
float PatternGoreCount;

//Where the boundary between two gores sits in sin(azimuth). Zero splits every pair of segments
//evenly, a positive value widens the primary-colored gore at the expense of the secondary one
//(the renderer derives this from PatternGoreWidth, which says it in plain fractions).
float PatternGoreThreshold;

//Where the polar discs start, as the |Y| of the object-space direction (1 = the pole itself)
float PatternCapExtent;

//Amplitude of the molded micro-relief of the skin, in world units (0 = a perfectly smooth sphere)
float PatternReliefStrength;

//How much of its own color the ball radiates, independent of any light falling on it
float EmissiveStrength;

//How much light is carried through the shell from a source behind it
float TranslucencyStrength;

//Seconds since the level started, beats per second, and how deep the pulse swings (0 = steady glow)
float PulseTime;
float PulseSpeed;
float PulseDepth;

//WHAT AN UNBREATHING BALL GLOWS AT, as a fraction of what a breathing one does at rest (#395). One for
//the cluster's own plane, where it is the no-op; the STILL plane sets it to the cluster's own resting
//level.
//
//It exists because #252's "loaded rounds do not breathe" was implemented as PulseDepth zero, and
//PulseDepth zero does not mean "resting" - it means "always at the TOP of the swing". Every emissive
//expression here reduces to lerp(1 - PulseDepth, 1, beat), so a depth of zero pins the ball at 1 while
//the cluster it is meant to match sits at 1 - PulseDepth (0.62 in the Game) for most of the heartbeat,
//which is two short pulses and a long rest. The loaded round was therefore about 1.6x a resting cluster
//ball of its own colour before this, and on the emissive styles the emission IS the colour - the owner
//reported it as the round in the cannon being a lighter shade than the one he was shooting at.
//
//⚠ It is a SEPARATE uniform and not a smaller PulseDepth, and the difference is the whole point: a
//smaller depth would make the round breathe faintly, which is exactly what #252 removed on the owner's
//own ruling ("it is enough that the cannon's tip glows"). This leaves it perfectly steady and only moves
//WHERE it is steady.
float StillEmission = 1;

//Direction the beat travels through the cluster, and how many world units one beat spans. Offsetting
//the phase by position is what turns a cluster of balls flashing in lockstep into a wave passing
//through them - the difference between a strobe and something breathing.
float3 PulseDirection;
float PulseWavelength;

//How hard a ball flares at the peak of its own ripple, as a multiple of its colour. Zero switches the
//whole term off - which is what the map editor and the testbed leave it at, since neither has a shot
//landing in a cluster to start one.
float RippleStrength;

//How far the flare is carried to white. Enough that it lifts the channels the ball has none of - which is
//what makes it read as lighting up - while keeping enough hue that a red ball's flare is still warm and
//short of the point where every ball in the front goes the same featureless white.
static const float RippleWhiten = 0.5;

//A ripple can carry an alarm instead of the ball's own light, and the SIGN of the per-instance value says
//which: positive is the ordinary landing wave, negative the alarm. One channel, two meanings, exactly as
//Dissolve encodes its two directions - and it means a ball can only be in one wave at a time, which is
//already true of it (the newest wave to reach a ball takes it over).
//
//The flare is a flat colour the ball's own has no say in: the whole point is that every ball in the wave
//says the same thing, and a red flare tinted by a green ball is not red.
//
//A UNIFORM and not a constant, because the wave has two meanings and they must not look alike. A descent
//the ceiling forces on the player is a threat and burns red; a descent the game hands them because they
//just cleared a great deal of a tall column is a REWARD arriving, and a red flash there tells them off for
//playing well. The caller states the colour with the wave. InstancedModelRenderer sets it unconditionally
//and defaults it to the red, so a renderer nobody has told is still saying "alarm" rather than black.
float3 RippleAlarmColor;

//How bright the alarm burns (linear radiance, over GLARE_THRESHOLD so it blooms) and how much of the ball
//it takes at the peak. Short of 1: leaving a trace of the ball's own shading is what keeps the cluster
//looking like balls rather than like flat red discs cut out of the frame.
static const float RippleAlarmBrightness = 1.7;
static const float RippleAlarmCoverage = 0.95;

//A heart does not beat like a sine. Two pulses per cycle, the second smaller and close behind the
//first, then a long rest: the lub-dub that reads as alive rather than as a fading lamp.
float Heartbeat(float t)
{
    float phase = frac(t);

    //Squared by multiplication, not pow(x, 2): HLSL compiles pow as exp(y * log(x)), and log of a
    //negative is a NaN. Both bases go negative over most of the cycle, which left the whole term NaN
    //and the beat silently stuck at zero.
    float lubOffset = (phase - 0.10) * 13.0;
    float dubOffset = (phase - 0.29) * 15.0;

    float lub = exp(-lubOffset * lubOffset);
    float dub = 0.55 * exp(-dubOffset * dubOffset);

    return saturate(lub + dub);
}

//What a ball technique adds as its own light (#303 - #40's second half finally landing). The RESTING
//emission follows the occlusion, SQUARED - the bubble's measured correction (BubbleOcclusionPower)
//arriving on the opaque styles: "a light buried in the pile is exactly the one that should still show"
//was an argument about one ball seen alone, and a flat EmissiveStrength added to every ball of a pile
//equally was a floor under the whole cluster that no amount of AO could take it below - the single
//biggest reason burial did not read. What still punches through is the ANNOUNCEMENTS: the heartbeat's
//swing rides unoccluded, so the wave the beat carries through the cluster stays legible in the pile's
//interior, and the ripple and the ceiling's alarm are added after shading and are never dimmed at all.
//(The identity is the old lerp(1 - PulseDepth, 1, beat) split into its resting and swinging halves, so
//a surface ball at occlusion 1 emits exactly what it always did.)
float3 BallEmission(float3 primary, float3 worldPosition, float occlusion)
{
    float beat = Heartbeat(PulseTime * PulseSpeed - dot(worldPosition, PulseDirection) / max(PulseWavelength, 1e-4));

    return primary * EmissiveStrength * StillEmission
        * ((1 - PulseDepth) * occlusion * occlusion + PulseDepth * beat);
}

//Width of the ring outlining each disc, so the circle reads whichever gore it lands on
static const float PatternRingWidth = 0.045;

//Depth of the weld between two panels (world units) and how wide the groove is, measured in the
//value of the field whose threshold the seam follows
static const float PatternSeamDepth = 0.010;
static const float PatternSeamGoreWidth = 0.13;
static const float PatternSeamCapWidth = 0.035;

//Wave count of the coarsest relief octave, used to fade the seam grooves, which are about that broad
static const float PatternSeamFrequency = 8.0;

struct PatternVertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 ObjectPosition : TEXCOORD3;
    //Flat across the instance; interpolating a constant is free and saves a nointerpolation qualifier
    float Dissolve : TEXCOORD4;
    float Ripple : TEXCOORD5;
};

//How wide one cell of the dissolve's dither is, in pixels of the CURRENTLY BOUND target. The caller sends
//the display-pixel size it wants multiplied by however much the scene is supersampled, so the cell is a
//block of the finished image whatever the render resolution - see BallRenderSet.Draw, which is the one
//thing that sets it, the dissolve being read by the ball technique alone.
//
//It is a SCREEN-space dither, and the reason is what the effect is for: the old colour has to visibly go
//away in PIXELS rather than fading, so the player sees the game re-colouring a loaded ball instead of a
//colour quietly changing behind their back. Cells in the ball's own object space - which is what this was,
//7 of them along each axis - are cubes in the world: they turn with the ball, they take its perspective,
//and what they read as on screen is a lumpy three-dimensional mottling of the surface rather than
//pixelation of the picture. The measured trap that argued for object space was real but was an argument
//against the WRONG screen-space form: a cell one TARGET pixel across is averaged straight back into a
//smooth cross-fade by the box filter that resolves a supersampled frame. A cell a whole display pixel or
//more across is not, because every target sample inside it takes the same decision - which is exactly what
//scaling this by the supersampling factor buys.
float DissolvePixelSize;

/// A hash with no sin in it, for the same reason the cloud field's has none: sine-based hashes band
/// differently across drivers. Cheap enough to run unconditionally rather than behind a per-instance
/// branch, which would diverge within a draw call. Two-dimensional now that the cell is a block of the
/// screen; the swizzle to three components is the usual way this hash family reaches one output.
float DissolveNoise(float2 cell)
{
    float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);

    return frac((p.x + p.y) * p.z);
}

PatternVertexShaderOutput PatternVS(VertexShaderInput input, InstanceInput instance)
{
    PatternVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);
    float4 worldPosition = mul(bonePosition, world);

    output.ObjectPosition = bonePosition.xyz;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, world);
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;

    return output;
}

//PatternVS's output with one more thing on it: the EYE'S POSITION IN OBJECT SPACE, for a technique that
//casts the pixel's eye ray at something INSIDE the ball (#628's sealed sphere, #624's cavity). The
//instance's world matrix is a rotation and a uniform scale over a translation, applied to row vectors
//(p_world = p_object * R + T), so p_object = (p_world - T) * R^T with each row scaled back by its own
//length squared. Per instance and not per pixel - interpolating a constant is free - and the ray the
//pixel shader casts starts here and ends at the pixel's own object position, so the object-to-world
//rotation never has to reach the pixel stage (the instance streams carry no inverse).
struct EyeRayVertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 ObjectPosition : TEXCOORD3;
    float Dissolve : TEXCOORD4;
    float Ripple : TEXCOORD5;
    float3 EyeObject : TEXCOORD6;
};

float3 EyeInObjectSpace(InstanceInput instance)
{
    float3 toEye = EyePosition - instance.WorldRow4.xyz;

    return float3(
        dot(toEye, instance.WorldRow1.xyz) / max(dot(instance.WorldRow1.xyz, instance.WorldRow1.xyz), 1e-6),
        dot(toEye, instance.WorldRow2.xyz) / max(dot(instance.WorldRow2.xyz, instance.WorldRow2.xyz), 1e-6),
        dot(toEye, instance.WorldRow3.xyz) / max(dot(instance.WorldRow3.xyz, instance.WorldRow3.xyz), 1e-6));
}

//PatternVS with the eye carried along. Everything else is PatternVS's, deliberately: the two must not
//drift apart, since every contract point a pixel shader answers is read off these same fields.
EyeRayVertexShaderOutput EyeRayVS(VertexShaderInput input, InstanceInput instance)
{
    EyeRayVertexShaderOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 bonePosition = mul(input.Position, Bone);
    float4 worldPosition = mul(bonePosition, world);

    output.ObjectPosition = bonePosition.xyz;
    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, world);
    output.OcclusionData = instance.Custom;
    output.Dissolve = instance.Dissolve;
    output.Ripple = instance.Ripple;
    output.EyeObject = EyeInObjectSpace(instance);

    return output;
}

//Soft step across a boundary, one screen pixel wide, so the stripes do not crawl on the
//small distant balls (a scene holds thousands of them, most only a few pixels across)
float AntialiasedStep(float edge, float value)
{
    float width = max(fwidth(value), 1e-5);

    return smoothstep(edge - width, edge + width, value);
}

//Molded micro-relief of the skin: the dimples and waviness a real ball is left with when it comes
//out of the mold. Four waves along spread-out directions at frequencies sharing no common factor,
//so the sum never repeats over the ball — multiplying two waves instead would lay down a regular
//crosshatch, the same plaid the seamless cannon metal tile had to avoid. The amplitudes add up to
//one, which leaves PatternReliefStrength as the peak height in world units.
float SurfaceRelief(float3 direction, float footprint)
{
    return 0.36 * ReliefOctave(direction, float3(0.71, 0.52, -0.47), 13.0, footprint)
        + 0.27 * ReliefOctave(direction, float3(-0.36, 0.83, 0.42), 21.0, footprint)
        + 0.21 * ReliefOctave(direction, float3(0.55, -0.44, 0.71), 34.0, footprint)
        + 0.16 * ReliefOctave(direction, float3(-0.82, -0.31, 0.48), 55.0, footprint);
}

//Box-filtered coverage of the band |v| <= halfWidth by a pixel spanning w in v: the fraction of the pixel
//the band covers, exact for a hard edge. A line thinner than a pixel then darkens that pixel in proportion
//rather than vanishing (the slab joints' rule) or flickering (a plain step's), and a line wider than one is
//a hard edge one pixel soft. It is what carries a seam to the overview stand-off: the heavy ball's flash and
//the acid's meniscus are drawn with it (#631, #627), and it is the answer to SeamLine's trap from the other
//side - SeamLine fades a line OUT on its band limit, which is right for a figure that should leave; this
//converges on the line's mean, which is right for one that should stay.
float BandCoverage(float v, float halfWidth, float w)
{
    w = max(w, 1e-5);

    return saturate((min(v + 0.5 * w, halfWidth) - max(v - 0.5 * w, -halfWidth)) / w);
}
