//Draws the Grid scene (#393): an early-1980s computer-graphics backdrop, drawn from named mathematics
//rather than noise - a flat, black-body floor lit only by its own glowing lines, a Hilbert curve traced along
//them as a circuit, and distant solids whose seams glow and whose windows run Conway's Game of Life, under a
//near-black, starless void. The twentieth SceneKind, and the third to belong to BOTH scene families at once,
//after the Moon (#125) and the aurora (#205): real ground (SceneRenderer.IsSolidTerrainScene - flat, the
//island's footprint clip()ed out, the dark pit shaft backing the drain) under a REPLACED sky
//(SceneRenderer.ReplacesSky - no dome, black clear, its own light rig). Three techniques over one effect, drawn
//in this order by DrawGrid: GridTerrain first (depth-writing, opaque), then GridTowers (the same), then GridSky
//depth-READ against both on the shared space quad - the Moon's own measured order (the opposite interleave was
//an 8x frame blow-up there; see DrawMoon's doc).
//
//The floor is genuinely FLAT - its height is a constant, not a height field - which is the honest source of
//most of this scene's cheapness: no octave sum, no per-pixel gradient normal (the normal is always straight
//up), and the camera-centred grid mesh itself is built coarse (GRID_MESH_N in SceneRenderer.cs) because
//nothing is ever displaced at vertex resolution. What the pixel shader spends instead is the Hilbert-curve
//test: three small bit-recursions per pixel, each bounded to GRID_HILBERT_BITS iterations of integer ALU only
//- no texture fetch, no transcendental - and computing no derivative of its own, so it is safe beside the
//derivatives the line antialiasing takes before it (BestPractices' own rule for a branch beside a gradient op).
//
//The sky is the plainest of the six sky-replacing passes: VoidColor and a dither against 8-bit banding,
//nothing else - no stars (Stars.fxh is deliberately not included). A starfield is space's, the Moon's and
//the aurora's look; this scene's void is meant to read as "nothing drawn" rather than as another night sky.
//
//Deliberately backdrop-only (see GridSceneConfig's own class doc and "The Grid" in docs/scenes.md): the
//balls, the island and the gun stay on the ordinary lit InstancedModel.fx path, tinted only by this scene's
//own light rig - the issue's own recommended default, and the smaller, cheaper, more consistent change.
//
//Credit, in the order it matters to this scene (the full record is issue #393): the backlit optical-printer
//compositing process that gave TRON (1982) its glowing costumes is the literal model for the shading rule
//here - a near-black body lit only by its own bright seams: the floor's lines, the Hilbert trace and every
//edge of every solid standing in for a costume's panel lines, fed to the same bloom
//(PostProcessPipeline/GLARE_THRESHOLD) every other emissive surface in this game already blooms through.
//MAGI/SynthaVision's combinatorial-solid vocabulary is this project's own procedural-mesh house style already
//(BoxMesh, LatheMesh, MeshBuilder), and the solids hold to it: plain rectangular prisms. Robert Abel and
//Associates' circuit-patterned digital terrain flyovers are the direct ancestor of the Hilbert tracery below.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float4x4 InverseViewProjection;
float3 CameraPosition;

float2 OriginXZ;

//Radius of the platform footprint cut out of the terrain around the world origin - see Forest.fx's own
//uniform of the same name for why. The Testbed and the Game set this to the island's radius; the map
//editor draws no island, so it leaves it 0 and nothing is cut.
float IslandHoleRadius;

//--- Terrain: a flat, glowing circuit-board floor --------------------------------------------------------

float GridLevelY;
float GridCellSize;
float GridLineWidth;
float GridAccentWidthScale;
float GridTraceStride;
float GridHorizonHazeDistance;

float3 GridBodyColor;
float3 GridLineColor;
float3 GridAccentColor;
float3 VoidColor;

//How many trace-lattice nodes make one repeat of the Hilbert tile - 2^GRID_HILBERT_BITS. A shader constant
//rather than a uniform: it sizes a fixed unrolled loop (GridHilbertIndex below), and the live-tunable scale
//of the pattern is GridCellSize x GridTraceStride, which needs no recompile. 6 bits = a 64-node tile. The
//tile's size no longer shows anywhere on the floor - the tiles chain into one unbroken curve (see
//GRID_HILBERT_LAST) - so this only decides how large the curve's biggest self-similar turn is.
#define GRID_HILBERT_BITS 6
#define GRID_HILBERT_N (1 << GRID_HILBERT_BITS)

//The last index along one tile's curve. The classic curve enters a tile at its (0, 0) node and leaves it at its
//(N - 1, 0) node - both on the same side - so the last node of one tile and the first node of the next tile
//along +X are neighbours, and joining them turns a row of tiles into a single infinite curve. With that one
//extra link every node of the floor has exactly two lit segments meeting at it (checked over 301 x 301 nodes
//across several tile boundaries): no dead end, no seam.
#define GRID_HILBERT_LAST (GRID_HILBERT_N * GRID_HILBERT_N - 1)

//Proper (always non-negative) modulo, for tiling a pattern across a floor far larger than one tile: HLSL's %
//keeps the sign of its dividend, so a plain % on a negative coordinate would hand GridHilbertIndex a negative
//x or y and read outside the curve it was built for. A mask rather than a real modulus - correct ONLY because
//n is a power of two (GRID_HILBERT_N and GRID_LIFE_SIZE always are): two's-complement AND against n-1 extracts
//exactly the low bits, which is the non-negative floored result for any sign of x, and is close to free where
//an integer % measurably is not (HLSL X3556).
int GridMod(int x, int n)
{
    return x & (n - 1);
}

//The classic Hilbert-curve index of a node (Wikipedia's xy2d, unrolled to GRID_HILBERT_BITS iterations of
//integer ALU only). x and y are first folded into [0, GRID_HILBERT_N) so the pattern tiles. Offset by half a
//tile before folding so the arena at the world origin stands at a tile's centre rather than where four tiles
//meet - placement only: since the tiles chain, nothing visible happens at a tile boundary.
int GridHilbertIndex(int nodeX, int nodeZ)
{
    int x = GridMod(nodeX + GRID_HILBERT_N / 2, GRID_HILBERT_N);
    int y = GridMod(nodeZ + GRID_HILBERT_N / 2, GRID_HILBERT_N);
    int d = 0;

    [unroll]
    for (int s = GRID_HILBERT_N / 2; s > 0; s /= 2)
    {
        int rx = (x & s) > 0 ? 1 : 0;
        int ry = (y & s) > 0 ? 1 : 0;
        d += s * s * ((3 * rx) ^ ry);

        //Rotate/flip the quadrant - the algorithm's own step, branchless in effect: rx/ry are per-pixel
        //data and this whole function already runs divergently across a wavefront, so a [branch] hint here
        //would buy nothing the compiler cannot already do with a select.
        if (ry == 0)
        {
            if (rx == 1)
            {
                x = GRID_HILBERT_N - 1 - x;
                y = GRID_HILBERT_N - 1 - y;
            }

            int t = x;
            x = y;
            y = t;
        }
    }

    return d;
}

struct GridVertexInput
{
    float4 Position : POSITION0;
};

struct GridVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
};

GridVertexOutput GridTerrainVS(GridVertexInput input)
{
    GridVertexOutput output;

    float2 xz = input.Position.xz + OriginXZ;
    float3 worldPosition = float3(xz.x, GridLevelY, xz.y);

    output.WorldPosition = worldPosition;
    output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

    return output;
}

//The world size of one pixel along each axis of a surface's own 2D coordinate - how much u changes across a
//pixel, and separately how much v does. A line of constant u is crossed along u only, so it must be
//antialiased by the first and never by the second: on a floor seen at a grazing angle one pixel can span a
//hundred times more ground along the view than across it, and a single isotropic length(fwidth()) smears the
//lines that run away from the camera as wide as the ones that cross it.
float2 GridAxisFootprint(float2 uv)
{
    return max(float2(length(float2(ddx(uv.x), ddy(uv.x))), length(float2(ddx(uv.y), ddy(uv.y)))), 1e-5);
}

//An antialiased line mask, 0 off a line and 1 on it: a line of full width `width` world units, `dist` from the
//pixel, repeating every `spacing` units, with `footprint` one pixel's size measured across the line. Two things
//beyond a plain smoothstep, both for the same grazing floor:
// - a line is never drawn thinner than a pixel, but is dimmed by how much of that pixel it really covers, so a
//   distant line keeps its true share of light instead of growing into a band as bright as a near one (which is
//   what the far floor did before: a bright haze of fat lines at the horizon);
// - once a pixel spans more than half the spacing the mask fades to the lines' average coverage, which is what
//   the eye would see there anyway, and which stops the lattice beating against the pixel grid.
//The same filtering Ben Golus sets out for a pristine procedural grid, restated in world units.
float GridLineMask(float dist, float footprint, float width, float spacing)
{
    float halfWidth = width * 0.5;
    float drawHalfWidth = max(halfWidth, footprint * 0.5);
    float feather = footprint * 0.75;

    float mask = 1.0 - smoothstep(drawHalfWidth - feather, drawHalfWidth + feather, dist);
    mask *= halfWidth / drawHalfWidth;

    return lerp(mask, width / spacing, saturate(footprint / spacing * 2.0 - 1.0));
}

float4 GridFloor(GridVertexOutput input)
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);

    //Every derivative first, off the continuous position, before any of the integer work below.
    float2 footprint = GridAxisFootprint(worldPosition.xz);

    //The plain lattice: a line at every multiple of GridCellSize, dim, under the glare threshold.
    float2 cell = worldPosition.xz / GridCellSize;
    float2 lineDist = abs(cell - round(cell)) * GridCellSize;
    float plain = max(GridLineMask(lineDist.x, footprint.x, GridLineWidth, GridCellSize),
                      GridLineMask(lineDist.y, footprint.y, GridLineWidth, GridCellSize));

    //The Hilbert curve, traced ALONG the lattice. Its nodes are lattice VERTICES - every GridTraceStride-th one
    //- and a lattice segment is lit when its two end nodes are consecutive along the curve. (The first cut lit
    //the segment between two consecutive CELLS instead, which is the edge the curve crosses, at right angles to
    //it: a ladder of disconnected rungs that read as a random scatter of brighter lines, not as a curve.)
    float traceSpacing = GridCellSize * max(GridTraceStride, 1.0);
    float2 node = worldPosition.xz / traceSpacing;
    float2 nodeFloor = floor(node);
    float2 nearest = round(node);

    int cx = (int) nodeFloor.x;
    int cz = (int) nodeFloor.y;
    int vx = (int) nearest.x;
    int hz = (int) nearest.y;

    //The pixel is beside two candidate segments: along its nearest line of constant x (x = vx, between z = cz
    //and cz + 1) and its nearest line of constant z (z = hz, between x = cx and cx + 1). They share the corner
    //node (vx, hz), so three index lookups answer both - a select at each step, not a branch.
    int corner = GridHilbertIndex(vx, hz);
    int alongZ = GridHilbertIndex(vx, 2 * cz + 1 - hz);
    int alongX = GridHilbertIndex(2 * cx + 1 - vx, hz);

    float zOnCurve = abs(corner - alongZ) == 1 ? 1.0 : 0.0;

    int west = vx == cx ? corner : alongX;
    int east = vx == cx ? alongX : corner;
    float xOnCurve = (abs(west - east) == 1 || (west == GRID_HILBERT_LAST && east == 0)) ? 1.0 : 0.0;

    //Wider as well as brighter than the lattice: a same-width trace in a different colour measured too subtle
    //from play distance in #393's first capture to call a feature at all.
    float2 traceDist = abs(node - nearest) * traceSpacing;
    float traceWidth = GridLineWidth * GridAccentWidthScale;
    float trace = max(zOnCurve * GridLineMask(traceDist.x, footprint.x, traceWidth, traceSpacing),
                      xOnCurve * GridLineMask(traceDist.y, footprint.y, traceWidth, traceSpacing));

    float3 color = lerp(GridBodyColor, GridLineColor, plain);
    color = lerp(color, GridAccentColor, trace);

    //Fade to the void at the horizon - a flat floor with a hard edge at the far plane reads as a wall, not
    //a vanishing point, so this is what lets the grid recede into the sky instead.
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / GridHorizonHazeDistance);
    color = lerp(color, VoidColor, saturate(haze * haze * 1.15));

    return float4(color, 1.0);
}

float4 GridTerrainPS(GridVertexOutput input) : COLOR { return GridFloor(input); }

//--- Sky: the plainest of the six sky-replacing passes - a flat void and a dither, nothing else -----------

struct GridSkyVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

GridSkyVertexOutput GridSkyVS(float3 position : POSITION0)
{
    GridSkyVertexOutput output;

    //Depth-READ after the terrain (see the header) - z = w puts the quad on the far plane, which is what
    //makes that test cheap and correct.
    output.Position = float4(position.xy, 1.0, 1.0);

    float4 far = mul(float4(position.xy, 1.0, 1.0), InverseViewProjection);
    output.Ray = far.xyz / far.w - CameraPosition;

    return output;
}

float4 GridSkyPS(GridSkyVertexOutput input) : COLOR
{
    float3 dir = normalize(input.Ray);

    //The Moon's and the aurora's own pixel-footprint measure, for the same dither-grain reason: it keeps
    //the void's own grain the same size on screen at every resolution and every supersampling setting.
    float pixelAngle = max(sqrt(dot(ddx(dir), ddx(dir)) + dot(ddy(dir), ddy(dir))), 1e-6);

    float3 sky = VoidColor * (1.0 + NoiseHash33(floor(dir / pixelAngle)).x * 0.015);

    return float4(sky, 1.0);
}

//--- Towers: distant solids with glowing seams, their windows running a Game of Life -----------------------
//
//MAGI/SynthaVision's combinatorial-solid discipline (see the header): plain closed rectangular prisms, built
//once on the CPU (SceneRenderer.BuildGridTowers) with their final WORLD-SPACE positions baked directly into the
//vertex buffer - no per-draw world matrix, no instancing, because there are only ever a few dozen of these.
//Two shapes, a tall narrow TOWER and a large, close-to-equilateral CUBE (see GridTowerConfig's own class doc for
//why the owner's review asked for the second).
//
//Each vertex carries two face coordinates, both in WORLD UNITS:
// - WindowUV places the face on its solid's own Life board. The four sides share ONE coordinate running round
//   the solid's perimeter, so the board wraps round the solid like a label - a glider crossing a corner walks
//   on onto the next face, and the panes line up across it - with the board's centre on the middle of the side
//   facing the arena. A top face maps the board's centre to its own.
// - FaceLocal is the position within this one face (xy, from its corner) and the face's size (zw) - what the
//   seams are measured from.
//So the pixel shader never needs to know which face or which solid a pixel belongs to. Each solid draws with
//its own board bound (GridLifeTexture, GRID_LIFE_SIZE square, point-sampled): red is this generation, green the
//one before, and GridLifeAge is the time since that board last stepped (Prazsky.Core.Render.GridLife).

Texture2D GridLifeTexture;
sampler GridLifeSampler = sampler_state
{
    Texture = <GridLifeTexture>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//A solid's Life board's side length - GridLife.SIZE, a power of two so GridMod's bitmask fold applies here too.
#define GRID_LIFE_SIZE 32

float GridTowerWindowCellSize;
float GridTowerWindowMargin;
float GridTowerEdgeWidth;
float GridPhosphorDecay;
float GridLifeAge;
float3 GridTowerBodyColor;
float3 GridTowerWindowColor;
float3 GridTowerEdgeColor;

struct GridTowerVertexInput
{
    float3 Position : POSITION0;
    float2 WindowUV : TEXCOORD0;
    float4 FaceLocal : TEXCOORD1;
};

struct GridTowerVertexOutput
{
    float4 Position : SV_POSITION;
    float2 WindowUV : TEXCOORD0;
    float4 FaceLocal : TEXCOORD1;
};

GridTowerVertexOutput GridTowerVS(GridTowerVertexInput input)
{
    GridTowerVertexOutput output;

    //Already in world space (baked at mesh-build time), so only the view/projection step is left.
    output.Position = mul(mul(float4(input.Position, 1.0), View), Projection);
    output.WindowUV = input.WindowUV;
    output.FaceLocal = input.FaceLocal;

    return output;
}

float4 GridTowerPS(GridTowerVertexOutput input) : COLOR
{
    //Every derivative first, off continuous coordinates, before anything data-dependent.
    float2 cell = input.WindowUV / GridTowerWindowCellSize;
    float2 cellFootprint = max(fwidth(cell), 1e-5);
    float2 local = input.FaceLocal.xy;
    float2 size = input.FaceLocal.zw;
    float2 localFootprint = GridAxisFootprint(local);

    int cellX = GridMod((int) floor(cell.x), GRID_LIFE_SIZE);
    int cellY = GridMod((int) floor(cell.y), GRID_LIFE_SIZE);

    //Sampled at the texel CENTRE (+0.5), not its corner - point filtering on an edge-aligned UV is one
    //rounding error away from reading the wrong neighbouring texel.
    float2 lifeUV = (float2(cellX, cellY) + 0.5) / (float) GRID_LIFE_SIZE;
    float4 life = tex2Dlod(GridLifeSampler, float4(lifeUV, 0.0, 0.0));

    //The phosphor: a cell alive this generation is lit outright - a beam draws at once - and one alive only in
    //the generation before decays the way a phosphor does, so a glider leaves a short trail behind it and a
    //pattern being replaced goes out rather than off.
    float alive = max(life.r, life.g * exp(-GridLifeAge / max(GridPhosphorDecay, 1e-3)));

    //The pane: the cell less its dark mullion (a window with none reads as one unbroken glowing wall), feathered
    //over a pixel so the panes do not crawl as the camera moves, and faded to its open area once a pixel spans
    //half a pane.
    float2 cellFrac = frac(cell);
    float2 inside = smoothstep(GridTowerWindowMargin - cellFootprint, GridTowerWindowMargin + cellFootprint, cellFrac)
        * smoothstep(GridTowerWindowMargin - cellFootprint, GridTowerWindowMargin + cellFootprint, 1.0 - cellFrac);
    inside = lerp(inside, 1.0 - 2.0 * GridTowerWindowMargin, saturate(cellFootprint * 2.0 - 1.0));
    float lit = inside.x * inside.y * alive;

    //The seams: a band along every edge of every face. Two faces meet at each edge and each draws its own half,
    //so a corner reads as one line of the full width. Without them a solid's body is within a few codes of the
    //void behind it and only its lit windows show - a pattern floating in the dark rather than an object.
    float2 edgeDist = min(local, size - local);
    float edge = max(GridLineMask(edgeDist.x, localFootprint.x, GridTowerEdgeWidth, size.x),
                     GridLineMask(edgeDist.y, localFootprint.y, GridTowerEdgeWidth, size.y));

    float3 color = lerp(GridTowerBodyColor, GridTowerWindowColor, lit);
    color = lerp(color, GridTowerEdgeColor, edge);

    return float4(color, 1.0);
}

technique GridTowers
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GridTowerVS();
        PixelShader = compile PS_SHADERMODEL GridTowerPS();
    }
};

technique GridTerrain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GridTerrainVS();
        PixelShader = compile PS_SHADERMODEL GridTerrainPS();
    }
};

technique GridSky
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GridSkyVS();
        PixelShader = compile PS_SHADERMODEL GridSkyPS();
    }
};
