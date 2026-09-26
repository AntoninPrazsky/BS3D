//InstancedModel.fx, part 1: what every program in the effect shares - the transforms, the mesh part's material,
//the sky's hemisphere ambient and the three-light rig, the world-space detail texture, the vertex and instance
//layouts, and MainVS, the vertex shader most techniques draw through.

//Towards the sun. The key light is positional and sits only forty units off, so its direction swings
//right across the scene - useless for a shadow that has to fall in parallel bands over a whole city.
float3 SunDirection;

float4x4 View;
float4x4 Projection;

//Absolute transform of the mesh parent bone, applied before the per-instance world matrix
float4x4 Bone;

//An object-space normal into world space through the bone and the instance's world matrix. By the COFACTOR of
//the world matrix's 3x3 rather than by the matrix itself (#569): the two agree for a rotation and a uniform
//scale, which is all most instances carry, but the shot in flight is stretched along its path
//(BallRenderSet.StretchAlong, up to 1.5x) and a building's box is scaled per axis, and a normal pushed through
//a non-uniform scale leans towards the stretched axis - 22 degrees off at the shot's full stretch. The cofactor
//is the inverse transpose times the determinant, and the result is normalised, so only the determinant's SIGN
//survives - and it is put back, so a mirrored instance (negative determinant) is not turned inside out. Three
//cross products a vertex.
float3 NormalToWorld(float3 objectNormal, float4x4 world)
{
    float3 n = mul(float4(objectNormal, 0), Bone).xyz;
    float3 r0 = world[0].xyz, r1 = world[1].xyz, r2 = world[2].xyz;
    float3 c0 = cross(r1, r2);
    float3 normal = n.x * c0 + n.y * cross(r2, r0) + n.z * cross(r0, r1);
    return normalize(dot(r0, c0) < 0.0 ? -normal : normal);
}

float3 EyePosition;

//Material of the mesh part being drawn
float4 DiffuseColor;
float3 EmissiveColor;
//Premultiplied on the CPU: ambient tint * material diffuse. Modulated per pixel by the sky hemisphere below.
float3 AmbientColor;
float3 SpecularColor;
float SpecularPower;

//Hemisphere ambient palette taken from the current sky dome: upward-facing surfaces receive SkyColor,
//downward-facing ones GroundColor. Both arrive in LINEAR radiance - Prazsky.Core.Tools.ColorSpace
//decodes them on the CPU, because the scales and tints applied to them there are multiplications and
//those mean nothing in a display encoding. Neither is clamped to 1: a bright sky does exceed white.
float3 SkyColor;
float3 GroundColor;

//Y of the ground plane, for the ground-contact part of the ambient occlusion
float GroundHeight;

//The key light is positional (a "sun" placed in the scene): its direction differs per surface point,
//so every ball is lit according to where it sits relative to the light instead of all balls looking identical.
float3 KeyLightPosition;
float3 DirLight0DiffuseColor;
float3 DirLight0SpecularColor;

float3 DirLight1Direction;
float3 DirLight1DiffuseColor;
float3 DirLight1SpecularColor;

float3 DirLight2Direction;
float3 DirLight2DiffuseColor;
float3 DirLight2SpecularColor;

//The world-space detail texture (InstancedModelTriplanar and its coarse and probe copies)
texture Texture;
sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
    //Anisotropic, not plain trilinear: a pixel on the ground seen at a grazing angle covers a long thin
    //sliver of texture, and isotropic mip selection has to pick the mip matching its *long* axis, so it
    //blurs across the short one too and the floor dissolves into a smear. The ground is the surface this
    //shows on worst, being the one the camera always looks along.
    MinFilter = Anisotropic;
    MagFilter = Linear;
    MipFilter = Linear;
    MaxAnisotropy = 16;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct InstanceInput
{
    float4 WorldRow1 : TEXCOORD1;
    float4 WorldRow2 : TEXCOORD2;
    float4 WorldRow3 : TEXCOORD3;
    float4 WorldRow4 : TEXCOORD4;
    //XYZ = world-space direction towards the instance's occluders (zero = none), W = base occlusion factor
    float4 Custom : TEXCOORD5;
    //How much of this instance has been dithered away: 0 draws it whole, positive eats it away, negative
    //fills it in. Read by the ball pattern technique alone; every other technique ignores it, and an
    //unconsumed element in the vertex layout costs nothing.
    float Dissolve : TEXCOORD6;
    //How brightly this instance is flaring as the ripple passes through it, 0 = not at all. Read by the
    //ball pattern technique alone, like Dissolve above.
    float Ripple : TEXCOORD7;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 WorldNormal : TEXCOORD1;
    float4 OcclusionData : TEXCOORD2;
};

VertexShaderOutput MainVS(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderOutput output;

    //Rows are stored in the same layout as an XNA row-major matrix, so no transpose is needed
    float4x4 world = float4x4(instance.WorldRow1, instance.WorldRow2, instance.WorldRow3, instance.WorldRow4);

    float4 worldPosition = mul(mul(input.Position, Bone), world);

    output.WorldPosition = worldPosition.xyz;
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = NormalToWorld(input.Normal, world);
    output.OcclusionData = instance.Custom;

    return output;
}
