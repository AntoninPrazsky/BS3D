//Broken cumulus for the storm scene (#219): cloud MASSES standing in open air around and below the arena,
//with sky between them, rather than a surface anyone stands on.
//
//⚠ WHY THIS REPLACED A HEIGHT FIELD, and it is the whole point of the scene. The first build drew the storm
//as a displaced grid - the same camera-centred terrain mesh every ground scene uses - and it read as
//landscape from every camera the game has. Three rounds of fixing its SHAPE (a mesa profile made bulbous,
//ridged noise turned back into billow noise, the relief coarsened) each made it a better landscape and none
//of them made it cloud, because the fault was never the shape:
//
//  * a height field is a SURFACE. It has one height per XZ, so it can be lumpy but it can never be broken,
//    and a white lumpy opaque surface is a snowfield. "Torn" cumulus with sky showing between the cells is
//    not expressible in it at all;
//  * seen from a camera five units above it, any such surface is being looked at from ON it, which is the
//    geometry of standing on ground - no shading undoes that;
//  * and its SILHOUETTE is a geometric edge, hard against the sky. Cloud reads as cloud because its edge
//    dissolves. That is a property of the medium, not of the outline.
//
//So the clouds are volume now: soft-edged billboard puffs clustered into cumulus cells, alpha-blended, with
//real gaps between the cells. Each puff shades as a little SPHERE rather than as a flat sprite (the disc's
//own offset gives the normal), so a mass lit from one side has a bright flank and a shaded one and reads as
//a body with volume. The pattern - a static vertex buffer of quads turned to face the camera in the vertex
//shader - is the sea's spray and the mountain's snow, and #151 measured 2000 of those at exactly nothing.
//
//Everything is written in LINEAR RADIANCE into the HDR target. Built by all three executables out of this
//directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;

//The camera, and its right/up in world space for turning each quad to face it.
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

//Towards the sun, and the sun's own radiance, tinted by the dome like every other scene's.
float3 SunDirection;
float3 SunColor;

//The current dome's gradient in LINEAR radiance - zenith overhead, horizon at the skyline.
float3 ZenithColor;
float3 HorizonColor;

//Cloud reflectance (linear): a puff's sunward crown, and the deep blue-grey its underside carries.
float3 TopColor;
float3 BaseColor;

//How much of the sky's hemisphere light fills the cloud, how strongly a rim lit from behind silvers, and
//how opaque a single puff is at its middle.
float AmbientStrength;
float SilverStrength;
float PuffOpacity;

//How hard a puff's edge falls off. Low is a hard-edged ball (a sprite); high is a wisp with no body left.
float EdgeSoftness;

//The vertical extent the field occupies, so a puff can be shaded by where it stands in the layer: the tops
//take the sun, the bottoms sit in their own shadow. This is what a storm's own darkness is made of.
float LayerBottomY;
float LayerTopY;
float UnderShade;

//The lightning, as the host solved it this frame: the strike's 0..1 envelope, where its cell stands in XZ,
//its colour, how brightly it lights the cloud from inside and how far that reaches.
float FlashEnvelope;
float2 FlashCenterXZ;
float3 FlashColor;
float FlashGlow;
float FlashReach;

//Which strike is running, as the period's own index. The channel's whole path is hashed off it, so the bolt
//is frozen for the length of one flash and is a different one the next time - the same rule the envelope's
//size and the strike's placement already follow.
float FlashStrikeIndex;

//How far the field melts into the skyline, and what the haze is MADE of. Not the dome's horizon colour by
//itself: the storm's own note records that fading to a sandy horizon painted the whole scene as desert.
float3 HazeTint;
float HorizonHazeDistance;
float HazeStrength;

//The wind the field drifts on, and the clock it runs off.
float2 WindDirection;
float CloudTime;
float DriftSpeed;

//How much a puff shades as part of its CELL rather than as its own sphere. See StormCloudsConfig.
float MassNormalMix;

struct CloudVertexInput
{
    float3 Centre : POSITION0;

    //(corner x, corner y, radius, seed). The corner is the unit quad's own -1..1 offset, which the pixel
    //shader reads back as the puff's disc coordinate - so one attribute carries both the billboard and the
    //shading frame.
    float4 Data : TEXCOORD0;

    //The middle of the cell this puff belongs to. The bolt technique reuses this layout and leaves it zero.
    float3 MassCentre : NORMAL0;
};

struct CloudVertexOutput
{
    float4 Position : SV_POSITION;
    float3 WorldCentre : TEXCOORD0;
    float3 WorldPosition : TEXCOORD1;
    float3 Corner : TEXCOORD2;   //(corner x, corner y, seed)
    float2 Depth : TEXCOORD3;    //(distance to camera, the puff's own place in the layer 0..1)
    float3 MassNormal : TEXCOORD4;
};

CloudVertexOutput CloudVS(CloudVertexInput input)
{
    CloudVertexOutput output;

    float2 corner = input.Data.xy;
    float radius = input.Data.z;
    float seed = input.Data.w;

    //The field drifts downwind bodily. It is generated far wider than the far plane, so nothing has to wrap
    //- a wrap would tear a mass in half, which is the one artefact a cloud cannot survive.
    float3 centre = input.Centre;
    centre.xz += WindDirection * (CloudTime * DriftSpeed);

    //And it breathes: each puff rises and falls on its own phase, by a tenth of its own radius. Enough that
    //a mass is never quite still, far too little to read as motion.
    centre.y += sin(CloudTime * 0.21 + seed * 37.0) * radius * 0.10;

    float3 world = centre + (CameraRight * corner.x + CameraUp * corner.y) * radius;

    //The direction out of the CELL this puff belongs to. Computed here, per puff, rather than in the pixel
    //shader: it is constant across a quad, and it is what lets the cell shade as one body.
    float3 fromMass = centre - (input.MassCentre + float3(WindDirection * (CloudTime * DriftSpeed), 0.0).xzy);
    output.MassNormal = normalize(fromMass + float3(0.0, 0.001, 0.0));

    output.WorldCentre = centre;
    output.WorldPosition = world;
    output.Corner = float3(corner, seed);
    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Depth = float2(distance(CameraPosition, world),
        saturate((centre.y - LayerBottomY) / max(LayerTopY - LayerBottomY, 1e-3)));

    return output;
}

float4 CloudPS(CloudVertexOutput input) : COLOR
{
    float2 corner = input.Corner.xy;
    float seed = input.Corner.z;

    //⚠ THE OUTLINE HAS TO BE BROKEN OR EVERY PUFF IS A DISC, and a field of discs reads as sprites however
    //well it is shaded. One turn of gradient noise around the quad, on the puff's own seed, so no two are
    //cut the same way. It runs on the CORNER and not on world space: the noise then rides with the billboard
    //instead of swimming across it as the camera turns.
    float wobble = GradientNoise2(corner * 1.9 + seed * 53.0) * 0.55
                 + GradientNoise2(corner * 4.7 + seed * 91.0) * 0.22;
    float r2 = dot(corner, corner) * (1.0 + wobble);

    //The soft edge. `EdgeSoftness` is where the falloff STARTS, so under it the puff has a solid middle and
    //over it it is all fringe: this is the dial that decides body against wisp.
    float body = smoothstep(1.0, EdgeSoftness, r2);
    clip(body - 0.004);

    //--- The puff as a sphere -------------------------------------------------------------------------
    //A billboard has no normal of its own, so a flat one shades as a coin and a field of them reads as
    //paper. Reconstructing the SPHERE the disc is a section of costs one square root and is the whole
    //difference: the offset from the middle is the normal's own tangential part, and what is left over
    //along the view axis is the rest of it.
    float3 towardsEye = normalize(CameraPosition - input.WorldCentre);
    float z = sqrt(saturate(1.0 - saturate(r2)));
    float3 sphere = normalize(CameraRight * corner.x + CameraUp * corner.y + towardsEye * z);

    //⚠ AND THEN BLENDED TOWARDS THE CELL'S OWN NORMAL, which is what stops the field reading as bubble
    //wrap. A puff shaded from its own disc alone gets a full light-to-dark gradient across it and a crisp
    //circular edge, so a cell built of them is a heap of glossy balls - the first thing the eye names.
    //Mixing in the direction out of the cell's middle makes the CELL the thing being lit, which is what it
    //is, and lets the individual puffs disappear into it.
    float3 normal = normalize(lerp(sphere, input.MassNormal, MassNormalMix));

    //--- Colour --------------------------------------------------------------------------------------
    float upFacing = saturate(normal.y * 0.5 + 0.5);

    //A cloud has no colour of its own: its crown is the colour of the sun and its underside the colour of
    //the sky. Both splits are WIDE, for the reason the sky's own deck records - ACES eats cloud contrast,
    //so two linear values close together in the highlights tonemap to the same white.
    float3 cloud = lerp(BaseColor, TopColor, upFacing * upFacing);

    float ndotl = saturate(dot(normal, SunDirection));
    float3 skyAmbient = lerp(HorizonColor, ZenithColor, upFacing);

    float3 color = cloud * (SunColor * ndotl + skyAmbient * AmbientStrength);

    //Where the puff stands in the layer. A storm is dark UNDERNEATH because the cloud above it is in the
    //way, and that is a property of the whole field rather than of any one puff - so it is taken from the
    //puff's own height and not from its normal.
    color *= lerp(UnderShade, 1.0, input.Depth.y * input.Depth.y);

    //THE SILVER LINING. Cloud is strongly forward-scattering, so a rim with the sun behind it is the
    //brightest thing in the sky - the single cue that separates a cloud from a hill of grey stone. It rides
    //the puff's own edge, which is where a billboard's optical depth is least.
    float towardsSun = saturate(dot(-towardsEye, SunDirection));
    color += TopColor * SunColor * (SilverStrength * saturate(r2) * pow(towardsSun, 4.0));

    //--- The flash -----------------------------------------------------------------------------------
    //Lightning lights cloud FROM INSIDE: a whole cell goes translucent for a beat. So this is emissive and
    //gated on distance from the strike, never a light with a normal - a normal-lit flash reads as a second
    //sun. Behind a [branch] on a uniform: non-divergent, and no gradient operation inside, so it is
    //derivative-safe (BestPractices.md's rule).
    [branch]
    if (FlashEnvelope > 0.0)
    {
        float toStrike = length(input.WorldCentre.xz - FlashCenterXZ);
        float near = saturate(1.0 - toStrike / max(FlashReach, 1e-3));

        //Squared, so it is a cell lighting up rather than the whole sky brightening, and weighted onto the
        //part the sun is NOT already lighting - which is where a discharge inside the cloud shows.
        float shaded = 0.30 + 0.70 * (1.0 - ndotl);

        color += FlashColor * (FlashGlow * FlashEnvelope * near * near * shaded);
    }

    //--- The air -------------------------------------------------------------------------------------
    //Aerial perspective, so the far cells sit behind air instead of being cut out of the sky. The eighth
    //power is the tropical beach's own correction: a scene whose horizon is itself the subject would be
    //half hazed at the skyline under a fourth.
    float haze = saturate(input.Depth.x / max(HorizonHazeDistance, 1e-3));
    color = lerp(color, HazeTint, pow(haze, 8.0) * HazeStrength);

    return float4(color, body * PuffOpacity);
}

//=====================================================================================================
//The discharge
//=====================================================================================================
//
//The bolt itself, which #219 asked for in as many words: without it the flash is light with no source in
//the frame, and that reads as the sun blinking rather than as lightning.
//
//The channel is generated ENTIRELY IN THE VERTEX SHADER off the strike's own period index. Nothing is
//uploaded per frame and nothing is stored: the buffer carries only which bolt a vertex belongs to, how far
//down it stands and which side of the channel it is - the path is hashed, the same way every other thing
//about a strike in this scene already is, so the Game, the Testbed and the map editor draw the same bolt at
//the same second.
//
//The vertex data is packed into the puffs' own layout, since the two differ in nothing but meaning:
//  Centre = (bolt index, t along the channel 0..1, side -1/+1), Data unused.

float3 BoltColor;
float BoltWidth;

//Where a bolt's channel stands at parameter t: a straight fall from the cloud tops to well below the field,
//kinked by two turns of hashed noise. The coarse turn is the bolt's overall lean and the fine one is the
//zig-zag; both are frozen per strike, so a channel does not writhe within its own flash.
float3 BoltPoint(float bolt, float t, float strike)
{
    float2 seed = float2(bolt * 17.3 + strike * 3.1, strike * 7.7);

    //Each bolt leaves the strike's own cell on its own bearing, so a fork spreads rather than doubling up.
    float2 spread = (NoiseHash22(seed + 41.0)) * 26.0;

    //⚠ The channel has to run through the band the CELLS actually occupy, not from the top of the world to
    //the bottom of it. Its first cut ran from above the arena down past the whole field, which put its
    //brightest end in clear air over the island and its tail below anything the camera can see - so what
    //reached the frame was a white line crossing the arena rather than a discharge inside the weather.
    float top = LayerTopY + 8.0;
    float bottom = LayerBottomY + 18.0;

    //A branch stops short: only the first channel runs the whole way down.
    float reach = bolt < 0.5 ? 1.0 : 0.34 + 0.42 * frac(NoiseHash22(seed + 73.0).x * 0.5 + 0.5);
    float u = t * reach;

    float3 p;
    p.y = lerp(top, bottom, u);
    p.xz = FlashCenterXZ + spread * u;

    //The kinks. Amplitude falls off towards the top, so the channel leaves the cloud roughly where the glow
    //is and gets wilder as it runs - which is the way a stepped leader actually looks.
    float2 coarse = NoiseHash22(seed + floor(u * 4.0) * 13.7 + 5.0);
    float2 fine = NoiseHash22(seed + floor(u * 14.0) * 29.3 + 11.0);

    p.xz += coarse * (14.0 * u) + fine * (5.5 * u);

    return p;
}

struct BoltVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Along : TEXCOORD0;   //(t along the channel, side -1..1)
};

BoltVertexOutput BoltVS(CloudVertexInput input)
{
    BoltVertexOutput output;

    float bolt = input.Centre.x;
    float t = input.Centre.y;
    float side = input.Centre.z;

    //The same period index the envelope and the strike's own placement are hashed off. Floor of time over
    //the period is all it is; the host cannot pass an integer through a float uniform any more cheaply.
    float strike = FlashStrikeIndex;

    float3 centre = BoltPoint(bolt, t, strike);

    //Widened across the channel's own screen-space run, so it keeps its width whichever way it kinks. A
    //billboarded ribbon rather than a tube: a lightning channel is a filament far thinner than one pixel at
    //this distance, and what is being drawn is its glare, not its body.
    float3 ahead = BoltPoint(bolt, saturate(t + 0.04), strike) - centre;
    float3 towardsEye = normalize(CameraPosition - centre);
    float3 across = normalize(cross(normalize(ahead + float3(1e-4, 0, 0)), towardsEye));

    //Tapering: a channel is thickest where it leaves the cloud and thins as it runs.
    float width = BoltWidth * lerp(1.25, 0.35, t);

    float3 world = centre + across * (side * width);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Along = float2(t, side);

    return output;
}

float4 BoltPS(BoltVertexOutput input) : COLOR
{
    //Bright core, soft shoulder: the core is what the glare pass turns into a stroke of light, and the
    //shoulder is what stops the ribbon reading as a hard-edged strip.
    float core = 1.0 - saturate(abs(input.Along.y));
    float fall = core * core * core;

    //Fades out along its run, so a channel dies into the air below rather than stopping at a line.
    float alongFade = saturate(1.0 - input.Along.x * input.Along.x * 0.8);

    return float4(BoltColor * (fall * alongFade * FlashEnvelope), 1.0);
}

technique StormClouds
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CloudVS();
        PixelShader = compile PS_SHADERMODEL CloudPS();
    }
}

technique StormBolts
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BoltVS();
        PixelShader = compile PS_SHADERMODEL BoltPS();
    }
}
