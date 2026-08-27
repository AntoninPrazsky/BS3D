//Draws the storm (#219): the arena hangs in clear high air over an unbroken deck of storm cloud, with
//convective turrets towering out of it and lightning flashing inside them. The seventeenth scene.
//
//⚠ WHY THIS IS BUILT AS TERRAIN AND NOT AS CLOUD, which is the whole design and is not what the issue
//sketched. Three separate mechanisms rule out reusing the shared cloud field (Clouds.fxh / CloudField) for
//a deck BELOW the arena, and all three were read before a line of this was written:
//
//  1. Sky.fx's ray-plane crossing is NOT sign-safe from above. It computes
//     `climb = max(direction.y, 0.02); distanceToPlane = (CloudPlaneY - CameraPosition.y) / climb;`
//     — climb is clamped POSITIVE while the numerator goes negative once the plane is below the eye, so
//     every direction returns a negative distance and samples a mirror point behind the camera.
//  2. A second gate makes it moot anyway: `smoothstep(0.0, CloudHorizonFade, direction.y)` is zero for
//     every ray at or below the horizon, so the shared field draws NOTHING in the lower hemisphere.
//  3. CloudSunlight() does not return 1 above the plane — its own clamp
//     `max((CloudPlaneY - worldPosition.y) / climb, 0.0)` degenerates to sampling the point's own XZ
//     column, which under a storm preset returns about ShadowFloor (0.16). A deck field with a live
//     coverage gain would therefore put the arena, the balls and the gun at a permanent 16 % of sun.
//
//And the Cloud* uniform namespace on a scene effect belongs to the sky's weather regardless: the host
//pushes `frame.ApplyClouds?.Invoke(effect)` into every scene effect each frame, so a deck expecting its own
//CloudPlaneY there would have it overwritten. Hence: own uniforms under own names, own geometry, and
//DrawStorm deliberately does NOT invoke the cloud hook (a deck does not want a cloud shadow cast on it —
//it IS the cloud).
//
//⚠ AND WHY THE TURRETS ARE THE SCENE RATHER THAN THE DECK. The gameplay lens is pinned at
//GameCameraFit.LENS_FLOOR_Y = -7.9, a bare 0.6 over ArenaIsland.TOP_Y = -8.5, and the island's disc is
//wider than the frame — so the stone occludes everything below ~0.6 degrees of depression and a flat
//surface at height C first appears about 96*(-7.9 - C) units out, which for this deck's own level is past
//the 500-unit far plane. Measured, not derived: the sea, an entire ocean 4.5 units below the arris, is
//EIGHT PIXELS of a 939-pixel frame from the real play camera. So the deck itself is nearly invisible in
//play and looks perfect in the map editor and in any free-camera shot, which is precisely the defect the
//Moon shipped. What reaches the play frame is relief cresting ABOVE the lens (the Moon's highland belt, the
//desert's dunes) — the turrets — plus the flash, since light arrives even when its source does not.
//
//Everything is written in LINEAR RADIANCE into the HDR target. Built by all three executables out of this
//directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//The shared noise library only - deliberately NOT Clouds.fxh (see the header).
#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

float3 CameraPosition;

//Towards the sun, and the sun's own radiance, tinted by the dome like every terrain scene's.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline.
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), so the deck sits still in the
//world while the mesh slides under it.
float2 OriginXZ;

//Radius of the island's footprint cut out of the deck around the world origin. The map editor draws no
//island and leaves it 0.
float IslandHoleRadius;

//The deck: its mean cloud-top level (the island's foot) and the clearing the island stands in.
float StormLevelY;
float ClearingRadius;
float ClearingTransition;

//Peak height of the deck's own billow between the turrets.
float BillowHeight;

//The turrets: how far apart their lattice cells sit, how many carry one, how tall the tallest stands, and
//how far its anvil top spreads.
float TurretSpacing;
float TurretChance;
float TurretHeight;
float AnvilSpread;

//Cloud reflectance (linear): the sunlit top, the shaded flank, and the deep base no sun reaches.
float3 TopColor;
float3 ShadeColor;
float3 BaseColor;

//How hard the cloud's own relief breaks its shading up, and how strongly a grazing angle silvers its rim.
float BillowRelief;
float SilverStrength;

//How much of the sky's hemisphere light fills the deck.
float AmbientStrength;

//The lightning, as the host solved it this frame: the strike's 0..1 envelope, where its cell stands in XZ,
//its colour and how brightly it lights the cloud from inside. Zero envelope costs a saturate and nothing
//else - see the [branch] in the pixel shader.
float FlashEnvelope;
float2 FlashCenterXZ;
float3 FlashColor;
float FlashDeckGlow;
float FlashReach;

//The air: how far the deck melts into the skyline, how much of the haze lands at its fullest, and what the
//haze is MADE of. That last one is not optional and the first build proved it: written as two stages both
//fading to the dome's own HorizonColor, the deck came out beige under dome 11's sandy horizon — the whole
//scene photographed as desert dunes. It is the outback's own recorded trap ("a dome with a teal horizon
//paints the far plain green — aerial perspective behaving correctly and still the wrong picture"), and the
//answer is its answer: the mid-distance keeps the SCENE's colour and only the last stretch arrives at the
//dome's exact horizon, so the mesh's edge still has no seam.
float3 HazeTint;
float HorizonHazeDistance;
float HazeStrength;

//The wind, and the clock the deck drifts on.
float2 WindDirection;
float StormTime;
float DriftSpeed;

//=====================================================================================================
//The deck
//=====================================================================================================

//How far past its base a turret's own spread reaches, as a multiple of its radius - the figure the
//single-cell lattice below is kept honest by. Outback.fx's TALUS_REACH under another name and for the same
//job: a turret's margin inside its cell is its FULL reach, anvil included, so nothing it draws can cross
//into a cell that is never read.
#define ANVIL_REACH 1.55

float2 RotateInto(float2 p, float2 axis)
{
    return float2(dot(p, axis), dot(p, float2(-axis.y, axis.x)));
}

float2 RollDirection(float2 roll)
{
    float2 v = roll * 2.0 - 1.0;

    return v * rsqrt(max(dot(v, v), 1e-4));
}

//One lattice of convective turrets. Ported from Outback.fx's RockLayer - the single-cell jittered lattice
//the Moon's craters, the space starfield and the meadow's wildflowers all use, so only the pixel's OWN cell
//is ever read (a handful of hashes) rather than the nine a neighbourhood walk costs. That matters here for
//the outback's own reason: this field is evaluated FOUR times per pixel (the vertex tap plus the normal's
//three).
//
//What is different from rock, and it is the whole shape: a storm cell is not a dome. It rises steeply,
//then FLATTENS and spreads where it hits the tropopause - the anvil - so the profile is a shoulder rather
//than a falloff, and the top is wider than the waist. `shape` comes back as the 0..1 coverage the pixel
//shader shades cloud by, `crest` as how near this point is to the turret's own top (which is what the
//sunlit/shaded split rides).
float TurretLayer(float2 p, float cellSize, float seed, float chance, float height,
    out float shape, out float crest)
{
    shape = 0.0;
    crest = 0.0;

    float2 q = p / cellSize;
    float2 cellId = floor(q);
    float2 f = q - cellId;

    float2 rollA = NoiseHash22(cellId + seed) * 0.5 + 0.5;

    //Not every cell carries a turret: the empty ones are what break the lattice.
    if (rollA.x > chance) return 0.0;

    float2 rollB = NoiseHash22(cellId + seed + 23.7) * 0.5 + 0.5;
    float2 rollC = NoiseHash22(cellId + seed + 57.1) * 0.5 + 0.5;
    float2 rollD = NoiseHash22(cellId + seed + 91.3) * 0.5 + 0.5;

    //A turret's own width, as a fraction of its cell. This dial has been wrong in BOTH directions and the
    //window between them is the whole shape of the scene: at 0.085-0.145 of a 250-unit cell the towers were
    //as wide as they were tall and photographed as beige dunes; cut to 0.055-0.095 they came out as sharp
    //white PINNACLES, which read as ice needles rather than as cloud. A thunderhead is a broad, bulbous
    //thing that happens to be tall — so this is back up, and the crown is broadened with it (`shoulder`
    //below) while the LUMP in StormHeight is what actually keeps it from being a smooth cone.
    //
    //⚠ A THIRD READING, and it is the one the owner rejected the scene on: at 0.075-0.130 of a 170-unit
    //cell the towers are 13-22 units of radius against 60 of height, i.e. two to three times taller than
    //wide, and with the flat crown the old profile drew they photographed as MESAS - buttes of rock
    //standing on a snowfield. A thunderhead is about as wide as it is tall. The range is up again, and the
    //ceiling is set by the jitter box rather than by taste: `margin` below is radius * 1.55 * AnvilSpread,
    //and a margin near 0.45 pins every turret at its own cell's centre, which draws the lattice this
    //deliberately breaks. At 0.155 the margin is 0.35, leaving 0.30 of a cell (about +-25 units) of jitter.
    float radius = lerp(0.100, 0.155, rollB.x);

    //The full reach, anvil included - what the margin must be, or a spreading top crosses into a cell
    //nobody reads and is cut off along a straight line. Clamped so a widened range can never make the
    //jitter box negative (which would break the single-cell read silently rather than loudly).
    float margin = min(radius * ANVIL_REACH * AnvilSpread, 0.45);
    float2 centre = margin + rollC * (1.0 - 2.0 * margin);

    //THE CLEARING IS DECIDED PER TURRET, from its own centre, never per pixel: per pixel the ramp is a
    //radial gradient sliced across the tower, which draws it half sunk on one side. The outback's rule.
    float2 centreWorld = (cellId + centre) * cellSize;
    float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(centreWorld));

    if (ramp <= 0.0) return 0.0;

    //The turret leans downwind as it climbs, which is what an anvil does. Taken on the cell's own bearing
    //so no two lean identically.
    float2 local = f - centre;
    local = RotateInto(local, RollDirection(rollD));

    float d = length(local) / radius;

    //Past the anvil's reach this turret contributes nothing.
    if (d >= ANVIL_REACH * AnvilSpread) return 0.0;

    //THE PROFILE, in two pieces that must not be confused — the first build multiplied the BODY's own reach
    //by AnvilSpread and drew a dune: the tower fell to nothing only at 2.25 radii, so it was wider than it
    //was tall whatever the height said.
    //
    //The BODY reaches zero at its own base line (d = 1).
    //
    //⚠ IT WAS A MESA AND THAT IS WHY THE SCENE READ AS LAND. The profile was `smoothstep(1.0, shoulder, d)`
    //— a smoothstep run backwards, which holds a FLAT crown everywhere inside `shoulder` (0.48-0.72 of the
    //radius) and then falls off hard. Flat top, steep wall, hard silhouette: that is the definition of a
    //butte, and photographed against the sky it is exactly what came out. The comment defending it argued
    //that the alternative was "the cone a plain falloff gives" — but the choice was never dome against
    //mesa, it is which of them carries the CAULIFLOWER, and the lump in StormHeight is what does that.
    //
    //A cumulus tower is BULBOUS: convex from the axis all the way out, widest up in its crown, rounding
    //over at the top. `(1 - d^2)` raised to a fraction is convex everywhere and flat nowhere; the exponent
    //is what says how much of the tower is crown, so it keeps the per-cell variation the shoulder had.
    float body = pow(saturate(1.0 - d * d), lerp(0.52, 0.86, rollA.y));

    //The ANVIL is a separate thing: a low, WIDE shelf lying out past the tower's own base, which is what
    //spreading at the tropopause looks like and the one cue that says weather rather than landscape. It is
    //the only part AnvilSpread widens, and it is a fraction of the height so it reads as a plate and not as
    //a skirt.
    float anvilEdge = ANVIL_REACH * AnvilSpread;
    float anvil = smoothstep(anvilEdge, 0.85, d) * 0.16;

    //The material mask reaches further than the height does, so the anvil's thin outer shelf is still cloud
    shape = saturate(body + anvil * 2.2);
    crest = saturate(body * 1.15);

    return (body * 0.94 + anvil) * height * lerp(0.62, 1.0, rollB.y) * ramp;
}

//The deck's own billow between the turrets - genuinely two octaves of gradient noise, not a sine pair (a
//sum of plane waves keeps its planes however many terms it has, which three scenes here learned the
//expensive way). Drifts downwind off the wall clock.
//⚠ REWRITTEN AFTER THE SCENE WAS REJECTED ON LOOKS. Two octaves of plain gradient noise is a SWELL, and a
//swell is what ground does: gradient noise is sinusoidal, so half of every period is saddle and the deck
//came out as a near-flat plain with a gentle roll in it — a snowfield, with the turrets standing on it as
//buttes. What a cloud deck looks like from an aeroplane is a CARPET OF ROUNDED LOBES, edge to edge, at
//several sizes at once, with creases between them; there is no flat anywhere in it.
//
//⚠ `|n|` AND NOT `1 - |n|`, AND THAT ONE CHARACTER IS THE WHOLE DIFFERENCE BETWEEN CLOUD AND MOUNTAIN.
//Both fold the field about zero; what they disagree about is WHERE the result peaks.
//
//  `1 - |n|` peaks along the noise's ZERO CONTOURS, and a zero contour of a smooth 2-D field is a LINE.
//            So it draws ridges running across the map — which is exactly what it is for: it is the
//            standard ridged-multifractal terrain primitive, the thing mountain generators are built on.
//  `|n|`     peaks at the noise's own EXTREMA, and those are POINTS. So it draws rounded blobs separated
//            by creased troughs, which is a cumulus field: puffs with dark crevices between them.
//
//This was written the wrong way round first and the deck photographed as crumpled ice ridges — sharper and
//more obviously rock than the smooth version it replaced. If this scene ever starts reading as terrain
//again, look here before looking anywhere else. The 1.42 takes gradient noise's own ~±0.7 out to ±1 first,
//so each octave spans a full 0..1.
//
//FOUR octaves, and the finest is set by the mesh rather than by eye: the deck is a 360-square grid over
//1400 units, so a cell is 3.9 units and nothing under about 8 can be carried by the vertices at all. The
//0.115 octave sits at 8.7 units, right on that floor; anything finer belongs in the pixel normal, which is
//what `relief` in the pixel shader already is.
float DeckBillow(float2 p)
{
    float2 drift = WindDirection * (StormTime * DriftSpeed);
    float2 q = p - drift;

    float a = abs(GradientNoise2(q * 0.017) * 1.42);
    float b = abs(GradientNoise2(q * 0.030 + 5.7) * 1.42);
    float c = abs(GradientNoise2(q * 0.068 + 11.3) * 1.42);
    float d = abs(GradientNoise2(q * 0.120 + 23.9) * 1.42);

    //Weighted hard towards the big lobes on purpose. An even spectrum came out as chop — a rough, busy
    //surface that reads as shingle or as a broken sea, because at this grazing an angle the small octaves
    //are what the eye lands on. A cloud carpet is a few big soft mounds with smaller ones ON them, so the
    //coarse octave owns the silhouette and the rest only inflect it.
    float carpet = a * 0.50 + b * 0.28 + c * 0.15 + d * 0.07;

    //⚠ AND THIS IS WHAT SEPARATES CLOUD FROM SNOWFIELD. A folded sum is still a CONTINUOUS SHEET — every
    //point sits somewhere on one surface — and it photographed as a glacier: smooth, white, unbroken, soft
    //mounds in it. What a deck looks like from above is discrete CELLS: rounded puffs standing clear of a
    //lower floor, with gaps and shadow between them. A power curve on the folded field does exactly that
    //for one instruction — it leaves the crowns where they are and presses everything under them down and
    //outward, so the lobes narrow into separate puffs and the troughs widen into the floor between them.
    //Under about 1.2 it is a smooth carpet again; much over 1.6 the puffs pull apart into isolated blobs.
    carpet = pow(saturate(carpet), 1.35);

    //Back to roughly zero mean, which is not tidiness: StormHeight multiplies this by a radial ramp, and
    //ramping a field with a mean in it lifts the mean with the ramp and draws a shallow bowl around the
    //island — the desert's trailing-constant trap, and the reason the old field was left unramped instead.
    //The constant is this curve's own mean and is approximate: |n| averages about a third, four octaves
    //averaged together keep that mean while narrowing the spread, and the power curve pulls it to ~0.22.
    return carpet - 0.22;
}

//The full displaced deck height at a world point: flat at StormLevelY inside the clearing, rising into
//billow with distance, with the turrets standing out of it. Tapped to displace the vertex (VS) and, thrice,
//for the per-pixel normal (PS).
//
//The billow is NOT gated by the clearing and the turrets are, which is what keeps the mean honest: a field
//with a mean multiplied by a radial ramp rises with the ramp and draws a shallow bowl with the island at
//the bottom of it (the trap the desert's dune sum carries a trailing constant to avoid). The one field with
//a big mean here is the turrets', and their ramp is per turret rather than per pixel.
float StormHeight(float2 p, out float shape, out float crest)
{
    float billow = DeckBillow(p);

    float turretShape, turretCrest;
    float turrets = TurretLayer(p, max(TurretSpacing, 1.0), 41.3, TurretChance, TurretHeight,
        turretShape, turretCrest);

    shape = turretShape;
    crest = turretCrest;

    //THE LUMP, and it is what makes a turret cloud rather than a cone. A smooth profile lit by one sun is a
    //mountain whatever colour it is painted — a cumulonimbus reads from its CAULIFLOWER silhouette, so the
    //irregularity has to be in the HEIGHT FIELD where it reaches the edge against the sky, not in the normal
    //where it is only shading. (The outback learned the same thing about its gullies the expensive way: a
    //per-pixel field driving the normal drew terraces and could never touch the silhouette.)
    //
    //⚠ ITS FREQUENCY WAS THE FAULT, not its strength, and that is why turning it up would never have helped.
    //The octaves ran at 33 and 12 unit features against a turret 26-44 units WIDE — so the coarse one was
    //wider than the tower it was meant to break up and merely tilted the whole thing, and the fine one was
    //the only lobing there was. The silhouette therefore stayed the profile's own smooth shoulder, which is
    //the read the owner rejected. A cumulus lobe is a fraction of its tower: these are 18 and 8.7 units
    //against a tower now 34-53 wide, so three to six lobes cross it. Folded like the deck's, so they bulge
    //rather than ripple. 8.7 is the mesh's own floor (see DeckBillow) and nothing may go under it here —
    //this field displaces VERTICES, so a finer octave aliases in the silhouette rather than adding to it.
    //`|n|` and not `1 - |n|`, for the reason DeckBillow's header spells out: inverted, this drew RIDGES
    //down the towers and gave them jagged rocky crowns.
    float lump = abs(GradientNoise2(p * 0.055 + 13.1) * 1.42) * 0.62
               + abs(GradientNoise2(p * 0.115 + 31.7) * 1.42) * 0.38
               - 0.33;

    //The billow is ramped out of the clearing now. It is zero-mean by construction, so the ramp changes its
    //AMPLITUDE and not its level, and the deck beside the island stays exactly at StormLevelY — which costs
    //nothing that can be seen, because a surface at the deck's own level is occluded by the island's disc
    //past the far plane anyway (the header's measurement). What it buys is the freedom to give the far
    //carpet real relief without standing a wall of cloud next to the arena.
    float swell = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, length(p));

    return StormLevelY + BillowHeight * billow * swell + turrets
        + lump * turretShape * TurretHeight * 0.34;
}

struct StormVertexInput
{
    float4 Position : POSITION0;
};

struct StormVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

StormVertexOutput StormVS(StormVertexInput input)
{
    StormVertexOutput output;

    float2 worldXZ = input.Position.xz + OriginXZ;

    float shapeUnused, crestUnused;
    float3 worldPosition = float3(worldXZ.x, StormHeight(worldXZ, shapeUnused, crestUnused), worldXZ.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//Tangent-free normal tilt from a height field (Christian Schueler), the same one every terrain scene here
//uses - the grid carries no tangents and the fine relief never reaches it anyway.
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
    float3 dpdx = ddx(worldPosition);
    float3 dpdy = ddy(worldPosition);

    float3 r1 = cross(dpdy, normal);
    float3 r2 = cross(normal, dpdx);

    float determinant = dot(dpdx, r1);
    float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

    return normalize(abs(determinant) * normal - surfaceGradient);
}

float4 StormPS(StormVertexOutput input) : COLOR
{
    float3 worldPosition = input.WorldPosition;

    //Cut the island's footprint out of the deck (see IslandHoleRadius). 0 in the map editor keeps it all.
    clip(length(worldPosition.xz) - IslandHoleRadius);

    float dist = distance(CameraPosition, worldPosition);
    float footprint = length(fwidth(worldPosition.xz));

    //The base normal, taken PER PIXEL from the height field's own gradient (three taps) rather than
    //interpolated from a per-vertex normal - every terrain scene's rule, and it matters more here than
    //anywhere because a turret's flank falls tens of units over a handful of cells.
    float e = 2.0;
    float shape, crest, shapeX, crestX, shapeZ, crestZ;
    float h = StormHeight(worldPosition.xz, shape, crest);
    float hx = StormHeight(worldPosition.xz + float2(e, 0.0), shapeX, crestX);
    float hz = StormHeight(worldPosition.xz + float2(0.0, e), shapeZ, crestZ);

    float2 slope = float2(hx - h, hz - h) / e;
    float3 baseNormal = normalize(float3(-slope.x, 1.0, -slope.y));

    //--- The cloud's own surface -----------------------------------------------------------------------
    //Billowed relief, band-limited against the footprint and drifting with the deck, so a turret's flank is
    //lumpy cloud rather than an airbrushed cone. Cloud has no hard edges anywhere, which is why this is a
    //normal tilt and never a colour step.
    //⚠ THIS WAS MOST OF THE ROCK. Four octaves from an 18-unit base is a 2-unit finest grain, and at
    //BillowRelief 3.4 it pebbled the whole deck with sharp little shadowed pits — stucco, or shingle, and
    //against a white surface the eye reads that as stone every time. Cloud has no grain at that size: what
    //it has is the next size of lobe down, which is now in the HEIGHT field where it can reach the
    //silhouette. So this is coarser (36-unit base, so the finest octave lands near the mesh's own 8-unit
    //floor rather than far under it) and much gentler — a softening of the shading, not a texture on it.
    float2 drift = WindDirection * (StormTime * DriftSpeed);
    float relief = Fbm2BandLimited((worldPosition.xz - drift) * 0.028, 4, footprint * 0.028);

    float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief * BillowRelief);

    //--- Colour ---------------------------------------------------------------------------------------
    //The sunlit top against the shaded flank, split by how UP-FACING the surface is rather than by height:
    //a storm's whites are where the sun lands, and on an anvil that is the top face while the flank a metre
    //away is nearly black. Then the deck's own base colour is mixed in low down, where no sun reaches at
    //all. Both splits are WIDE, for the reason the sky's own deck records: ACES eats cloud contrast, so two
    //linear values close together in the highlights tonemap to the same white.
    float upFacing = saturate(normal.y);
    float3 cloud = lerp(ShadeColor, TopColor, upFacing * upFacing);

    //Low in the deck between the towers, towards the base's blue-grey. `crest` is 0 on the deck and 1 at a
    //turret's crown, so this only reaches the floor of the scene.
    float depth = saturate(1.0 - (h - StormLevelY) / max(BillowHeight * 2.0, 1e-3));
    cloud = lerp(cloud, BaseColor, depth * (1.0 - crest) * 0.75);

    //--- Lighting -------------------------------------------------------------------------------------
    //No cloud shadow term anywhere in this shader, deliberately: the deck IS the cloud, and casting the
    //sky's field onto it would darken it by its own shape (see the header's point 3).
    float ndotl = saturate(dot(normal, SunDirection));

    //Hemisphere sky light: up-facing cloud takes the zenith, flanks turned to the skyline take the horizon
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

    float3 color = cloud * (skyAmbient * AmbientStrength + SunColor * ndotl);

    //THE SILVER LINING. Cloud is strongly forward-scattering, so its rim against the sun is its brightest
    //part - the single cue that separates a cloud from a hill of grey stone, and the sky's own deck carries
    //the same term for the same reason. Rides a grazing angle so it lands on the silhouette, and is gated
    //on the sun actually being behind the rim.
    float3 towardsEye = normalize(CameraPosition - worldPosition);
    float grazing = 1.0 - saturate(dot(normal, towardsEye));
    float towardsSun = saturate(dot(-towardsEye, SunDirection));

    color += TopColor * SunColor * (SilverStrength * pow(grazing, 2.5) * pow(towardsSun, 3.0));

    //--- THE FLASH ------------------------------------------------------------------------------------
    //Lightning lights the cloud FROM INSIDE, which is what it actually looks like from above a deck: a
    //whole cell goes translucent for a beat. So this is an emissive term gated on distance from the
    //strike's own cell, not a light with a normal - a normal-lit flash reads as a second sun.
    //
    //Behind a [branch] on a uniform: non-divergent, and there is no gradient operation inside, so it is
    //derivative-safe (BestPractices.md's rule for scene-gated shader work). Most frames skip it entirely.
    [branch]
    if (FlashEnvelope > 0.0)
    {
        float toStrike = length(worldPosition.xz - FlashCenterXZ);
        float near = saturate(1.0 - toStrike / max(FlashReach, 1e-3));

        //Squared, so the glow is a cell lighting up rather than the whole deck brightening: the falloff has
        //to be steeper than the eye's, or a strike two hundred units away still washes the foreground.
        //
        //GATED ON THE SHADED SIDE, and that is the difference between lightning and an exposure change. A
        //discharge lights the cloud FROM WITHIN, so what it reveals is the part the sun is not already
        //lighting — the flanks, the hollows and the undersides of the billow. Added flat, it blew the near
        //deck to featureless white at any strength that could be SEEN at all (measured across three
        //passes: 2.6 moved a deck mean by 3 of 255 and was invisible; 14 washed the whole foreground and
        //took the cloud's form with it). Weighted this way the same energy lands where there is contrast to
        //spend it on, so a strike reads as cloud lighting up rather than as the frame brightening.
        float shaded = 0.30 + 0.70 * (1.0 - ndotl);

        color += FlashColor * (FlashDeckGlow * FlashEnvelope * near * near * shaded);
    }

    //--- The air --------------------------------------------------------------------------------------
    //TWO STAGES, the outback's arrangement, and one colour cannot do both jobs (see HazeTint). The
    //mid-distance keeps the scene's own cool cloud-white, or the deck takes whatever hue the dome's horizon
    //happens to be and a sandy-horizoned dome hands back a desert. The last stretch has to arrive at the
    //dome's exact HorizonColor, or the finite grid's own edge shows as a seam against a sky it does not
    //match. So: a quadratic into the scene's tint, then a quartic that only bites in the final fifth.
    //
    //The tint takes the sky's BRIGHTNESS in full and only half of its hue — the outback's own correction,
    //because lighting the tint by the sky's colour outright (`HazeTint * skyLight`, the obvious spelling)
    //keeps whichever channel the dome runs strongest in, and a multiply cannot be out-weighted by a blend
    //whose other end carries the same hue.
    float3 skyLight = HorizonColor + SunColor * 0.35;
    float skyLuminance = dot(skyLight, float3(0.2126, 0.7152, 0.0722));

    float3 hazeLit = HazeTint * lerp(skyLuminance.xxx, skyLight, 0.45);

    float haze = saturate(dist / HorizonHazeDistance);

    color = lerp(color, hazeLit, HazeStrength * haze * haze);
    color = lerp(color, HorizonColor, haze * haze * haze * haze);

    return float4(color, 1.0);
}

technique Storm
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL StormVS();
        PixelShader = compile PS_SHADERMODEL StormPS();
    }
};
