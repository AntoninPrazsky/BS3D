//The Grid's light cycles (#956): the owner, "for fun ... a few light cycles to pay homage to this iconic film, in the same
//style as the film's original renders". LightCycles rides three of them along the floor's lattice in right-angled runs,
//each trailing a wall of light.
//
//THE CYCLE. The scene's rule (docs/scenes/grid.md): light comes only from the seams. A near-black body of boolean
//primitives - boxes for the body and the cowl, discs for the wheels, as the film built its vehicles (#393) - one flat value
//a face, every edge of every box glowing in the cycle's colour past the glare threshold, the wheels' rims and hubs ringed in
//light. Each vertex carries its face-local position: a quad's (u, v, width, height), or a disc's (x, y, radius, -1).
//
//THE TRAIL. A wall standing on the run behind the cycle, added over the frame and writing no depth: brightest along its top
//and its foot, a translucent glow between, fading to nothing towards its tail.
//
//Drawn in the Game and the Testbed through the shared SceneRenderer (GridBackdrop). Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 World;
float4x4 View;
float4x4 Projection;
float3 CameraPosition;

float3 CycleColor;       //the rider's colour, linear, past the glare threshold
float3 BodyColor;        //the near-black body (GridTowerConfig's)
float3 FaceLight;        //the direction a face is lit from, and the floor under N.L
float FaceShadeFloor;

static const float EDGE_WIDTH = 0.07;      //a seam's half-width, world units
static const float HUB_SHARE = 0.38;       //a wheel's hub ring, as a share of its radius
static const float TRAIL_RADIANCE = 0.9;   //the wall's middle; its top and foot run brighter
static const float TRAIL_EDGE = 0.10;      //the bright top and foot, as a share of the wall's height

struct CycleVertexInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float4 Face : TEXCOORD0;
};

struct CycleVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Normal : TEXCOORD0;
    float4 Face : TEXCOORD1;
};

CycleVertexOutput CycleVS(CycleVertexInput input)
{
    CycleVertexOutput output;
    float4 world = mul(float4(input.Position, 1.0), World);
    output.Position = mul(mul(world, View), Projection);
    output.Normal = mul(input.Normal, (float3x3)World);
    output.Face = input.Face;
    return output;
}

float4 CyclePS(CycleVertexOutput input) : COLOR
{
    float3 n = normalize(input.Normal);

    //How far the point is from the nearest seam: a quad's four borders, or a disc's rim and hub. Both evaluated, then the
    //one the face is picked, so the footprint's derivatives are taken the same on every pixel
    float2 uv = input.Face.xy;
    float2 size = abs(input.Face.zw);
    float quad = min(min(uv.x, size.x - uv.x), min(uv.y, size.y - uv.y));
    float r = length(input.Face.xy);
    float disc = min(abs(input.Face.z - r), abs(r - input.Face.z * HUB_SHARE));
    float seamDistance = input.Face.w < 0.0 ? disc : quad;
    float footprint = max(fwidth(seamDistance), 1e-4);
    float seam = 1.0 - smoothstep(EDGE_WIDTH, EDGE_WIDTH + footprint * 1.5, seamDistance);

    float3 body = BodyColor * (FaceShadeFloor + (1.0 - FaceShadeFloor) * saturate(dot(n, normalize(FaceLight))));
    return float4(lerp(body, CycleColor, seam), 1.0);
}

struct TrailVertexInput
{
    float3 Position : POSITION0;
    float2 Data : TEXCOORD0;   //(age: 0 at the cycle to 1 at the tail, height: 0 at the foot to 1 at the top)
};

struct TrailVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Data : TEXCOORD0;
};

TrailVertexOutput TrailVS(TrailVertexInput input)
{
    TrailVertexOutput output;
    output.Position = mul(mul(float4(input.Position, 1.0), View), Projection);
    output.Data = input.Data;
    return output;
}

float4 TrailPS(TrailVertexOutput input) : COLOR
{
    float age = input.Data.x;
    float height = input.Data.y;
    float edges = smoothstep(1.0 - TRAIL_EDGE, 1.0, height) + (1.0 - smoothstep(0.0, TRAIL_EDGE, height));
    float fade = pow(saturate(1.0 - age), 1.5);
    //Alpha 1: BlendState.Additive scales the source by its alpha (SourceAlpha, One)
    return float4(CycleColor * (TRAIL_RADIANCE * (0.35 + 1.2 * edges) * fade), 1.0);
}

technique LightCycleBody
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CycleVS();
        PixelShader = compile PS_SHADERMODEL CyclePS();
    }
};

technique LightCycleTrail
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL TrailVS();
        PixelShader = compile PS_SHADERMODEL TrailPS();
    }
};
