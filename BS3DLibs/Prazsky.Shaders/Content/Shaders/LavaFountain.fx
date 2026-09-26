//The volcano's lava fountains and its smoke plume (#223): one static buffer of billboards, every particle
//on a real BALLISTIC arc computed in the vertex shader from the vent it was thrown out of, so a jet tapers,
//falls back and lands instead of streaming upwards forever. Additive and well over 1 in radiance, so the
//glare pass blooms the jets for free - Flame.fx's answer for the campfire, scaled up and thrown.
//
//Separate techniques over the ONE buffer rather than one technique with a mode switch: the host draws the
//first slice of the buffer as smoke and the rest as lava, so neither pass pays for the other's particles and
//neither carries a runtime branch (docs/rendering.md's measured rule for this project's heavy passes). They
//also want different blending - smoke occludes, lava adds - which a single pass could not give either. The
//third, the glow over the crater (#509), is one quad: the buffer's first, whose corners are all it reads.
//
//The eruption itself is a uniform, not state: the host computes one deterministic envelope off the wall
//clock (SceneRenderer.VolcanoEruption) and hands the same figure to the jets, to the plume and to the
//crater's point light (and, since #509, the glow over the crater and the one on the cloud deck), so a burst
//lights the flank, throws higher and thickens the column as one event.
//
//Since #509 both are drawn from references, and both changed shape. A blob of the jets is a STREAK along its
//own velocity - every fountain the references drew was a spray of bright arcs, which is what an eye sees of
//spatter moving that fast, and a field of round dots read as confetti. A puff of the plume is a lumpy sphere
//lit from below by the crater and from above by nothing much, and the column spreads into a head that the
//wind takes - where it had been a tube of soft grey discs that read as steam from a chimney.
//
//Shader Model 5.0, drawn in all three executables out of the one copy in Prazsky.Shaders.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 CameraRight;
float3 CameraUp;

float FountainTime;

//Matched by MAX_VENTS in SceneRenderer. Vent 0 is the crater; the rest are side vents on the flank, each
//with its own strength so they do not all throw the same fountain.
#define MAX_VENTS 4
float3 VentPosition[MAX_VENTS];
float VentStrength[MAX_VENTS];
int VentCount;

float LaunchSpeed;
float LaunchSpread;
float BlobGravity;
float BlobLife;
float BlobSize;
float2 WindDirection;
float WindDrag;

//How long an exposure the eye takes of a blob in flight, in seconds: a streak's length is its speed across
//the line of sight times this.
float StreakTime;

//0 between bursts, up to 1 at the peak of one. Multiplied into the launch speed by EruptionBoost and into
//the plume's density, so a burst is visible as reach and as volume rather than only as brightness.
float Eruption;
float EruptionBoost;

float3 LavaHot;
float3 LavaCool;

float3 PlumeColor;
float PlumeStrength;
float PlumeGlow;

struct FountainVertexInput
{
    float4 Position : POSITION0; //Per-particle randoms, a fixed point in the unit cube
    float3 Data : TEXCOORD0;     //(corner x, corner y in {-1,1}, one more per-particle random)
};

struct FountainVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;
    float3 Color : TEXCOORD1;
    float Alpha : TEXCOORD2;
};

//Four decorrelated randoms out of the four the vertex carries. Hashed rather than used raw because the
//buffer's own values are needed for more roles than it has channels, and a particle whose size says
//something about its direction reads as a pattern the moment two of them are in shot.
void ParticleRandoms(float3 b, float rand, out float2 aim, out float2 shape)
{
    aim = NoiseHash22(float2(b.x, b.z) * 37.1) * 0.5 + 0.5;
    shape = NoiseHash22(float2(b.y, rand) * 61.7) * 0.5 + 0.5;
}

//--- The jets ----------------------------------------------------------------------------------------------

FountainVertexOutput FountainVS(FountainVertexInput input)
{
    FountainVertexOutput output;

    float3 b = input.Position.xyz;
    float rand = input.Data.z;

    float2 aim, shape;
    ParticleRandoms(b, rand, aim, shape);

    int vent = min((int)(b.z * VentCount), VentCount - 1);

    //Each blob lives its own span and starts at its own moment, so a jet is a continuous spray rather than
    //a pulse of particles leaving together. The offset is scaled well past the life so neighbouring
    //particles in the buffer are nowhere near each other in time.
    float life = BlobLife * (0.55 + 0.9 * shape.x);
    float t = frac((FountainTime + rand * 137.0) / life) * life;
    float age = t / life;

    //A cone about the vertical, sqrt-distributed so the blobs spread evenly over the cone's solid angle
    //instead of crowding its axis.
    float azimuth = aim.x * 6.2831853;
    float tilt = LaunchSpread * sqrt(aim.y);
    float3 direction = float3(sin(tilt) * cos(azimuth), cos(tilt), sin(tilt) * sin(azimuth));

    float speed = LaunchSpeed * VentStrength[vent] * (0.55 + 0.6 * shape.y) * (1.0 + Eruption * EruptionBoost);

    //Ballistic: launched, pulled down, and leaned over by the wind - the drag term grows as t² because a
    //blob has been in the air that much longer, which is what bends a tall jet downwind at its top and
    //leaves its base standing straight.
    float3 world = VentPosition[vent] + direction * speed * t;
    world.y -= 0.5 * BlobGravity * t * t;
    world.xz += WindDirection * WindDrag * t * t;

    float size = BlobSize * (0.45 + 1.1 * shape.y) * (1.0 - 0.3 * age);

    //The velocity the position above is the integral of.
    float3 velocity = direction * speed;
    velocity.y -= BlobGravity * t;
    velocity.xz += WindDirection * (2.0 * WindDrag * t);

    //A STREAK along that velocity as the camera sees it (#509): the quad's long axis is the velocity's
    //component across the line of sight and its short axis is perpendicular to both, so a blob crossing the
    //frame draws an arc segment and one flying straight at the lens stays a round blob - which is what it
    //is from there. cross(v, toCamera) has exactly the across-the-line-of-sight speed as its length.
    float3 toCamera = normalize(CameraPosition - world);
    float3 across = cross(velocity, toCamera);
    float seen = length(across);
    float3 side = seen > 1e-3 ? across / seen : CameraRight;
    float3 along = cross(toCamera, side);

    world += side * (input.Data.x * size * 0.42) + along * (input.Data.y * (size * 0.42 + seen * StreakTime));

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Corner = input.Data.xy;

    //Thrown white-hot and cooling on the way: the colour is the age, which is what makes the top of a jet
    //darker than its root without a single extra particle. The first moments are hotter than the lava on
    //the ground - the references' fountains are yellow-white at the vent where every flow is orange.
    output.Color = lerp(LavaHot * 1.35, LavaCool, saturate(age * 1.35));
    output.Alpha = smoothstep(0.0, 0.06, age) * (1.0 - smoothstep(0.62, 1.0, age));

    return output;
}

float4 FountainPS(FountainVertexOutput input) : COLOR
{
    float r = length(input.Corner);
    float body = saturate(1.0 - r);
    body *= body;

    clip(body * input.Alpha - 0.004);

    //BlendState.Additive in MonoGame is (SourceAlpha, One), so the alpha channel is a MULTIPLIER on what
    //goes in rather than a coverage: the radiance actually added is rgb * a. Written out that way here -
    //rgb carries the blob's shape and a carries its life - so the two are not accidentally applied twice.
    return float4(input.Color * body, body * input.Alpha);
}

//--- The plume ---------------------------------------------------------------------------------------------

float PlumeRise;
float PlumeSpread;
float PlumeLife;
float PlumeSize;

struct PlumeVertexOutput
{
    float4 Position : SV_POSITION;
    float2 Corner : TEXCOORD0;
    float3 Color : TEXCOORD1; //the smoke's own colour, before its form is shaded
    float3 Glow : TEXCOORD2;  //what the crater throws up onto its underside
    float Alpha : TEXCOORD3;
    float Seed : TEXCOORD4;   //offsets the puff's own lumps, so no two share an outline
};

PlumeVertexOutput PlumeVS(FountainVertexInput input)
{
    PlumeVertexOutput output;

    float3 b = input.Position.xyz;
    float rand = input.Data.z;

    float2 aim, shape;
    ParticleRandoms(b, rand, aim, shape);

    float life = PlumeLife * (0.7 + 0.6 * shape.x);
    float t = frac((FountainTime + rand * 211.0) / life) * life;
    float age = t / life;

    //Straight up out of the crater, slowing as it goes (a column loses its momentum to the air it drags in),
    //spreading as it rises and carried downwind the whole time. The swirl is what keeps it from reading as
    //a cone of dots: each puff turns around the column's axis on its own radius and its own rate.
    //
    //The slowing has to be a SATURATION and not a subtracted t², which is what it was first: a parabola's
    //slope goes negative past its vertex, so every puff climbed, stopped and then sank back into the crater,
    //and the column ended in a ball hanging over the summit. t/(1+kt) only ever rises.
    float rise = PlumeRise * t / (1.0 + 0.075 * t);
    float swirlAngle = aim.x * 6.2831853 + t * (0.35 + 0.5 * shape.y);

    //The HEAD (#509). Every eruption column in the references is a stem that opens into a head two or three
    //times its width, and the wind takes the head: grown only linearly with age, the spread kept the column
    //a tube from the crater to the top. The square term is what opens it, and the drift's square term is
    //what bends the top downwind while the stem stands.
    float spread = 0.3 + age + 2.4 * age * age;
    float swirlRadius = PlumeSpread * (0.25 + 0.75 * aim.y) * spread;
    float drift = t * 1.6 + t * t * 0.1;

    float3 world = VentPosition[0];
    world.y += rise;
    world.x += cos(swirlAngle) * swirlRadius + WindDirection.x * drift;
    world.z += sin(swirlAngle) * swirlRadius + WindDirection.y * drift;

    float size = PlumeSize * (0.35 + 2.4 * age) * (0.6 + 0.8 * shape.y);

    world += CameraRight * (input.Data.x * size) + CameraUp * (input.Data.y * size);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Corner = input.Data.xy;
    output.Seed = rand * 173.0;

    //The young smoke still sitting in the crater's throat is lit from BELOW by what threw it; the old smoke
    //high over the summit is cold ash and nothing else. That gradient is the whole reason the column reads
    //as coming out of a volcano rather than out of a chimney. It falls off smoothly rather than stopping at
    //a third of the way up, which is where the first build cut it: the references light the whole stem and
    //the underside of the head, dimmer with height.
    float underlit = exp(-age * 4.5);
    output.Color = PlumeColor;
    output.Glow = LavaCool * PlumeGlow * underlit * (0.5 + 0.9 * Eruption);

    //Present at all times and denser in a burst. Between bursts the first build thinned it to a quarter,
    //which left a faint grey wisp over a volcano every reference drew under a column of ash. Fading in over
    //the first sixth keeps puffs from popping into existence at the vent.
    output.Alpha = PlumeStrength * (0.55 + 0.45 * Eruption)
        * smoothstep(0.0, 0.16, age) * (1.0 - smoothstep(0.62, 1.0, age));

    return output;
}

float4 PlumePS(PlumeVertexOutput input) : COLOR
{
    float2 c = input.Corner;
    float r = length(c);

    //Lumpy: the rim pulled in and out by a noise read in the puff's own frame, so a stack of puffs builds the
    //cauliflower head the references draw rather than a column of soft discs. The same value varies the
    //density inside the puff and tilts its shading, which is what makes the lumps read as lumps.
    float lump = GradientNoise2(c * 1.7 + input.Seed);
    float body = 1.0 - smoothstep(0.2, 0.8 + 0.35 * lump, r);

    float alpha = saturate(body * input.Alpha * (0.8 + 0.3 * lump));
    clip(alpha - 0.004);

    //Shaded as the sphere a puff stands for: its upper side takes what little light the night sky gives, its
    //lower side what the crater throws up. c.y is up on the screen, which is up in the world for every
    //camera this scene is looked at from.
    float up = saturate(0.5 + 0.6 * c.y + 0.25 * lump);
    float3 color = input.Color * (0.55 + 0.45 * up) + input.Glow * (1.0 - up);

    //PREMULTIPLIED, and it has to be: MonoGame's BlendState.AlphaBlend is (One, InverseSourceAlpha), so a
    //shader returning a straight colour has that colour ADDED at full strength on every layer. Smoke drawn
    //that way is not smoke - a column of 900 puffs at 0.085 grey came out as a white steam plume brighter
    //than the sky behind it, which is what sent this looking for a bug in the colour it was pushing.
    return float4(color * alpha, alpha);
}

//--- The glow ----------------------------------------------------------------------------------------------
//The fountain's incandescent heart: one soft quad over the crater, additive, brighter in a burst. Every
//eruption the #509 references drew has a blaze of light where the jets leave the vent - the air and the spray
//there are lit so densely that no single blob reads - and a few thousand streaks cannot add up to it without
//being many times more. Depth-read like the jets, so the crater's rim hides its lower half from a low camera
//and the light seems to come out of the crater, which is where it comes from.

float GlowSize;
float GlowStrength;

FountainVertexOutput GlowVS(FountainVertexInput input)
{
    FountainVertexOutput output;

    float3 world = VentPosition[0] + float3(0.0, GlowSize * 0.25, 0.0)
        + CameraRight * (input.Data.x * GlowSize) + CameraUp * (input.Data.y * GlowSize);

    output.Position = mul(mul(float4(world, 1.0), View), Projection);
    output.Corner = input.Data.xy;
    output.Color = LavaHot * GlowStrength * (0.6 + 0.8 * Eruption);
    output.Alpha = 1.0;

    return output;
}

float4 GlowPS(FountainVertexOutput input) : COLOR
{
    //A Gaussian taken down to exactly zero at the quad's rim, so no edge of the quad can ever show.
    float r2 = dot(input.Corner, input.Corner);
    float glow = exp(-r2 * 5.0) * saturate(1.0 - r2);

    clip(glow - 0.002);

    //Additive, (SourceAlpha, One): alpha 1, so what is added is exactly rgb.
    return float4(input.Color * glow, 1.0);
}

technique Fountain
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FountainVS();
        PixelShader = compile PS_SHADERMODEL FountainPS();
    }
};

technique Plume
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL PlumeVS();
        PixelShader = compile PS_SHADERMODEL PlumePS();
    }
};

technique Glow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GlowVS();
        PixelShader = compile PS_SHADERMODEL GlowPS();
    }
};
