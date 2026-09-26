//InstancedModel.fx: the world-space height field of the scene surfaces - the island's relief, its slab joints,
//its dusts, bands and strata - and the per-octave band-limit the ball relief shares.

//Procedural surface relief, shared by the ball pattern and by the scene objects. Nothing here moves a
//vertex: the height field only tilts the normal, so silhouettes stay exactly as modeled and what
//changes is that a surface catches light unevenly, the way a real material does.

//Peak height of the relief on the scene objects, in world units (0 = flat shading), and the base wave
//count per world unit — larger is finer grained. Four more octaves ride on top at rising frequencies.
float SurfaceReliefStrength;
float SurfaceReliefFrequency;

//Floor slabs: joints cut into the horizontal plane, in world units. SlabSize 0 turns them off.
//These exist so the relief has something at a scale the eye can actually see: micro-relief alone is
//sub-centimeter, and on a marble floor the structure is the joints between the slabs.
float SlabSize;
float SlabJointWidth;
float SlabJointDepth;

//How far the joint grid is bent by a world-space noise, in world units (#534): 0 is the square grid every
//paving is laid in; at a unit or so the lines wander and the cells lose their corners, which is what the
//fractures in a sheet of ice look like - a net of curved lines round cells of about one size. The bend is
//one noise read per pixel of a surface that asks for it, behind a branch on the uniform.
float SlabWarp;

//What the joints' FLOORS put out, as linear radiance (#535): the volcano island's cooling cracks glowing a
//dull red from inside, the cavern island's mineral veins running where the walls' do. Zero on every other
//surface, and the branch that reads it skips. Read at the groove's floor SQUARED, so the bevel stays dark
//and only the deepest part of a crack is incandescent - which is what a cooling crack looks like.
float3 JointGlow;

//How much of the joint glow is gated by the world-space patch noise (#537): 1 lights about a third of the
//grid in patches, which is what a cooling crack or an ore vein does; 0 lights every joint evenly along its
//whole length, which is what a seam in a made thing does - the grid's vector plates, a station's panel lines.
float JointGlowPatchiness;

//Dust settled on the up-facing faces (#535): ash on the volcano's island. A modulation of the albedo rather
//than a colour - the ratio of the dust's linear colour to the material's, so 1 is no dust - and how much of
//it, 0 none. Keyed to the GEOMETRIC normal, so a slope takes less and a wall none, and the grain of the relief
//does not put dust on and off across the top.
float3 TopDustTint;
float TopDustStrength;

//The same for the SIDE faces (#534): rime crusted on the ice islands' drums. Keyed to how far the geometric
//normal is from vertical, so the top takes none and the wall all of it.
float3 SideDustTint;
float SideDustStrength;

//A band keyed to world HEIGHT on the side faces (#536): the sea stack's tide line - the rock below it dark, wet and
//greened by weed - and the beach platform's crust of sand round its foot. Everything under BandTopY takes the band,
//fading out over BandFade above it along a line a low noise wanders, so it is a tide mark and not a ruled one.
//BandTint is an albedo ratio like the dusts' (1 is none), BandWet how many times over the band mirrors the sky (a
//wet face is a mirror, 0 leaves it dry), BandStrength how much of it, 0 none - and 0 skips the branch.
float BandTopY;
float BandFade;
float3 BandTint;
float BandWet;
float BandStrength;

//Bedding planes on the side faces (#536): a stack of sedimentary rock is layers, and seen from the side each layer
//is a course of its own shade with a dark line where two meet. StrataSpacing is the layers' thickness in world
//units, StrataStrength how far the lines and courses darken; 0 is none and skips the branch. Band-limited against
//the pixel's footprint in height, so a far face goes to the courses' mean rather than crawling.
float StrataSpacing;
float StrataStrength;

//THE OFF-WORLD AND ARID FAMILY (#538). The top dust blown CLEAR round the island's axis: the lunar pad's powder, swept off in a ring round the
//drain. x is the radius in world units inside which the dust is gone, y how far outside it the dust fades back
//in; x = 0 clears nothing. The island stands on the world's axis, so the distance is the position's own.
float2 TopDustClear;


//The two above, applied to the triplanar paths' albedo (#536): returns how wet the pixel is, which the caller hands
//ShadePixel as extra sky mirrored. `side` is how far the geometric normal is from vertical, the dusts' own weight,
//and `footprintY` how much world height one pixel spans - both taken outside the branches, which hold no gradients.
float ApplyHeightBands(inout float3 texRgb, float3 worldPosition, float side, float footprintY)
{
    float wet = 0.0;

    //Both terms are for SIDE faces, and on the island most pixels are its flat top: behind a data branch on the side
    //weight as well as on the uniform, so the top pays nothing (it paid about a millisecond on the APU in the first
    //cut). The wanders are crossed sines rather than gradient noise for the same reason - a tide mark or a bedding
    //plane wants a slow undulation, not a noise's grain.
    [branch]
    if (BandStrength > 0.0 && side > 0.02)
    {
        float wander = sin(dot(worldPosition.xz, float2(0.31, 0.23))) * sin(dot(worldPosition.xz, float2(-0.17, 0.29)) + 1.3);
        float tideLine = BandTopY + wander * BandFade * 0.6;
        float inBand = saturate((tideLine - worldPosition.y) / max(BandFade, 1e-3)) * side;

        texRgb = lerp(texRgb, texRgb * BandTint, BandStrength * inBand);
        wet = BandWet * BandStrength * inBand;
    }

    [branch]
    if (StrataStrength > 0.0 && side > 0.02)
    {
        //The planes are not level: they dip and wander a little across the stack, as bedding does
        float dip = sin(dot(worldPosition.xz, float2(0.07, 0.05))) * 0.6 + sin(dot(worldPosition.xz, float2(-0.04, 0.09)) + 2.0) * 0.4;
        float y = (worldPosition.y + dip * StrataSpacing * 0.9) / StrataSpacing;
        float layer = frac(y);
        float course = frac(sin(floor(y) * 12.9898 + 4.1) * 43758.5453);

        //The line where two layers meet, a tenth of a layer wide, and each course its own shade - both faded to
        //their means as a layer shrinks towards a few pixels
        float resolve = saturate(1.5 - footprintY / StrataSpacing * 6.0);
        float seam = 1.0 - smoothstep(0.0, 0.1, min(layer, 1.0 - layer));
        float shade = lerp(0.1, seam, resolve) * 0.8 + lerp(0.5, course, resolve) * 0.4;

        texRgb *= 1.0 - StrataStrength * side * shade;
    }

    return wet;
}

//How dark the pits of the relief go from being shaded by their own walls (0 = off)
float CavityStrength;

//One octave, band-limited on the spot: a wave of this frequency spans 2 * pi / f of whatever space it
//is evaluated in, so it is faded out as a pixel grows towards half of that — its Nyquist limit.
//Attenuating each octave against its own wavelength, rather than the whole field against the finest
//one, is what lets fine detail exist at all: it stays fully present while the pixels can still resolve
//it and drops out silently when they cannot, instead of breaking into the hard checkerboard that
//point-sampled high frequencies produce through the derivatives below. Position and footprint only
//have to share units — object-space directions over a ball radius, or plain world space.
float ReliefOctave(float3 position, float3 waveDirection, float frequency, float footprint)
{
    return sin(dot(position, waveDirection) * frequency) * saturate(1 - footprint * frequency / 3.14159265);
}

//The same octave, band-limited against the footprint measured *along the wave's own direction* instead
//of against its overall extent. A pixel only fails to resolve a wave when it is wide across that wave's
//crests; how far it stretches parallel to them costs nothing. One scalar footprint cannot express that,
//and on a surface seen at a grazing angle — where a pixel covers meters along the view but stays
//millimeters across it — it reports the long axis and fades out every octave at once. The floor then
//becomes geometrically perfect exactly where it should look roughest, and takes the light like polished
//glass: the milky smear this replaces. Directionally, the waves running across the view survive.
float ReliefOctaveDirectional(float3 position, float3 waveDirection, float frequency, float3 dpdx, float3 dpdy)
{
    float footprint = abs(dot(dpdx, waveDirection)) + abs(dot(dpdy, waveDirection));

    return sin(dot(position, waveDirection) * frequency) * saturate(1 - footprint * frequency / 3.14159265);
}

//World-space grain for the scene surfaces: stone, marble and cast metal all read as an irregular
//surface rather than a polished one. Amplitudes sum to one, so SurfaceReliefStrength stays the peak
//height in world units, and the frequency ratios are irrational so the sum never settles into a tile.
//Seven octaves rather than a handful on purpose: too few waves spaced too far apart interfere into a
//regular diagonal weave instead of a surface, which is exactly what the cannon barrel showed first.
float SurfaceReliefWorld(float3 worldPosition, float frequency, float3 dpdx, float3 dpdy)
{
    return 0.26 * ReliefOctaveDirectional(worldPosition, float3(0.71, 0.52, -0.47), frequency, dpdx, dpdy)
        + 0.20 * ReliefOctaveDirectional(worldPosition, float3(-0.36, 0.83, 0.42), frequency * 1.43, dpdx, dpdy)
        + 0.16 * ReliefOctaveDirectional(worldPosition, float3(0.55, -0.44, 0.71), frequency * 2.11, dpdx, dpdy)
        + 0.12 * ReliefOctaveDirectional(worldPosition, float3(-0.82, -0.31, 0.48), frequency * 3.07, dpdx, dpdy)
        + 0.10 * ReliefOctaveDirectional(worldPosition, float3(0.31, 0.62, 0.72), frequency * 4.51, dpdx, dpdy)
        + 0.09 * ReliefOctaveDirectional(worldPosition, float3(-0.64, 0.27, -0.72), frequency * 6.73, dpdx, dpdy)
        + 0.07 * ReliefOctaveDirectional(worldPosition, float3(0.18, -0.91, 0.37), frequency * 9.87, dpdx, dpdy);
}

//The world-space relief of a scene object, ready to hand to PerturbNormalFromHeight.
//Takes the world-space screen derivatives rather than a scalar footprint so every octave can be
//band-limited along its own direction (see ReliefOctaveDirectional).
//Width of the run-out from a joint's floor up to the slab face
static const float SlabJointBevel = 0.03;

//One axis of the joint grid: 1 in the floor of a joint, 0 out on the slab face.
//
//Two things here are deliberate, and both are the lesson the ground's grain already taught. The
//footprint is the pixel's extent along this axis alone, because a joint running across the view is
//perfectly resolvable however far the pixel stretches along it. And once the pixel does grow past the
//joint, the profile widens to the pixel rather than fading out: a joint that is thinner than a pixel
//still darkens that pixel, in proportion to how much of it the joint covers. Fading it to nothing —
//which is what measuring it against its own bevel did — deletes the only structure the floor has at a
//scale the eye can see. It only leaves once the pixel can no longer resolve the slab grid itself.
//
//THE BEVEL IS WIDENED HARDER THAN THE WIDTH, AND THAT IS THE FIX FOR #351: two and a half pixels of
//run-out against the width's half. It is the shoulder, not the floor, that has to survive being sampled -
//and what it has to survive is the NORMAL being a per-quad quantity. PerturbNormalFromHeight builds its
//normal out of ddx/ddy, and a screen derivative is one value per 2x2 quad, so a shoulder that climbs
//inside one or two pixels tilts whole quads at a time. Strung along a joint running away from the eye that
//is a row of 2x2 bright blobs - the beading #126 chased into the chromatic aberration, fixed the fringing
//half of, and closed noting a residual. This is the residual. Stretched over ~5 pixels the same climb is
//spread across several quads and reads as a line again.
//
//Four things were measured on the way and none of them is worth rediscovering:
//  - it IS the direct highlight: forcing SurfaceSpecular.Highlight to 0 on this surface makes the beads
//    vanish outright, so nothing about the groove's darkening is at fault;
//  - Toksvig - the textbook answer, damping the highlight by the screen-space variance of the normal - is
//    useless here, because that variance is identically zero for exactly the reason above. Drawn out as a
//    colour the whole cap came back black, and the damping did nothing at 1.4 or at 50;
//  - the beads are NOT on the pixels the groove's coverage marks: damping the covered pixels to zero left
//    every one of them standing, and at the brightest beads the coverage-based gate measured 0.00;
//  - and they are not a sub-pixel problem either. At the brightest beads the pixel measured SMALLER than
//    the joint - the joint was fully resolved and beading anyway, which is what finally pointed at the
//    quad rather than at the footprint.
float SlabGrooveAxis(float coordinate, float footprint)
{
    float cell = frac(coordinate / SlabSize);
    float distance = min(cell, 1 - cell) * SlabSize;

    float width = max(SlabJointWidth, footprint * 0.5);
    float bevel = max(SlabJointBevel, footprint * 2.5);

    return (1 - smoothstep(width, width + bevel, distance)) * saturate(1 - footprint / (SlabSize * 0.5));
}

float SlabGroove(float3 worldPosition, float3 dpdx, float3 dpdy)
{
    if (SlabSize <= 0) return 0;

    //Extent of this pixel along X and along Z, measured separately
    float2 footprint = abs(dpdx.xz) + abs(dpdy.xz);

    //The fractures (#534): the grid's own coordinates bent by a low noise, so the lines wander. Two reads of
    //a 2D noise on XZ, one per axis (a joint bent the same way on both would keep its corners) - 2D because
    //the grid is cut in world X and Z whatever the face, so a side face carries the bend straight down its
    //height the way a fracture plane does; and cheaper than the 3D reads the first cut made (+0.50 -> +0.35 ms
    //on the polar sheet at 3840x1600, photographed identical from the #533 and the ring camera).
    float2 xz = worldPosition.xz;
    [branch]
    if (SlabWarp > 0)
    {
        xz += SlabWarp * float2(GradientNoise2(worldPosition.xz * 0.31 + 11.0), GradientNoise2(worldPosition.xz * 0.31 + 47.0));
    }

    return max(SlabGrooveAxis(xz.x, footprint.x), SlabGrooveAxis(xz.y, footprint.y));
}

//The height field the whole surface is built from: micro-relief on the slab faces, joints cut below
//them. The normal and the cavity shading both read this one function, so a feature added here is
//automatically lit and occluded rather than needing to be handled twice.
float SceneSurfaceHeight(float3 worldPosition, float3 dpdx, float3 dpdy)
{
    float height = SurfaceReliefWorld(worldPosition, SurfaceReliefFrequency, dpdx, dpdy) * SurfaceReliefStrength;

    return height - SlabGroove(worldPosition, dpdx, dpdy) * SlabJointDepth;
}

//The same two fields handing the groove OUT (#534): the triplanar techniques read it once here and spend it
//twice - in the height, and in the joint glow - where the glow used to evaluate SlabGroove a second time, which
//with the fractures' warp was two more noise reads on every island pixel (measured at half the cold family's
//cost on the polar sheet).
float SceneSurfaceHeightGroove(float3 worldPosition, float3 dpdx, float3 dpdy, out float groove)
{
    groove = SlabGroove(worldPosition, dpdx, dpdy);
    return SurfaceReliefWorld(worldPosition, SurfaceReliefFrequency, dpdx, dpdy) * SurfaceReliefStrength - groove * SlabJointDepth;
}

float SceneSurfaceHeightCoarseGroove(float3 worldPosition, float3 dpdx, float3 dpdy, out float groove)
{
    float frequency = SurfaceReliefFrequency;
    groove = SlabGroove(worldPosition, dpdx, dpdy);

    float height = (0.26 * ReliefOctaveDirectional(worldPosition, float3(0.71, 0.52, -0.47), frequency, dpdx, dpdy)
        + 0.20 * ReliefOctaveDirectional(worldPosition, float3(-0.36, 0.83, 0.42), frequency * 1.43, dpdx, dpdy)
        + 0.16 * ReliefOctaveDirectional(worldPosition, float3(0.55, -0.44, 0.71), frequency * 2.11, dpdx, dpdy)) * SurfaceReliefStrength;

    return height - groove * SlabJointDepth;
}

//Highest and lowest the field can reach: the micro-relief rides above zero, the joints cut below it
float ReliefCeiling() { return SurfaceReliefStrength; }
float ReliefFloor() { return -(SurfaceReliefStrength + SlabJointDepth); }

//A pit is shaded by its own walls, and this is the cheapest honest way to say so: the deeper a point
//sits in the field, the less of the sky it can see. Without it a normal-mapped surface has its bumps
//lit but its hollows just as bright as its peaks, which is most of why relief-by-normal reads as a
//painted-on texture rather than as shape.
float CavityOcclusion(float height)
{
    float openness = saturate((height - ReliefFloor()) / max(ReliefCeiling() - ReliefFloor(), 1e-6));

    return lerp(1 - CavityStrength, 1, openness);
}
