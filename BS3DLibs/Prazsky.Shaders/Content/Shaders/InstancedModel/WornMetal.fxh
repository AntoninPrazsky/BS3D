//InstancedModel.fx: the gun's metal and the drain's laser emitters worn by use (#1199) - the plain lit material with the
//flaws a steel object that is handled, loaded and left out in the weather carries, the way #640 gave the ceiling's glass,
//the drain and the crystal cup theirs. A material that renders perfect reads as CG; the owner's QA notes asked for "a few
//imperfections: scratches, oxidation of the metal, slight grime" on the cannon and the metal lamps, on High and Ultra.
//
//Five flaws, all in the MESH'S OWN SPACE so they ride the part they are on (the barrel elevating, a wheel rolling, a
//housing turning to its corner) and never swim over it, and none of them a texture:
//
// * a PATINA'S MOTTLE: no sheet of steel is one tone; a low noise moves the albedo and the sheen a little either way;
// * OXIDATION: matt rust in sparse patches, gathering on the underside where water stands, and in every inside corner
//   of a turned profile (where a ring meets the tube) - the reference images' brown line in each join. A patch has
//   structure at three scales (#1244: one soft field in one flat tint read as mud): its shape, a ragged, speckled rim
//   where it thins out, and a grain inside it, a thin warm film round a darker crusted core;
// * GRIME: those inside corners darker still, oil and dust that a rag never reaches;
// * RUBBED EDGES: the outside corners of the profile (a ring's crest, the muzzle's lip, a hub's chamfer) worn back to
//   bright metal in places, where hands, rounds and cloths pass;
// * SCRATCHES AND PITS: fine straight scratches - along the use where a part has one (down the bore, round a wheel's
//   machined face), every way where it has none - and a sprinkle of tiny pits, more of them in the tarnish.
//
//The inside and outside corners are the profile's own (InstancedModelRenderer.Wear, Prazsky.Core.Render.MetalWear):
//every worn part but the carriage's plates and the yoke is a solid of revolution, so a corner is a point of its profile
//and the distance to it is two floats. A screen-space curvature cannot find them: a crease is two triangles with a normal
//each, and derivatives never cross a triangle's edge.
//
//Subtle by design and band-limited: every mark fades by the share of a pixel it fills, so a gun far off on the menu's
//orbit loses its scratches and pits rather than crawling with them, and nothing is ever drawn thinner than a pixel. The
//muzzle collar (its colour is a statement, #425) and the rollers' rubber are not drawn through this.
//
//Its uniforms are the LAST in the effect (this file is included after CityReflection.fxh), so no other program's
//constant buffer moves (the include order note in InstancedModel.fx).
//
//COST: about 1,530 instruction slots against MainPS's 507 (1,280 before #1244's rim and grain), most of it the five gradient noises (one for the mottle,
//which the rubbed edges' patchiness reuses, two octaves for the tarnish's field, and its rim and its grain since #1244)
//and the corner loop. What it costs in a frame: "The metal's wear on High and Ultra" in docs/testbed/cannon.md.

//How much of each flaw a part carries, 0 none to 1 the full figure below: x the use (scratches and rubbed edges),
//y the oxidation, z the grime, w the patina's mottle.
float4 WearAmount;

//The way the scratches run, in the mesh's own space (unit): along the use, the bore for the barrel. Zero scatters them
//every way. w = 1 turns them round WearProfile's axis instead, on the faces that look along it: a machined face's rings.
float4 WearGrain;

//The profile the corners below lie on: xyz the axis the part is turned about, through the mesh's origin (unit); w 0 for
//a part with no profile (no corners are read), 1 for one, 2 for a pair mirrored across the origin (a wheel's two faces).
float4 WearProfile;

//The profile's corners, two to a slot: (axial, radial, axial, radial), the radial's SIGN saying which kind - positive an
//outside corner (an edge, rubbed bright), negative an inside one (a join, tarnished and grimed). A radial of 0 is an empty
//place. Read at constant indices in an unrolled loop, so it is one array and never addressed relatively (GL, docs/rendering/opengl-es.md).
#define WEAR_CORNER_SLOTS 8
float4 WearCorners[WEAR_CORNER_SLOTS];

//How far the corners' wear reaches, in world units: x from an inside corner (the tarnish and grime), y from an outside
//one (the rub). Per part, because a corner's wear is as wide as the step it is on: the barrel's rings stand 0.055 proud
//and their joins' dirt lies well out onto the tube in front of them, not only up the ring's own face, which the light
//rarely reaches; a wheel's hub face is a few hundredths high and a band that wide would cover the whole plate.
float4 WearBands;

//The patina, one octave of gradient noise: cycles per world unit, and how far it moves the albedo and the reflected sky
//either way at full amount
static const float WEAR_MOTTLE_SCALE = 1.6;
static const float WEAR_MOTTLE_ALBEDO = 0.3;
static const float WEAR_MOTTLE_SHEEN = 0.45;

//The tarnish: its field's frequency (two octaves), where the field rises into a patch (it rarely leaves +-0.4), and how
//much nearer a downward face is to one. A patch's edge is narrower than its field's wander, so it reads as an edge and
//not a stain's soft halo, and RAGGED of a finer noise (RIM_SCALE cycles a unit) moves it about, so it is torn, not round
static const float WEAR_RUST_SCALE = 1.25;
static const float2 WEAR_RUST_PATCH = float2(0.14, 0.22);
static const float WEAR_RUST_SHELTER = 0.22;
static const float WEAR_RUST_PATCH_SHARE = 0.85;
static const float WEAR_RUST_RIM_SCALE = 7.0;
static const float WEAR_RUST_RAGGED = 0.09;

//Inside a patch: a grain (GRAIN_SCALE cycles a unit) that breaks up the thin rim into specks and moves the tone, and the
//two tints a pixel's albedo takes (linear, multipliers on the steel's): a thin oxide FILM where the patch is young, warm
//and still showing the metal, and the crusted CORE, dark reddish brown, where the field stands well past the edge
static const float WEAR_RUST_GRAIN_SCALE = 26.0;
static const float WEAR_RUST_GRAIN_TONE = 0.35;
static const float WEAR_RUST_SPECKLE = 2.2;
static const float2 WEAR_RUST_CORE = float2(0.24, 0.36);
static const float3 WEAR_RUST_FILM_TINT = float3(0.95, 0.52, 0.26);
static const float3 WEAR_RUST_CORE_TINT = float3(0.46, 0.25, 0.14);

//The grime: how dark it takes the albedo at full, and how much of it the underside carries away from any corner
static const float WEAR_GRIME_DARK = 0.45;
static const float WEAR_GRIME_UNDERSIDE = 0.3;

//The scratches. Cells along and across the grain (world units), the share of them that hold one, how far one turns off
//the grain (radians), and the second, finer layer's scale and share. Every way, the cells are square and the turn full.
static const float2 WEAR_SCRATCH_CELL_GRAIN = float2(1.1, 0.14);
static const float2 WEAR_SCRATCH_CELL_ANY = float2(0.36, 0.36);
static const float WEAR_SCRATCH_SPREAD_GRAIN = 0.10;
//The shares were 0.5 and 0.35 until the owner asked for clearly fewer (#1244): the shaded flank read covered in them
static const float WEAR_SCRATCH_CHANCE = 0.22;
static const float WEAR_SCRATCH_FINE_SCALE = 0.6;
static const float WEAR_SCRATCH_FINE_CHANCE = 0.12;
static const float WEAR_SCRATCH_WIDTH = 0.006;

//What a scratch does to the light at full. A scratch is a rough groove: it scatters what reaches it every way, so it
//shows roughly the AVERAGE of the light round it - the sky and a share of the sun - where the satin steel beside it
//shows the light from one direction. In daylight that average reads lighter than the steel, in the shade most. Laid as
//a lighter albedo instead (the first cut of this), the scratches were all but lost on the sunlit barrel, whose lit side
//sits in the tonemap's shoulder. TONE is how far a scratched pixel goes to that average, SUN the sun's share in it,
//GLINT how much more of the lamp it catches.
static const float WEAR_SCRATCH_TONE = 0.85;
static const float WEAR_SCRATCH_SUN = 0.35;
static const float WEAR_SCRATCH_GLINT = 1.5;

//And a rubbed edge: bare, polished metal, a little lighter and mirroring more of the lamp and the sky
static const float WEAR_EDGE_LIFT = 0.30;
static const float WEAR_EDGE_GLINT = 1.0;
static const float WEAR_EDGE_SHEEN = 0.8;

//The pits: their 3D lattice (world units), the share of cells holding one away from the tarnish and inside it, their
//radius as a share of the cell, and how dark they go
static const float WEAR_PIT_CELL = 0.05;
static const float2 WEAR_PIT_CHANCE = float2(0.035, 0.22);
static const float WEAR_PIT_RADIUS = 0.2;
static const float WEAR_PIT_DARK = 0.55;

struct WornVSOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 LocalPosition : TEXCOORD3;
    float3 LocalNormal : TEXCOORD4;
};

WornVSOutput WornVS(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderOutput main = MainVS(input, instance);

    WornVSOutput output;
    output.Position = main.Position;
    output.WorldPosition = main.WorldPosition;
    output.WorldNormal = main.WorldNormal;
    output.OcclusionData = main.OcclusionData;
    output.LocalPosition = mul(input.Position, Bone).xyz;
    output.LocalNormal = mul(float4(input.Normal, 0), Bone).xyz;

    return output;
}

//One layer of scratches over a 2D field laid in the grain's frame (x along it): each cell holds at most one straight
//scratch, turned off the grain by up to `spread` and sized and placed to stay inside its cell, so the cell a point is in
//is the only one asked. `footprint` is a pixel in the field's units: a scratch is drawn at least a pixel wide and fades by
//the share of the pixel it really covers, so far away it becomes a faint haze and never a crawling line.
float WearScratchLayer(float2 q, float2 cell, float spread, float chance, float footprint, float salt)
{
    float2 id = floor(q / cell);
    float2 rollA = NoiseHash22(id + salt) * 0.5 + 0.5;
    float2 rollB = NoiseHash22(id + salt + 41.3) * 0.5 + 0.5;

    float angle = (rollA.x - 0.5) * spread;
    float2 dir = float2(cos(angle), sin(angle));

    //Its half length, then the room that leaves it to move about the cell's centre in
    float halfLength = lerp(0.18, 0.46, rollA.y) * min(cell.x, spread > 3.0 ? cell.y : cell.x);
    float2 reach = abs(dir) * halfLength;
    float2 room = max(cell * 0.5 - reach - WEAR_SCRATCH_WIDTH, 0.0);
    float2 centre = (id + 0.5) * cell + (rollB - 0.5) * 2.0 * room;

    float2 o = q - centre;
    float along = dot(o, dir);
    float across = abs(o.x * dir.y - o.y * dir.x);

    float drawn = max(WEAR_SCRATCH_WIDTH, footprint);
    float stroke = saturate(1.0 - across / (0.5 * drawn)) * (WEAR_SCRATCH_WIDTH / drawn);

    //Tapered at both ends, its own depth, and present in `chance` of the cells
    float ends = saturate((halfLength - abs(along)) / (0.3 * halfLength));
    float depth = 0.35 + 0.65 * frac(rollA.x * 17.31 + rollB.y * 5.17);
    float present = step(frac(rollA.y * 13.73 + rollB.x * 7.91), chance);

    return stroke * ends * depth * present;
}

float4 WornMetalPS(WornVSOutput input) : COLOR
{
    float3 p = input.LocalPosition;
    float3 n = normalize(input.LocalNormal);

    //A pixel's size on the part, in world units: what every mark below is band-limited against. Outside any branch.
    float footprint = max(length(ddx(p)), length(ddy(p)));

    //--- The scratches' field: the face's own plane, laid along the grain -------------------------------------------
    float3 an = abs(n);
    bool onX = an.x >= max(an.y, an.z);
    bool onY = !onX && an.y >= an.z;
    float2 uv = onX ? p.yz : onY ? p.xz : p.xy;
    float2 grain = onX ? WearGrain.yz : onY ? WearGrain.xz : WearGrain.xy;

    //Round the profile's axis, on a face that looks along it (a machined disc): arc length along, radius across
    float3 axis = WearProfile.xyz;
    float axial = dot(p, axis);
    float3 radialVector = p - axis * axial;
    float radial = length(radialVector);
    float3 basis1 = normalize(cross(axis, abs(axis.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
    float3 basis2 = cross(axis, basis1);
    bool concentric = WearGrain.w > 0.5 && abs(dot(n, axis)) > 0.7;

    float grained = concentric || dot(grain, grain) > 0.25 ? 1.0 : 0.0;
    float2 g = grain / max(length(grain), 1e-4);
    float2 q = concentric ? float2(atan2(dot(p, basis2), dot(p, basis1)) * radial, radial)
        : grained > 0.5 ? float2(dot(uv, g), dot(uv, float2(-g.y, g.x)))
        : uv;

    //Taken here, on the field actually used, so a pixel whose quad straddles two faces' planes reads a huge footprint
    //and its scratch fades rather than tearing
    float scratchFootprint = max(fwidth(q.x), fwidth(q.y));

    //--- The profile's corners ------------------------------------------------------------------------------------
    //The nearest of each kind, compared squared and rooted once
    float joinDistance = 1e3;
    float edgeDistance = 1e3;

    [branch]
    if (WearProfile.w > 0.5)
    {
        float2 at = float2(WearProfile.w > 1.5 ? abs(axial) : axial, radial);
        float join2 = 1e6, edge2 = 1e6;

        [unroll]
        for (int i = 0; i < WEAR_CORNER_SLOTS; i++)
        {
            float4 c = WearCorners[i];
            float2 t0 = at - float2(c.x, abs(c.y));
            float2 t1 = at - float2(c.z, abs(c.w));
            float d0 = dot(t0, t0), d1 = dot(t1, t1);

            join2 = min(join2, c.y < 0.0 ? d0 : 1e6);
            edge2 = min(edge2, c.y > 0.0 ? d0 : 1e6);
            join2 = min(join2, c.w < 0.0 ? d1 : 1e6);
            edge2 = min(edge2, c.w > 0.0 ? d1 : 1e6);
        }

        joinDistance = sqrt(join2);
        edgeDistance = sqrt(edge2);
    }

    //Each band at least a pixel and a half wide, fading by the share of it the band really fills
    float joinDrawn = max(WearBands.x, footprint * 1.5);
    float edgeDrawn = max(WearBands.y, footprint * 1.5);
    float joinBand = (1.0 - smoothstep(0.0, joinDrawn, joinDistance)) * (WearBands.x / joinDrawn);
    float edgeBand = (1.0 - smoothstep(0.0, edgeDrawn, edgeDistance)) * (WearBands.y / edgeDrawn);

    //--- The fields -------------------------------------------------------------------------------------------------
    float mottle = 0.6 * GradientNoise3(p * WEAR_MOTTLE_SCALE);
    float field = Fbm3(p * WEAR_RUST_SCALE + 17.0, 2);
    float shelter = saturate(-n.y);

    //The tarnish's two finer scales (#1244), each faded out by the share of a pixel its features would fill (gone where a
    //period is two pixels, whole from six), so a gun far off loses the grain and keeps the patch rather than crawling
    float rim = GradientNoise3(p * WEAR_RUST_RIM_SCALE + 3.1) * saturate(1.5 - 3.0 * WEAR_RUST_RIM_SCALE * footprint);
    float rustGrain = GradientNoise3(p * WEAR_RUST_GRAIN_SCALE + 9.7) * saturate(1.5 - 3.0 * WEAR_RUST_GRAIN_SCALE * footprint);

    //The tarnish: sparse patches, nearer on the underside, their edge torn by the rim's noise and, where a patch thins out,
    //broken into specks by the grain; and a line in every inside corner broken where the field is low
    float shaped = field + WEAR_RUST_SHELTER * shelter + WEAR_RUST_RAGGED * rim;
    float patch = smoothstep(WEAR_RUST_PATCH.x, WEAR_RUST_PATCH.y, shaped);
    patch *= lerp(1.0, saturate(0.5 + WEAR_RUST_SPECKLE * rustGrain), 4.0 * patch * (1.0 - patch));
    float joinPatchy = saturate(field * 1.6 + 0.65);
    float rust = saturate(patch * WEAR_RUST_PATCH_SHARE + joinBand * joinPatchy) * WearAmount.y;

    //Its crusted core, where the field stands well past the edge, and its grain's tone through all of it
    float core = smoothstep(WEAR_RUST_CORE.x, WEAR_RUST_CORE.y, shaped + 0.5 * WEAR_RUST_RAGGED * rustGrain) * rust;
    float3 rustTint = lerp(WEAR_RUST_FILM_TINT, WEAR_RUST_CORE_TINT, core) * (1.0 + WEAR_RUST_GRAIN_TONE * rustGrain);

    //The grime: the inside corners, and a little on the underside where the field is high
    float grime = saturate(joinBand * 0.8 + shelter * WEAR_GRIME_UNDERSIDE * saturate(field * 2.0 + 0.5)) * WearAmount.z;

    //The rub: the outside corners worn back in places, not as an even ring
    float edge = edgeBand * saturate(0.75 + 2.5 * mottle) * WearAmount.x;

    //The scratches, fewer on the underside, where nothing is handled
    float scratch = WearScratchLayer(q, grained > 0.5 ? WEAR_SCRATCH_CELL_GRAIN : WEAR_SCRATCH_CELL_ANY,
        grained > 0.5 ? WEAR_SCRATCH_SPREAD_GRAIN : 6.2832, WEAR_SCRATCH_CHANCE, scratchFootprint, 3.7);
    scratch += 0.7 * WearScratchLayer(q + 0.37, (grained > 0.5 ? WEAR_SCRATCH_CELL_GRAIN : WEAR_SCRATCH_CELL_ANY) * WEAR_SCRATCH_FINE_SCALE,
        grained > 0.5 ? WEAR_SCRATCH_SPREAD_GRAIN * 2.0 : 6.2832, WEAR_SCRATCH_FINE_CHANCE, scratchFootprint, 11.9);
    scratch = saturate(scratch) * lerp(1.0, 0.45, shelter) * (1.0 - 0.7 * rust) * WearAmount.x;

    //The pits: a dot where the surface passes a pit of the lattice, more of them in the tarnish
    float3 cellId = floor(p / WEAR_PIT_CELL);
    float3 roll = NoiseHash33(cellId + 5.3) * 0.5 + 0.5;
    float pitAt = length(frac(p / WEAR_PIT_CELL) - (0.3 + 0.4 * roll)) * WEAR_PIT_CELL;
    float pitRadius = WEAR_PIT_RADIUS * WEAR_PIT_CELL;
    float pitDrawn = max(pitRadius, footprint);
    float pit = step(roll.x, lerp(WEAR_PIT_CHANCE.x, WEAR_PIT_CHANCE.y, saturate(rust * 2.0)))
        * saturate((pitDrawn - pitAt) / (0.5 * pitDrawn)) * (pitRadius / pitDrawn) * saturate(WearAmount.y + WearAmount.z);

    //--- What it does to the light ----------------------------------------------------------------------------------
    float3 albedo = (1.0 + WEAR_MOTTLE_ALBEDO * mottle * WearAmount.w) * lerp(1.0, rustTint, rust)
        * (1.0 - WEAR_GRIME_DARK * grime) * (1.0 - WEAR_PIT_DARK * pit) + WEAR_EDGE_LIFT * edge;

    SurfaceSpecular surface;
    surface.Highlight = (1.0 + WEAR_SCRATCH_GLINT * scratch + WEAR_EDGE_GLINT * edge) * (1.0 - 0.75 * rust) * (1.0 - 0.4 * grime);
    surface.Environment = (1.0 + WEAR_MOTTLE_SHEEN * mottle * WearAmount.w + WEAR_EDGE_SHEEN * edge)
        * (1.0 - 0.6 * rust) * (1.0 - 0.3 * grime);
    surface.Smoothness = saturate(1.0 - 0.55 * rust - 0.2 * core - 0.25 * grime);

    float4 shaded = ShadePixel(input.WorldPosition, input.WorldNormal, input.OcclusionData, float4(albedo, 1), 1, 1, surface);

    //The scratches: the light's average round the groove, the sky's two halves and a share of the sun (see WEAR_SCRATCH_TONE)
    float3 average = SrgbToLinear(DiffuseColor.rgb) * albedo
        * (0.5 * (SkyColor + GroundColor) + WEAR_SCRATCH_SUN * DirLight0DiffuseColor * DirLightStrength);
    shaded.rgb = lerp(shaded.rgb, average, WEAR_SCRATCH_TONE * scratch);

    //The sea's submerge fade, as MainPS takes it: the emitters stand in the drain the sea's pool fills
    return ApplySeaSubmerge(shaded, input.WorldPosition);
}

technique InstancedWornMetal
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL WornVS();
        PixelShader = compile PS_SHADERMODEL WornMetalPS();
    }
};
