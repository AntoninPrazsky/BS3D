//Draws the Grid scene (#393): an early-1980s computer-graphics backdrop, drawn from named mathematics
//rather than noise - a flat, black-body floor lit only by its own glowing lines, a Hilbert-curve circuit
//trace threading the plain grid, under a near-black, starless void. The twentieth SceneKind, and the third
//to belong to BOTH scene families at once, after the Moon (#125) and the aurora (#205): real ground
//(SceneRenderer.IsSolidTerrainScene - flat, the island's footprint clip()ed out, the dark pit shaft backing
//the drain) under a REPLACED sky (SceneRenderer.ReplacesSky - no dome, black clear, its own light rig). Two
//techniques over one effect, drawn in that order by DrawGrid: GridTerrain first (depth-writing, opaque),
//then GridSky depth-READ against it on the shared space quad - the Moon's own measured order (the opposite
//interleave was an 8x frame blow-up there; see DrawMoon's doc).
//
//The floor is genuinely FLAT - TerrainHeight is a constant, not a height field - which is the honest source
//of most of this scene's cheapness: no octave sum, no per-pixel gradient normal (the normal is always
//straight up), and the camera-centred grid mesh itself is built coarse (GRID_MESH_N in SceneRenderer.cs)
//because nothing is ever displaced at vertex resolution. What the pixel shader spends instead is the
//Hilbert-curve test: three small bit-recursions per pixel (the pixel's own cell, and its nearest neighbour
//across the nearest vertical and nearest horizontal grid line), each bounded to GRID_HILBERT_BITS
//iterations of integer ALU only - no texture fetch, no transcendental - and computing no derivative of its
//own, so it is safe beside the fwidth() the line antialiasing takes outside it (BestPractices' own rule for
//a branch beside a gradient op).
//
//The sky is the plainest of the five sky-replacing passes: VoidColor and a dither against 8-bit banding,
//nothing else - no stars (Stars.fxh is deliberately not included). A starfield is space's, the Moon's and
//the aurora's look; this scene's void is meant to read as "nothing drawn" rather than as another night sky.
//
//Deliberately backdrop-only (see GridSceneConfig's own class doc and "The Grid" in docs/scenes.md): the
//balls, the island and the gun stay on the ordinary lit InstancedModel.fx path, tinted only by this scene's
//own light rig - the issue's own recommended default, and the smaller, cheaper, more consistent change.
//
//Credit, in the order it matters to this scene (the full record is issue #393): the backlit optical-printer
//compositing process that gave TRON (1982) its glowing costumes is the literal model for the shading rule
//here - a near-black body lit only by its own bright seams, the grid lines and the Hilbert trace standing
//in for a costume's panel lines, fed to the same bloom (PostProcessPipeline/GLARE_THRESHOLD) every other
//emissive surface in this game already blooms through. MAGI/SynthaVision's combinatorial-solid vocabulary
//is this project's own procedural-mesh house style already (BoxMesh, LatheMesh, MeshBuilder), and this
//scene adds no new geometry to it. Robert Abel and Associates' circuit-patterned digital terrain flyovers
//are the direct ancestor of the Hilbert tracery below.

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
float GridHorizonHazeDistance;

float3 GridBodyColor;
float3 GridLineColor;
float3 GridAccentColor;
float3 VoidColor;

//How many grid cells make one repeat of the Hilbert tile - 2^GRID_HILBERT_BITS. A shader constant rather
//than a uniform: it sizes a fixed unrolled loop (GridHilbertIndex below), and the one live-tunable "zoom"
//on this pattern is GridCellSize, which needs no recompile. 6 bits = a 64-cell tile; at the shipped
//GridCellSize (8 units) that is a 512-unit repeat, comfortably under this scene's own extent, so two or
//three tiles cross the visible floor - echoing a real circuit board's own repeating unit cells rather than
//tracing one curve across the whole ground, which would either be too coarse to read as a curve up close
//or too fine to read as one from a distance.
#define GRID_HILBERT_BITS 6
#define GRID_HILBERT_N (1 << GRID_HILBERT_BITS)

//Proper (always non-negative) modulo, for tiling the Hilbert pattern across a floor far larger than one
//tile: HLSL's % keeps the sign of its dividend, so a plain % on a negative cell coordinate would hand
//GridHilbertIndex a negative x or y and read outside the curve it was built for. A mask rather than a
//real modulus — correct ONLY because n is a power of two (GRID_HILBERT_N always is): two's-complement
//AND against n-1 extracts exactly the low bits, which is the non-negative floored result for any sign of
//x, and is close to free where an integer % measurably is not (HLSL X3556).
int GridMod(int x, int n)
{
    return x & (n - 1);
}

//The classic Hilbert-curve index of a cell (Wikipedia's xy2d, unrolled to GRID_HILBERT_BITS iterations of
//integer ALU only). x and y are first folded into [0, GRID_HILBERT_N) so the pattern tiles - the fold is
//what makes this well-defined for a floor that reaches far past one tile. Offset by half the tile before
//folding, so the tile's CENTRE sits at the world origin instead of its seam: a Hilbert curve does not
//wrap (index 0 and index N*N-1 are not generally adjacent cells), so the fold is a real discontinuity in
//the trace at every tile boundary, and without this offset that seam runs in a cross straight through the
//arena - the one place every scene is actually looked at from.
int GridHilbertIndex(int cellX, int cellZ)
{
    int x = GridMod(cellX + GRID_HILBERT_N / 2, GRID_HILBERT_N);
    int y = GridMod(cellZ + GRID_HILBERT_N / 2, GRID_HILBERT_N);
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

//An antialiased "how close is this pixel to a line" mask, 0 off a line and 1 dead on it, softened over one
//pixel's own world-space footprint so a distant line does not crawl. Width is a parameter rather than
//always GridLineWidth so the Hilbert trace can draw wider than the plain grid (below) - the same reason a
//real circuit board's traces read as traces: bolder than the background lattice, not just a different
//colour on it.
float GridLineMask(float dist, float footprint, float width)
{
    float halfWidth = width * 0.5;
    return 1.0 - smoothstep(halfWidth - footprint, halfWidth + footprint, dist);
}

float4 GridFloor(GridVertexOutput input)
{
    float3 worldPosition = input.WorldPosition;

    clip(length(worldPosition.xz) - IslandHoleRadius);

    float2 cell = worldPosition.xz / GridCellSize;
    int cx = (int) floor(cell.x);
    int cz = (int) floor(cell.y);
    float fx = cell.x - cx;
    float fz = cell.y - cz;

    //Distance to the nearest line in each axis, and which neighbouring cell that line separates this one
    //from - a select, not a branch: both arms cost the same, so there is nothing a real branch would save.
    float vDist = min(fx, 1.0 - fx) * GridCellSize;
    float hDist = min(fz, 1.0 - fz) * GridCellSize;
    int nx = cx + (fx < 0.5 ? -1 : 1);
    int nz = cz + (fz < 0.5 ? -1 : 1);

    int selfIndex = GridHilbertIndex(cx, cz);
    int vNeighbourIndex = GridHilbertIndex(nx, cz);
    int hNeighbourIndex = GridHilbertIndex(cx, nz);

    //On the Hilbert path if the two cells the line separates are CONSECUTIVE along the curve - the literal
    //definition of an edge of that curve, read off the two indices rather than drawn from a traced path.
    float vAccent = abs(selfIndex - vNeighbourIndex) == 1 ? 1.0 : 0.0;
    float hAccent = abs(selfIndex - hNeighbourIndex) == 1 ? 1.0 : 0.0;

    //The width itself is accent-dependent, selected before the mask rather than after: a wider AND
    //brighter trace reads clearly from play distance, where a same-width trace only a different colour
    //measured too subtle in a first capture to call a feature at all.
    float vWidth = lerp(GridLineWidth, GridLineWidth * GridAccentWidthScale, vAccent);
    float hWidth = lerp(GridLineWidth, GridLineWidth * GridAccentWidthScale, hAccent);

    float footprint = length(fwidth(worldPosition.xz));
    float vLine = GridLineMask(vDist, footprint, vWidth);
    float hLine = GridLineMask(hDist, footprint, hWidth);

    float lineGlow = max(vLine, hLine);
    float accentGlow = max(vLine * vAccent, hLine * hAccent);

    float3 lineColor = lerp(GridLineColor, GridAccentColor, saturate(accentGlow / max(lineGlow, 1e-5)));
    float3 color = lerp(GridBodyColor, lineColor, lineGlow);

    //Fade to the void at the horizon - a flat floor with a hard edge at the far plane reads as a wall, not
    //a vanishing point, so this is what lets the grid recede into the sky instead.
    float dist = distance(CameraPosition, worldPosition);
    float haze = saturate(dist / GridHorizonHazeDistance);
    color = lerp(color, VoidColor, saturate(haze * haze * 1.15));

    return float4(color, 1.0);
}

float4 GridTerrainPS(GridVertexOutput input) : COLOR { return GridFloor(input); }

//--- Sky: the plainest of the five sky-replacing passes - a flat void and a dither, nothing else ----------

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
