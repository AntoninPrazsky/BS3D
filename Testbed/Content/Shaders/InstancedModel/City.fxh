//InstancedModel.fx: the city's towers (InstancedCity) - windows laid out per building from world position,
//the facade's plaster and grain, the lamps switching on and off, and the neon city's signs.

//Window layout and look. These are uniforms now, set on every city draw from CitySceneConfig (whose
//defaults reproduce the values that used to be hard-coded here). Only the city technique reads them, so
//leaving them zero for the ball and scene draws is harmless.
//Vertical/horizontal window spacing (world units) and how much of each cell is glass rather than wall:
float WindowPitchY;
float WindowPitchX;
float WindowFillY;
float WindowFillX;

//The raised plaster frame moulding around each pane. A pane cut straight into a flat wall reads as a
//hole in it, not as a window; a real window is set into a frame, and the frame is render carried proud
//of the surrounding wall. The moulding lives between the glass (withinCell < WindowFill) and its outer
//edge (WindowFill + WindowFrameWidth), so it is the same material as the wall -- it catches light and
//casts its own shadow -- rather than a second surface pasted on.
float WindowFrameWidth;
//How high the moulding stands proud of the wall, in world units (0 = no frame, just a flat pane).
float WindowFrameHeight;
//How much the moulding's own relief also SHADES -- the lit top of the bead and the shadow it throws on
//the wall just past it (0 = the normal is tilted and nothing else). The same lesson FacadeGrainShading
//already taught: a tilted normal alone is invisible at a tower's distance, so the bead's height also
//darkens and lightens the render's tone, which at this range is the stronger cue.
float WindowFrameShading;

//What makes a window read as a window from across the arena (#435), drawn from references rendered for it: a
//surround in a tone of its own, a sill under the pane with a lit top and a shadow on the wall below, the
//reveal's shadow along the top of the recessed glass, and glazing bars across the pane. All in world units
//except the tone, a multiple of the wall's own albedo, so the neon city's dark walls keep dark frames.
float WindowFrameTone;
float WindowSillHeight;
float WindowSillOverhang;
float WindowSillShadow;
float WindowSillShading;
float WindowRevealDepth;
float WindowBarWidth;

//Wall border kept clear of glass at every building edge (see CityPS): the grid is laid out per building
//with this margin held at every side, so no window is ever jammed into a corner and every tower matches.
float WindowMargin;

//Fraction of the windows that are lit, and the two colours they are lit with.
float WindowLitFraction;
float3 WindowWarm;
float3 WindowCool;

//How long a window holds one state before deciding again, how much that varies from window to window,
//and how much of an interval the switch itself takes.
float WindowHoldSeconds;
float WindowHoldVariation;
float WindowSwitchFade;

//How brightly the lit windows glow, and how dark the facade around them is
float CityWindowBrightness;

//Wall clock driving the windows switching on and off, in seconds
float CityWindowTime;

//0 = ordinary city, 1 = neon night city: near-black facades and vivid saturated signs, one hue per tower
float CityNeon;

//The plaster: what the wall between the windows is made of. Its albedo by day and under neon, how far that
//wanders from tower to tower, and the grain of the render itself.
//
//The albedo has to be a real one, and that is not a detail. NEITHER specular term is multiplied by albedo --
//a highlight does not care how dark the surface under it is -- so with a dark facade almost the whole
//brightness of a tower used to BE its white highlight and its grazing sky reflection. Take the shine off a
//dark wall and what is left is a black slab; the albedo comes up in the same change, and the light on the
//wall is then diffuse light, which is what plaster does with it.
float3 FacadeColor;
float3 FacadeNeonColor;
float FacadeColorVariation;

//Peak height of the render's grain in world units, and its base wave count per unit. What makes a wall read
//as plaster is not its BRDF but that the light plays over it: a flat box face takes the light as a flat box
//face however the reflectance is tuned.
float FacadeGrainStrength;
float FacadeGrainFrequency;

//How much that same grain also shades: the ambient it keeps out of its hollows, and the mottling of the
//render's own tone. Without it the grain is invisible at a tower's distance, where a few degrees of normal
//tilt move a matte surface by a percent.
float FacadeGrainShading;

//How the wall answers light against how the glass does (see SurfaceSpecular). The wall is rough and shows
//almost no highlight; the window keeps -- and is boosted past -- the shine the whole tower used to have,
//because a window IS the glass on a building and should be the only thing on it mirroring the sky.
float FacadeSmoothness;
float FacadeHighlight;
float WindowHighlightBoost;
float WindowReflectionBoost;

//Albedo of the glass itself: what is behind a pane is a dim room, not a rendered wall.
float3 WindowGlassColor;

//Glass is glass: no dial, and the value that makes FresnelSchlick and the roughness lerp identities, so a
//pane behaves exactly as every polished surface elsewhere in the scene does.
static const float WindowSmoothness = 1.0;

//A fully saturated color from a hue in [0,1] - the neon signs' palette. Pure and bright; the brightness
//that makes them bloom comes from CityWindowBrightness, not from here.
float3 HueToRGB(float h)
{
    float3 k = frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0));
    return saturate(abs(k * 6.0 - 3.0) - 1.0);
}

//The render's grain, and it has to be NOISE rather than a sum of waves. The scene's own SurfaceReliefWorld
//sums seven sines along seven fixed 3D directions, which decorrelate handsomely over a ball or a triplanar
//floor -- but a facade is a FLAT, axis-aligned plane, and on one of those only each direction's projection
//into the plane survives. Several of the seven project alike, the sum interferes with itself, and the wall
//comes out under a regular diagonal weave: woven cloth, not plaster. (It is the trap the cannon barrel
//already showed once, one surface further on, and the reason that function has seven octaves in the first
//place -- on a plane no number of them is enough.) A hashed lattice has no preferred direction to interfere
//along, so it cannot weave however few octaves it is given.
//Returns the value AND both partial derivatives, in the units of p. Analytic rather than taken with
//ddx/ddy, and that buys two things: the tilt below needs no tangent frame reconstructed from screen
//derivatives, and with no gradient op left anywhere in the grain the whole of it can sit behind a branch on
//a uniform -- which is this shader's rule for scene-gated work, and the off switch a low quality preset needs.
float3 FacadeNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = p - cell;

    //Smoothstep and its own derivative. Not the raw fraction: a linear ramp's derivative jumps at every cell
    //boundary, which would print the noise lattice straight back onto the wall as a grid of creases in the
    //normal -- one regular pattern traded for another.
    float2 u = f * f * (3.0 - 2.0 * f);
    float2 du = 6.0 * f * (1.0 - f);

    float a = Hash21(cell);
    float b = Hash21(cell + float2(1, 0));
    float c = Hash21(cell + float2(0, 1));
    float d = Hash21(cell + float2(1, 1));

    //The bilinear patch written as a + k0.u + k1.v + k2.u.v, which differentiates by inspection
    float k0 = b - a;
    float k1 = c - a;
    float k2 = a - b - c + d;

    //All three scaled alike by the [0,1] -> [-1,1] remap, so the derivatives stay the derivatives OF the value
    return float3(
        (a + k0 * u.x + k1 * u.y + k2 * u.x * u.y) * 2.0 - 1.0,
        (k0 + k2 * u.y) * du.x * 2.0,
        (k1 + k2 * u.x) * du.y * 2.0);
}

//Three octaves of it, value and gradient together. Amplitudes sum to one, so FacadeGrainStrength stays the
//peak height in world units, and each octave fades out on its own once a pixel grows past half its cell --
//the same per-octave band-limiting ReliefOctave does, so the render is fully present on the towers around the
//arena and silently gone on the skyline behind them instead of boiling into a moire.
//
//Three and not more: a fourth would have a cell of some four centimetres of tower, which is inside two units
//of the eye -- closer than the play camera ever gets to a facade -- and it measured at 0.16 ms of the frame.
//Footprint arrives in the same units as the position.
float3 FacadeGrain(float2 position, float footprint)
{
    float3 grain = 0;
    float amplitude = 0.57;
    float frequency = 1.0;

    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float3 octave = FacadeNoise(position * frequency);

        //The gradient is with respect to this octave's own scaled position, so it carries its frequency back
        grain += amplitude * saturate(1 - footprint * frequency * 2.0) * float3(octave.x, octave.yz * frequency);

        amplitude *= 0.5;
        frequency *= 2.17;   //not an exact doubling, which would line successive octaves' lattices up
    }

    return grain;
}

//The profile of one edge of the plaster frame moulding around a pane, as a function of the per-axis cell
//coordinate `withinCell` (0 at the pane centre, 1 at the wall centre between panes). It returns THREE
//things as a float3, each of which the shading below reads separately so the moulding reads as a real
//raised body and not as a painted-on stripe:
//
//   .x = the bead height (0 off the moulding, 1 on its crest). Drives the normal tilt.
//   .y = the bead's analytic slope (signed: +rising up the inner flank toward the glass, -falling down
//        the outer flank toward the wall). Tilts the normal on both flanks so light catches the crest.
//   .z = a sharper "on the crest" mask, near-1 only across the flat top of the bead and 0 on the flanks.
//        This is what gets LIGHTENED -- the crest stands proud, catches the sun, reads as the top face of
//        a real piece of trim, where a height field alone would leave its top the same shade as its sides.
//
//The moulding occupies the ring between the glass edge (withinCell = WindowFill) and its own outer edge
//(WindowFill + WindowFrameWidth). The crest is a real flat top -- a classical profile is a flat fillet, not
//a needle -- occupying the middle third of the ring, with the two flanks rising to and falling from it.
//The flanks are kept SHARP (a fraction of the footprint, not the whole bead width): a moulding that reads
//as standing off the wall has crisp edges, and the first version's mistake was softening the whole bead
//across the footprint, which smeared it into the blurry line you could not read as 3D.
//
//`soft` widens only each flank by the pixel footprint (so a sub-pixel edge still anti-aliases rather than
//shimmering), never the whole bead. At WindowFrameWidth <= 0 this returns 0 (frame off).
float3 WindowFrameProfile(float withinCell, float WindowFill, float WindowFrameWidth, float footprint)
{
    float3 result = 0;

    if (WindowFrameWidth > 0.0)
    {
        float edge0 = WindowFill;
        float edgeOuter = WindowFill + WindowFrameWidth;
        float crest0 = WindowFill + WindowFrameWidth * 0.34;
        float crest1 = WindowFill + WindowFrameWidth * 0.66;

        //The two flanks are each anti-aliased across roughly one pixel -- sharp, but not a shimmering hard
        //step. The flat crest between them is the fillet's own top face.
        float soft = max(footprint * 0.7, WindowFrameWidth * 0.06);
        float inner = smoothstep(edge0 - soft, edge0 + soft, withinCell);
        float outer = 1.0 - smoothstep(edgeOuter - soft, edgeOuter + soft, withinCell);
        float bead = saturate(min(inner, outer));

        //The flat top: 1 between crest0 and crest1, softening off across a pixel at each end of the fillet.
        float topSoft = max(footprint * 0.7, WindowFrameWidth * 0.06);
        float crest = smoothstep(crest0 - topSoft, crest0 + topSoft, withinCell)
            * (1.0 - smoothstep(crest1 - topSoft, crest1 + topSoft, withinCell));

        //The slope is the bead's height derivative: +1 scaled up the inner flank, -1 down the outer, ~0 on
        //the flat crest. Used to tilt the normal; on the crest itself it is ~0, which is correct (a flat
        //top face has the wall's own normal). Analytic so no ddx/ddy is needed in the frame block.
        float innerSlope = 6.0 * inner * (1.0 - inner) / max(2.0 * soft, 1e-5);
        float outerSlope = 6.0 * outer * (1.0 - outer) / max(2.0 * soft, 1e-5);
        float slope = innerSlope - outerSlope;

        result = float3(bead, slope, crest);
    }

    return result;
}

//How much of a pixel the interval [a, b] covers, the pixel spanning `footprint` about x: a box filter, not a
//smoothstep. A smoothstep across a feature narrower than the pixel still peaks at a half, so a glazing bar a
//tenth of a pixel wide would draw as a half-bright line; the box filter gives it its tenth, which is what keeps
//the window surround's pieces reading at their true weight on the far towers instead of shimmering or vanishing.
float WindowSpan(float x, float a, float b, float footprint)
{
    return saturate((min(x + 0.5 * footprint, b) - max(x - 0.5 * footprint, a)) / max(footprint, 1e-4));
}

//The city needs each building's own extent, not just world position, so windows can be laid out relative to
//the tower (a consistent edge margin) instead of on a world grid that clips them at the corners. This VS
//hands the pixel shader the offset from the building's centre and the building's world size. The box is the
//1x1x1 unit cube, so a transformed unit direction is the world size along that axis and the transform of the
//object origin is the centre; Bone is applied exactly as for the position. Everything else matches MainVS.
struct CityVSOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
    float3 PosFromCenter : TEXCOORD3;
    float3 BuildingSize : TEXCOORD4;
};

CityVSOutput CityVS(VertexShaderInput input, InstanceInput instance)
{
    CityVSOutput output;

    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);
    float4 worldPosition = mul(mul(input.Position, Bone), world);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, world);
    output.OcclusionData = instance.Custom;

    float3 center = mul(mul(float4(0, 0, 0, 1), Bone), world).xyz;
    output.PosFromCenter = worldPosition.xyz - center;
    output.BuildingSize = float3(
        length(mul(mul(float4(1, 0, 0, 0), Bone), world).xyz),
        length(mul(mul(float4(0, 1, 0, 0), Bone), world).xyz),
        length(mul(mul(float4(0, 0, 1, 0), Bone), world).xyz));

    return output;
}

//Windows laid out relative to the building rather than on a world grid: a fixed wall margin (WindowMargin)
//is held at every edge and the windows are spread evenly across the interior, so none is jammed into a
//corner and every tower carries the same border. The count is solved from the building's world size against
//the target pitch, so a taller tower still gets more floors and a wider one more columns.
float4 CityPS(CityVSOutput input) : COLOR
{
    float3 worldNormal = normalize(input.WorldNormal);

    //Which pair of world axes runs across this facade. Branchless: a lerp on the face's own normal,
    //which is constant over a flat face, so the derivatives below stay well defined.
    float facingX = step(abs(worldNormal.z), abs(worldNormal.x));
    //Facade coordinates measured from the building centre (horizontal axis picked by the facing), and the
    //building's half-size along the same two axes. Vertical is always world Y.
    float2 posFromCenter = float2(lerp(input.PosFromCenter.x, input.PosFromCenter.z, facingX), input.PosFromCenter.y);
    float2 halfSize = float2(lerp(input.BuildingSize.x, input.BuildingSize.z, facingX), input.BuildingSize.y) * 0.5;

    //Roofs and the ground faces get no windows
    float vertical = 1 - step(0.5, abs(worldNormal.y));

    //Interior left for glass after the wall margin, the whole windows that fit at the target pitch, and the
    //per-building pitch that fills the interior evenly (kept near the target, so it still reads natural).
    float2 interior = halfSize - WindowMargin;
    float2 count = floor(interior * 2.0 / float2(WindowPitchX, WindowPitchY) + 0.5);
    float hasGrid = step(1.0, count.x) * step(1.0, count.y) * vertical;
    float2 cellPitch = (interior * 2.0) / max(count, 1.0);

    float2 grid = (posFromCenter + interior) / cellPitch;
    float2 cell = floor(grid);
    float2 withinCell = abs(frac(grid) - 0.5) * 2;

    //The pixel's extent across the facade, per axis, in cells. Band-limited the way every other feature
    //here is: once a pixel covers more than a window the pattern fades to its own average rather than
    //aliasing into a moire of lit and unlit floors, which is what a city at distance would otherwise do.
    float2 footprint = (abs(ddx(posFromCenter)) + abs(ddy(posFromCenter))) / cellPitch;
    float resolvable = saturate(1 - max(footprint.x, footprint.y));

    float2 shape = smoothstep(float2(WindowFillX, WindowFillY) + footprint, float2(WindowFillX, WindowFillY) - footprint, withinCell);

    //No glass in the wall margin outside the interior (the cut lands in wall, so it needs no smoothing)
    float2 inside = step(abs(posFromCenter), interior);
    float window = shape.x * shape.y * hasGrid * inside.x * inside.y;

    //The building this facade belongs to, taken from the tower's own centre so it is one value across the
    //whole tower (neon hue per tower, and a window pattern that belongs to the building not the world grid)
    float facadeY = input.WorldPosition.y;
    float2 buildingId = floor((input.WorldPosition.xz - input.PosFromCenter.xz) * 0.37);
    float2 windowId = cell + buildingId * 101.0;

    //A window does not decide once and for all. Each keeps its own rhythm — a stretch of its own length,
    //then it decides again — so lamps come on and go out across the skyline at their own pace. A city
    //whose windows never change reads as a texture of a city rather than as one with people in it.
    float rhythm = Hash21(windowId + 3.71);
    float interval = WindowHoldSeconds + rhythm * WindowHoldVariation;
    float slot = CityWindowTime / interval + rhythm * 37.0;
    float slotIndex = floor(slot);

    float wasLit = step(1 - WindowLitFraction, Hash21(windowId + slotIndex * 17.13));
    float willBeLit = step(1 - WindowLitFraction, Hash21(windowId + (slotIndex + 1) * 17.13));

    //The switch is a short fade rather than a cut: at this distance a lamp that vanishes between two
    //frames reads as a rendering glitch, one that dies over a moment reads as somebody leaving
    float lit = lerp(wasLit, willBeLit, smoothstep(1 - WindowSwitchFade, 1, frac(slot)));

    //Ordinary warm/cool lamp for the plain daytime city
    float3 lamp = lerp(WindowWarm, WindowCool, step(0.5, Hash21(cell * 1.7 + 11.3)));

    //Fading to the average keeps a distant tower a dim glowing block instead of a flickering one
    float coverage = lerp(WindowFillX * WindowFillY * WindowLitFraction * hasGrid * inside.x * inside.y, window * lit, resolvable);

    //Where the facade is glass rather than plaster, which is what the material below is blended by. NOT the
    //emission's coverage: that one is multiplied by `lit`, and whether the lamp behind a pane happens to be
    //on says nothing about what the pane is made of -- a dark window is still the one part of the wall that
    //mirrors the sky. Faded to the windows' own area fraction at distance, the same band-limiting the
    //emission gets, so a far tower becomes one averaged material instead of aliasing between two.
    float glass = lerp(WindowFillX * WindowFillY * hasGrid * inside.x * inside.y, window, resolvable);

    //--- The surround, the sill, the reveal and the glazing bars (#435) ------------------------------------------
    //The moulding below was already here and was right in shape, and it still could not be seen: it was a tenth of
    //a half-cell wide, about 0.085 world units, which is one pixel at 60 units at 1600x900 and nothing past it, and
    //`resolvable` then switched it off entirely wherever a pixel covered more than a window. What the references
    //show a window reading by at a tower's distance is not a moulding's relief but four flat, box-filtered cues
    //that survive being small: the surround's own tone, the sill's lit top over its shadow, the reveal's shadow on
    //the glass, and the bars across the pane. Measured in withinCell's half-cell units, so every width is divided
    //by the half-cell it runs across; the footprint is doubled for the same reason (it is in whole cells).
    float2 halfCell = cellPitch * 0.5;
    float2 cellFootprint = max(footprint * 2.0, 1e-4);
    float signedCellY = (frac(grid.y) - 0.5) * 2.0;
    float windowGate = hasGrid * inside.x * inside.y * vertical;

    float paneSpan = WindowSpan(withinCell.x, -WindowFillX, WindowFillX, cellFootprint.x)
        * WindowSpan(signedCellY, -WindowFillY, WindowFillY, cellFootprint.y);
    float surroundX = WindowFillX + WindowFrameWidth;
    float surroundY = WindowFillY + WindowFrameWidth;
    float surround = saturate(WindowSpan(withinCell.x, -surroundX, surroundX, cellFootprint.x)
        * WindowSpan(signedCellY, -surroundY, surroundY, cellFootprint.y) - paneSpan) * windowGate;

    float sillHalfX = surroundX + WindowSillOverhang / halfCell.x;
    float sillTop = -surroundY;
    float sillBottom = sillTop - WindowSillHeight / halfCell.y;
    float sillAcross = WindowSpan(withinCell.x, -sillHalfX, sillHalfX, cellFootprint.x);
    float sill = sillAcross * WindowSpan(signedCellY, sillBottom, sillTop, cellFootprint.y) * windowGate;
    float sillShade = sillAcross * WindowSpan(signedCellY, sillBottom - WindowSillShadow / halfCell.y, sillBottom, cellFootprint.y) * windowGate;

    float reveal = WindowSpan(withinCell.x, -WindowFillX, WindowFillX, cellFootprint.x)
        * WindowSpan(signedCellY, WindowFillY - WindowRevealDepth / halfCell.y, WindowFillY, cellFootprint.y) * windowGate;

    //One upright down the middle and one rail a third of the way up, the way a sash window is divided
    float barHalfX = 0.5 * WindowBarWidth / halfCell.x;
    float barHalfY = 0.5 * WindowBarWidth / halfCell.y;
    float railY = WindowFillY * 0.33;
    float bars = saturate(WindowSpan(withinCell.x, -barHalfX, barHalfX, cellFootprint.x)
        + WindowSpan(signedCellY, railY - barHalfY, railY + barHalfY, cellFootprint.y)) * paneSpan * windowGate;

    glass *= 1.0 - bars;
    coverage *= (1.0 - bars) * (1.0 - 0.5 * reveal);

    //The plaster frame moulding around each pane. Two earlier versions were wrong in instructive ways: the
    //first combined the two axes with max and drew a grid over the facade; the second was a correct ring but
    //a height field at 0.015 world units, softened across the whole footprint, which read as a blurry line
    //rather than as trim standing off the wall. A moulding that reads as 3D needs three things a flat bead
    //lacks: a CRISP edge (so the eye sees a body, not a gradient), a LIT FLAT TOP that stands proud of the
    //wall and catches the sun, and a CAST SHADOW thrown onto the wall in the sun's lee. All three are below.
    //
    //WindowFrameProfile returns (.x height, .y slope, .z crestTop): the height tilts the normal, the crest
    //mask picks out the flat fillet's top to be lightened, and the slope carries the flank shading. The two
    //axes are gated by each other's pane span so the four pieces meet as one ring per window, never a grid.
    float3 frameX = WindowFrameProfile(withinCell.x, WindowFillX, WindowFrameWidth, footprint.x);
    float3 frameY = WindowFrameProfile(withinCell.y, WindowFillY, WindowFrameWidth, footprint.y);

    //The cross-axis span of one window plus its frame: 1 inside, softening to 0 across one pixel at the
    //pane's outer edge, so the bead stops where the wall between windows begins.
    float paneSpanX = 1.0 - smoothstep(WindowFillX + WindowFrameWidth - footprint.x, WindowFillX + WindowFrameWidth + footprint.x, withinCell.x);
    float paneSpanY = 1.0 - smoothstep(WindowFillY + WindowFrameWidth - footprint.y, WindowFillY + WindowFrameWidth + footprint.y, withinCell.y);

    //Side beads (height/slope/crest on X) exist only within the pane's vertical span; head/sill (on Y) only
    //within its horizontal span. The two never run past the pane, so they form a ring, not a grid.
    float frameBead = saturate(max(frameX.x * paneSpanY, frameY.x * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
    float frameCrest = saturate(max(frameX.z * paneSpanY, frameY.z * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
    frameBead = lerp(0.0, frameBead, resolvable);
    frameCrest = lerp(0.0, frameCrest, resolvable);

    //The cast shadow: the moulding stands proud of the wall, so it occludes the sun for the strip of wall on
    //its lee side. Sample the bead mask a little way DOWN-SUN from this pixel and, if that offset point sits
    //on the frame, this pixel is in the frame's shadow. The offset is the frame's own height projected onto
    //the wall along the sun's direction -- the same geometry a real projection-cast shadow uses, and the one
    //cue that most strongly reads as "this trim is raised", because a flat stripe casts nothing. `cellPitch`
    //turns the world-space offset back into the cell space `withinCell` lives in. Softened by the footprint
    //so the penumbra widens at distance instead of aliasing; zero where the sun is behind the camera (no
    //visible cast shadow then, and the division would blow up).
    float2 sunOnFacade = float2(lerp(SunDirection.x, SunDirection.z, facingX), SunDirection.y);
    float sunLen = length(sunOnFacade);
    float frameShadow = 0.0;
    if (sunLen > 0.05)
    {
        float2 offsetDir = sunOnFacade / sunLen;
        //Project the bead's height along the sun direction: taller trim throws a longer shadow.
        float2 offsetCells = offsetDir * WindowFrameHeight * 1.7 / cellPitch;
        float3 ssX = WindowFrameProfile(withinCell.x - offsetCells.x, WindowFillX, WindowFrameWidth, footprint.x);
        float3 ssY = WindowFrameProfile(withinCell.y - offsetCells.y, WindowFillY, WindowFrameWidth, footprint.y);
        float shadowBead = saturate(max(ssX.x * paneSpanY, ssY.x * paneSpanX)) * (1.0 - glass) * hasGrid * inside.x * inside.y * vertical;
        //Only the wall in the moulding's lee is shadowed, not the moulding itself, and not the glass.
        shadowBead *= (1.0 - frameBead) * (1.0 - glass);
        float penumbra = max(footprint.x, footprint.y);
        frameShadow = lerp(shadowBead, 0.0, saturate(penumbra * 3.0)) * resolvable;
    }

    float3 lampColor = lamp;
    float windowFlicker = 1.0;
    float3 signEmission = float3(0.0, 0.0, 0.0);

    //One tower is not the next. Real renders are mixed and painted and weathered per building, and once the
    //wall is matte its tone is the only variety it has left -- the tonal spread the skyline used to get came
    //from the mirror, and goes out with it. Off the building's own id, so it is one shade per tower.
    float3 facadeColor = FacadeColor * (1.0 + FacadeColorVariation * (Hash21(buildingId + 13.9) - 0.5) * 2.0);

    //Neon night city, gated at runtime by CityNeon; both the Testbed and the map editor drive it (V cycles
    //to the neon scene in the editor too). The skyline runs on magenta and cyan, the pink-and-blue of a neon
    //street, with the odd off-colour tower; about one window in six sparks the opposite hue, big sign bands
    //wrap some towers in the contrast colour, and a fraction of it all buzzes. Brightness sits over the glare
    //threshold, so every lit pane blooms.
    //
    //A uniform branch, deliberately: CityNeon is 0 in the ordinary city — the DEFAULT scene — and this block
    //is ~7 hashes of per-pixel work that the lerps below would throw away entirely at 0. A branch on a
    //uniform is non-divergent (every pixel takes the same path) and there are no gradient ops inside, so it
    //is derivative-safe; the defaults above already hold the plain-city values for the else path.
    [branch]
    if (CityNeon > 0.0)
    {
        float3 neonMagenta = float3(1.0, 0.04, 0.85);
        float3 neonCyan = float3(0.05, 0.85, 1.0);

        float pickBuilding = Hash21(buildingId + 5.0);
        float3 buildingNeon = pickBuilding < 0.45 ? neonMagenta : (pickBuilding < 0.9 ? neonCyan : HueToRGB(Hash21(buildingId + 6.3)));
        float3 contrast = buildingNeon.r > buildingNeon.b ? neonCyan : neonMagenta;
        float3 neonWindow = lerp(buildingNeon, contrast, step(0.83, Hash21(windowId + 4.4)));

        //A bright solid sign band wrapping some towers at a hashed height, in the contrast colour
        float hasSign = step(0.5, Hash21(buildingId + 21.0));
        float signHeight = 5.0 + Hash21(buildingId + 22.0) * 34.0;
        float signBand = hasSign * vertical * (1.0 - smoothstep(1.1, 1.9, abs(facadeY - signHeight))) * resolvable;

        //A fraction of the windows buzz on and off, the way a tired neon tube does
        float flickerId = Hash21(windowId + 8.8);
        float buzz = 0.55 + 0.45 * step(0.45, frac(CityWindowTime * (5.0 + flickerId * 9.0) + flickerId * 13.0));

        //Kept as lerps by CityNeon (not straight assignments), so a fractional CityNeon still blends exactly
        //as it did when this ran unconditionally
        lampColor = lerp(lamp, neonWindow, CityNeon);
        windowFlicker = lerp(1.0, lerp(1.0, buzz, step(0.86, flickerId)), CityNeon);
        signEmission = signBand * contrast * (CityWindowBrightness * 1.6) * CityNeon;
        facadeColor = lerp(facadeColor, FacadeNeonColor, CityNeon);
    }

    //The render's grain, in the facade's own 2D frame. Offset by the building's id so every tower is rendered
    //in its own patch rather than all of them wearing one pattern at the same height, and by the facing so a
    //tower's two axes do not match each other. Anchored to the building, like the window grid and for the same
    //reason; the pattern does not carry around a corner, which a hard 90-degree edge hides.
    //
    //The footprint is the window grid's own, multiplied back out of cells into world units along the facade --
    //that quantity is already abs(ddx) + abs(ddy) of posFromCenter, so reusing it saves a second pair of
    //derivative ops rather than merely tidying.
    float2 facadeFootprint = footprint * cellPitch;

    //Vertical faces only. A roof's posFromCenter is (z, a constant), so the field would degenerate into
    //stripes along one axis there; a flat concrete roof reading flat is the better answer, and it is matte
    //either way, since the material below does not depend on the grain.
    float3 grain = 0;

    //A branch on a uniform, which is what this shader's conventions ask for: FacadeGrainStrength is the one
    //dial that turns the render's grain off, everything below is multiplied away at 0, every pixel takes the
    //same path, and there is not a single gradient op inside -- the noise's gradient being analytic is exactly
    //what makes that last part true.
    [branch]
    if (FacadeGrainStrength > 0.0)
        grain = FacadeGrain((posFromCenter + buildingId * 37.0 + facingX * 19.0) * FacadeGrainFrequency,
            max(facadeFootprint.x, facadeFootprint.y) * FacadeGrainFrequency) * vertical;

    //A facade is an axis-aligned plane, and that is worth exploiting rather than working around: its two
    //tangents ARE world axes, so the height field's analytic gradient becomes a world-space tilt directly --
    //no tangent frame rebuilt from screen derivatives, no ddx of the height, and exact. The same flatness that
    //makes a sum of sines weave here is what makes this cheap.
    float3 tangentAcross = facingX > 0.5 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 slope = FacadeGrainStrength * FacadeGrainFrequency * (grain.y * tangentAcross + grain.z * float3(0, 1, 0));

    //The frame's own tilt rides the same mechanism: its analytic slope (frameX.y / frameY.y) becomes a
    //world-space lean on the bead's two flanks. The slope is gated by the bead mask, so it is exactly zero
    //over the glass and in the deep wall -- only the bead's two rising/falling edges tilt the normal, which
    //is what makes the moulding read as standing proud of the wall rather than as a painted-on stripe.
    //Scale by the bead height (world units) the way the grain's slope carries FacadeGrainStrength.
    slope += WindowFrameHeight * frameBead * (frameX.y * tangentAcross + frameY.y * float3(0, 1, 0));

    //Tilted first and blended back towards the flat face by the glass mask, never the other way round: a pane
    //is flat, but folding the mask into the height would put a window edge inside the gradient.
    float3 shadingNormal = normalize(worldNormal - slope * (1.0 - glass));
    float grainField = grain.x;

    //A tilted normal alone is not enough, and that is the lesson the ground's coursed slabs already taught:
    //relief by normal has its bumps lit and its hollows just as bright as its peaks, which is most of why it
    //reads as a painted-on texture rather than as shape. At a tower's distance a few degrees of tilt changes
    //a matte surface's N.L by a percent or two and is simply invisible. So the same field also SHADES -- it
    //darkens the ambient a hollow cannot see, and mottles the render's own tone, which at this range is the
    //stronger cue of the two. One field doing both is what a real render does: the hollows hold the shade and
    //the high spots wear lighter. Off over the glass, which is flat and evenly tinted.
    float plaster = 1.0 - glass;
    float cavity = 1.0 - FacadeGrainShading * saturate(-grainField) * plaster;

    facadeColor *= 1.0 + FacadeGrainShading * grainField * plaster;

    //The frame's three shading cues, each strong enough to read at a tower's distance:
    //  - the FLAT TOP lightens, because a fillet standing proud catches the sky and the sun a flat wall does
    //    not. This is the cue that says "the top of a body", and it is why the profile keeps a crest mask
    //    separate from the height: lightening the whole bead would light its shadowed flanks too.
    //  - the CAST SHADOW darkens the wall in the moulding's lee (frameShadow, computed above). This is the
    //    cue that says "a body that occludes the sun", and a flat stripe casts none -- which is exactly why
    //    the height-field-only version read as paint.
    //  - the cavity term still darkens the bead's own hollows for the grain, untouched by the frame.
    facadeColor *= 1.0 + WindowFrameShading * 1.4 * frameCrest;
    facadeColor *= 1.0 - WindowFrameShading * 0.9 * frameShadow;
    cavity = saturate(cavity - WindowFrameShading * 0.5 * frameShadow);

    //The surround, the bars and the sill in the frame's own tone; the sill's top faces the sky, so it is lighter
    //again, and the wall under it is in its shadow (#435, see the block where the masks are made)
    facadeColor *= lerp(1.0, WindowFrameTone, saturate(surround + sill + bars));
    facadeColor *= 1.0 + 0.35 * sill;
    facadeColor *= 1.0 - WindowSillShading * sillShade;
    cavity = saturate(cavity - 0.5 * WindowSillShading * sillShade);

    //And the glass gets its own albedo, which is DARK: what is behind a pane is a dim room, not a rendered
    //wall. That is the other half of why the windows read as glass and the wall does not -- a dark surface
    //under a bright mirror is exactly what glass looks like, and it is the same combination that was wrong
    //on the plaster. It also gives a facade its variation back: a pane is dark where it faces nothing and
    //bright where it catches the sky, which is how a glazed tower reads at all.
    facadeColor = lerp(facadeColor, WindowGlassColor, glass);

    //The recessed glass lies in the reveal's shadow along its head
    facadeColor *= 1.0 - 0.45 * reveal * glass;

    //Two materials on one triangle, blended per pixel: rough plaster, and the glass of the windows in it.
    SurfaceSpecular surface;
    surface.Highlight = lerp(FacadeHighlight, WindowHighlightBoost, glass);
    surface.Environment = lerp(1.0, WindowReflectionBoost, glass);
    surface.Smoothness = lerp(FacadeSmoothness, WindowSmoothness, glass);

    float4 shaded = ShadePixel(input.WorldPosition, shadingNormal, input.OcclusionData, float4(facadeColor, 1), 1, cavity, surface);

    shaded.rgb += coverage * lampColor * CityWindowBrightness * windowFlicker;
    shaded.rgb += signEmission;

    return shaded;
}

technique InstancedCity
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CityVS();
        PixelShader = compile PS_SHADERMODEL CityPS();
    }
};
