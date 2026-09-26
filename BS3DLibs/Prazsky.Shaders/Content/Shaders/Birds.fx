//Birds circling over the savanna, the dunes and the outback: a small flock of soaring raptors, the only
//living things in this project's world. They were camera-facing billboards whose pixel shader drew the whole
//bird - a dark silhouette with a sin() flap - until #235, and both halves of that read wrong: a flat quad
//shows one silhouette however the camera moves, so a bird could never bank into the turn it was flying, and
//its wings could only ever be the straight V two line segments make. They are REAL 3D GEOMETRY now
//(BirdMesh), for the reason the acacias stopped being billboards in #202, and shaded from the scene's own
//sun and dome exactly as Acacia.fx and the terrain under it are - so a bird sits in the scene's light
//instead of being pasted over it. Still procedural, still no sprite sheet.
//
//THE SHAPE IS THE REST POSE AND THE FLAP IS ENTIRELY HERE. The mesh is one shared static buffer and every
//bird is a draw with its own World, FlapPhase and FlapAmount; the C# side circles the birds, banks them and
//decides when one beats its wings and when it glides. Each vertex carries a pair the flap is computed from
//(see BirdMesh's own remarks, which are the other half of this contract):
//
//    Data.x  the SIGNED spanwise station, -1 at the left wing tip through 0 at the spine to +1 at the right.
//            Its magnitude is how far out the flap has travelled; its sign is which way that wing lifts.
//            Body and tail vertices carry 0, so they take no flap without needing a flag of their own.
//    Data.y  the vertex's distance FORWARD of the wing's mean line. The twist is a rotation about that line
//            and this is all such a rotation needs, so the shader is never told where the line runs.
//
//Drawn in both executables through the shared SceneRenderer (the map editor builds this file out of this
//directory too). Shader Model 5.0, no OPENGL branch.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

float4x4 World;      //per bird: its place, its heading and its bank, with the wingspan folded in
float4x4 View;
float4x4 Projection;

//The scene's own light, taken exactly as Acacia.fx takes it: the sun dotted with the surface normal, and the
//dome's ambient split zenith-to-horizon by the normal's height.
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//The bird's albedo - a dark plumage, NOT a finished radiance. Seen from below against a bright sky the
//ambient term alone brings it out near black, which is what a backlit bird is; the sun is what puts a flash
//on its back when it banks over.
float3 BirdColor;

//Where this bird is in its wingbeat, and how much of a wingbeat it is doing at all (0 would be a wing frozen
//at its gliding dihedral). The C# side holds both, because when a bird beats and when it glides is a
//property of the bird, not of the shape.
float FlapPhase;
float FlapAmount;

//The shallow V a soaring bird holds, and how far the beat swings the tips around it.
static const float REST_DIHEDRAL = 0.20;
static const float FLAP_AMPLITUDE = 0.70;

//How far the beat lags by the time it reaches the tip. THIS is what makes a wing read as a wing rather than
//as a hinged plank: the stroke travels out along the span as a wave, so the tip is still finishing the last
//stroke while the shoulder starts the next.
static const float SPAN_LAG = 0.85;

//How hard the stroke is skewed. (theta - K*sin(theta)) is monotonic for K < 1, so it retimes the beat -
//downstroke fast and powerful, recovery slow - without ever running it backwards or leaving [-1,1].
static const float STROKE_SKEW = 0.45;

//How much of the angle the inner wing takes. The rest goes as t*t, so every station turns by its own amount
//and the wing comes out an ARC. A single angle applied along the whole span is exactly the straight V that
//#235 was about.
static const float BEND_INNER = 0.40;

//The wrist flexes on the way up: the hand pulls in and sweeps back so the wing does not beat rigid. Outboard
//of WRIST only, since the arm does not fold.
static const float WRIST = 0.45;
static const float FOLD_SPAN = 0.17;
static const float FOLD_SWEEP = 0.055;

//How far the hand pitches nose-down as it comes down - which is what a wing does to make thrust, and what
//keeps the downstroke from reading as a slap.
static const float TWIST = 0.30;

//The body rises and falls against its own wings. A body pinned dead still while the wings work is the
//giveaway of a puppet; the offset is where in the beat the rise peaks.
static const float BODY_LIFT = 0.030;
static const float BODY_LIFT_PHASE = 1.6;

struct BirdVertexInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 Data : TEXCOORD0; //(signed spanwise station, distance forward of the wing's mean line)
};

struct BirdVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldNormal : TEXCOORD0;
};

float3 RotateAboutSpan(float3 v, float c, float s)
{
    return float3(v.x, v.y * c - v.z * s, v.y * s + v.z * c);
}

float3 RotateAboutBody(float3 v, float c, float s)
{
    return float3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
}

BirdVertexOutput BirdVS(BirdVertexInput input)
{
    BirdVertexOutput output;

    float3 p = input.Position.xyz;
    float3 n = input.Normal;

    float span = input.Data.x;
    float chord = input.Data.y;
    float t = abs(span);

    //The beat at THIS station, lagged by how far out along the wing it is and skewed so the downstroke is
    //the fast half.
    float theta = FlapPhase - SPAN_LAG * t;
    float shaped = theta - STROKE_SKEW * sin(theta);
    float beat = sin(shaped);

    //The angle this station turns through. bend() grows faster than t does, so the wing is an arc; sign()
    //is what sends both wings up together, since the two sides turn opposite ways about the same axis.
    float bend = BEND_INNER * t + (1.0 - BEND_INNER) * t * t;
    float angle = (REST_DIHEDRAL + FLAP_AMPLITUDE * FlapAmount * beat) * bend * sign(span);

    //The wrist's flex, strongest with the wing high - a bird folds its hand on the recovery so it is not
    //pushing the air it just moved back down again.
    float fold = saturate(beat) * FlapAmount * smoothstep(WRIST, 1.0, t);
    p.x *= 1.0 - FOLD_SPAN * fold;
    p.z += FOLD_SWEEP * fold;

    //The twist, a rotation about the wing's mean line. The vertex carries its own distance from that line,
    //which is the whole of what placing the axis needs - and because it is expressed in that distance rather
    //than as a rotation about a signed axis, the same two lines pitch BOTH wings nose-down.
    float twist = TWIST * FlapAmount * t * cos(shaped);
    float twistSin, twistCos;
    sincos(twist, twistSin, twistCos);
    p.y += chord * twistSin;
    p.z += chord * (1.0 - twistCos);
    n = RotateAboutSpan(n, twistCos, twistSin);

    //The dihedral itself. The normal takes this and the twist, but NOT the spanwise gradient of bend(): the
    //animated wing is a helicoid and its true normal leans a few degrees further, which is under the noise
    //floor on a bird whose albedo is near black against a bright sky.
    float flapSin, flapCos;
    sincos(angle, flapSin, flapCos);
    p = RotateAboutBody(p, flapCos, flapSin);
    n = RotateAboutBody(n, flapCos, flapSin);

    p.y += BODY_LIFT * FlapAmount * sin(FlapPhase + BODY_LIFT_PHASE);

    float4 worldPosition = mul(float4(p, 1.0), World);
    output.Position = mul(mul(worldPosition, View), Projection);
    output.WorldNormal = mul(n, (float3x3)World);

    return output;
}

float4 BirdPS(BirdVertexOutput input, bool isFrontFace : SV_IsFrontFace) : COLOR
{
    //Two-sided: the wings, the primaries and the tail are single sheets drawn with CullNone, so the side
    //that is lit is whichever one is turned towards the camera. A wing seen from underneath must take the
    //ground's dim half of the sky, not the sun that is on its back.
    float3 N = normalize(input.WorldNormal);
    N = isFrontFace ? N : -N;

    float3 ambient = lerp(HorizonColor, ZenithColor, saturate(N.y * 0.5 + 0.5));
    float ndotl = saturate(dot(N, SunDirection));

    return float4(BirdColor * (ambient + SunColor * ndotl), 1.0);
}

technique Birds
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BirdVS();
        PixelShader = compile PS_SHADERMODEL BirdPS();
    }
};
