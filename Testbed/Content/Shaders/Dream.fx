//Draws the dream: a hallucinatory skyscape - slow marbled colour flowing across the whole sphere of the
//sky, hard glassy solids that tumble, morph and melt into one another, soft luminous orbs breathing in and
//out of the murk, and fast sparks whipping between them. The tenth scene, and deliberately a scene of
//CONTRASTS: sharp against blurred (raymarched surfaces with crisp silhouettes against pure gaussian glows),
//fast against slow (the background marbling drifts over minutes while the sparks cross the sky in seconds),
//near against far. It is the picture of a hallucination the way Space.fx is the picture of space.
//
//Like Space it replaces the SKY, and everything structural follows from that: one full-screen pass over a
//quad already in normalized device coordinates, the view ray recovered per pixel through
//InverseViewProjection, drawn with the depth state off so the island, the cluster and the gun draw over it.
//The caller draws no dome and no cloud deck, suppresses the cloud shadow on the instanced effect, and takes
//the scene's own light rig (DreamLightingConfig) instead of a dome's.
//
//Everything here is ANALYTIC 3D - fields of sines on the view direction, ray-sphere tests, closest-approach
//glows - and never a 2D chart of the sphere, so there are no pole seams and nothing to hide. The floating
//solids are the one raymarched element, and the march is GATED: each shape carries an analytic bounding
//sphere, the ray is tested against those first (six quadratics), and only a shape whose bound the ray
//actually crosses is marched, inside its own [t0, t1] interval. Most pixels march nothing.
//
//Levels against the glare (GLARE_THRESHOLD 0.55 on luminance): the background marbling stays well under it
//- it is the CANVAS, and a canvas that blooms buries everything hung on it, which is exactly what the first
//build did at a higher brightness - the orbs are allowed over it deliberately (smooth areas hundreds of
//pixels wide, the planet's lit-limb reasoning, so they bloom steadily), while the SPARKS, which are small,
//stay at the threshold's edge and read fast through their trails rather than through bloom (a small point
//over the threshold is sampled stochastically by the glare's sparse grid and flickers, which reads as a fault).
//
//Built by all three executables out of this directory, Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

//How many of each element the sky carries. Fixed at compile time (the loops unroll); the config dials
//scale their look, not their count.
#define SHAPE_COUNT 8
#define ORB_COUNT 7
#define SPARK_COUNT 14

float4x4 InverseViewProjection;
float3 CameraPosition;
float DreamTime;

//--- The palette ---------------------------------------------------------------------------------------
//A cosine palette: colour(t) = A + B * cos(2pi * (C * t + D)). Four vectors are the scene's whole colour
//identity - every element below indexes the same ramp at a different phase, which is what keeps a frame
//full of saturated colour reading as ONE hallucination rather than as a box of crayons.
float3 PaletteA;
float3 PaletteB;
float3 PaletteC;
float3 PaletteD;

//--- The background marbling -----------------------------------------------------------------------------
float SwirlScale;          //bands per unit of direction - how fine the marbling is
float SwirlWarp;           //how far the field bends its own sampling direction; 0 is straight bands
float SwirlSpeedSlow;      //the broad marbling's drift (slow - it should read over minutes)
float SwirlSpeedFast;      //the sharp ribbons' travel (fast - they cross in seconds)
float RibbonSharpness;     //exponent on the fine layer; higher is thinner, sharper ribbons
float BackgroundBrightness;

//--- The floating solids ---------------------------------------------------------------------------------
float ShapeOrbitRadius;    //how far out the solids roam; they must stay outside the play space (see C#)
float ShapeSize;           //base half-size of a solid, world units
float ShapeMorphSpeed;     //how fast a solid melts between its forms
float ShapeEmission;       //how much of the palette glows from inside a solid
float ShapeReflection;     //how much of the marbled sky a solid mirrors

//--- The soft orbs and the sparks ------------------------------------------------------------------------
float OrbRadius;           //gaussian sigma of a soft orb, world units
float OrbBrightness;       //peak linear radiance of an orb's core (allowed over the glare threshold)
float SparkBrightness;     //peak linear radiance of a spark's head (kept AT the threshold, see header)
float SparkSpeed;

//The empty sky between everything else - not black: a dream has no void, only deeper colour.
float3 DeepColor;

static const float PI = 3.14159265;

float3 Palette(float t)
{
	return PaletteA + PaletteB * cos(2.0 * PI * (PaletteC * t + PaletteD));
}

//--- The marbling ----------------------------------------------------------------------------------------
//TWICE-warped fractal noise on the 3D view direction. The first build was a sum of plane-wave sines under
//a sine warp, and it read as exactly what it was - the plasma effect of a 1994 demo, smooth blobs sliding
//over each other. Plane waves keep their planes however many are summed; fractal noise has no planes to
//keep, and feeding one fBm's output into another's DOMAIN (twice) is what produces the filaments, eddies
//and mixing that real turbulence has. The time rides inside the domains, so the fluid itself evolves
//rather than a fixed pattern scrolling.
float3 Background(float3 d)
{
	float t = DreamTime;
	float3 p = d * SwirlScale;

	//The first warp: three offset fBms driving the domain apart - slow, the broad circulation.
	float3 q;
	q.x = Fbm3(p + float3(0.0, 1.7, t * SwirlSpeedSlow), 3);
	q.y = Fbm3(p + float3(5.2, 8.3, -t * SwirlSpeedSlow * 0.8), 3);
	q.z = Fbm3(p + float3(9.1, 2.8, t * SwirlSpeedSlow * 0.6), 3);

	//The second warp, off the already-warped domain - the layer that turns smooth drift into mixing.
	float3 warped = p + SwirlWarp * q;
	float3 r;
	r.x = Fbm3(warped * 1.3 + float3(1.7, 9.2, t * SwirlSpeedSlow * 0.5), 3);
	r.y = Fbm3(warped * 1.3 + float3(8.3, 2.8, -t * SwirlSpeedSlow * 0.4), 3);
	r.z = Fbm3(warped * 1.3 + float3(4.1, 6.9, t * SwirlSpeedSlow * 0.7), 3);

	float field = Fbm3(p + SwirlWarp * r, 4);

	//Colour: the palette rides the field, and the WARP INTERMEDIATES shade it - where the domain was
	//dragged furthest the fluid "mixed", so q picks the second palette phase and r darkens the folds.
	//Colouring by the intermediates rather than the final value alone is most of why warped fBm reads as
	//a substance with an inside instead of as a flat pattern.
	float3 color = Palette(field * 0.55 + t * 0.004);
	color = lerp(color, Palette(field * 0.55 + 0.38 + t * 0.004) * 1.35, saturate(dot(q, q) * 0.35));
	color *= 0.5 + 0.65 * saturate(length(r) * 0.65);

	//The ribbons: ridged fBm racing through - thin sharp filament networks, the fast half of the sky,
	//in a palette phase far from the ground they cross.
	float ribbon = RidgedFbm3(d * SwirlScale * 2.2 + float3(0.0, 0.0, t * SwirlSpeedFast), 3);
	ribbon = pow(saturate(ribbon - 0.35) * 1.7, RibbonSharpness * 0.45);
	color += Palette(field * 0.3 + 0.61) * ribbon * 0.9;

	return DeepColor + color * BackgroundBrightness;
}

//--- The floating solids ---------------------------------------------------------------------------------
//Where solid i stands now: a slow independent orbit, each at its own radius, height swing and rate, so the
//constellation never repeats and two solids occasionally drift close enough to melt together.
float3 ShapeCenter(float i, float t)
{
	float a = t * (0.020 + 0.011 * frac(i * 0.371)) + i * 2.399;
	float r = ShapeOrbitRadius * (0.78 + 0.22 * sin(i * 5.3));
	float y = 26.0 + 46.0 * sin(t * 0.013 + i * 2.7);

	return float3(cos(a) * r, y, sin(a) * r);
}

//One solid's distance field, in its own tumbling frame: a sphere, a rounded box and a torus, melted into
//one another on a slow cycle. The morph is the point - a shape that is never quite any one thing is what
//"the shapes keep changing" means, and the smooth lerp of distance fields is what makes the change a MELT
//rather than a swap.
float ShapeSdf(float3 p, float i, out float sizeOut)
{
	float t = DreamTime;
	float3 q = p - ShapeCenter(i, t);

	//A slow two-axis tumble. Rotations only - the SDF stays exact under them.
	float ya = t * (0.11 + 0.05 * frac(i * 0.73)) + i;
	float ca = cos(ya), sa = sin(ya);
	q.xz = float2(q.x * ca - q.z * sa, q.x * sa + q.z * ca);
	float xa = t * 0.07 + i * 1.7;
	float cb = cos(xa), sb = sin(xa);
	q.yz = float2(q.y * cb - q.z * sb, q.y * sb + q.z * cb);

	float size = ShapeSize * (0.7 + 0.3 * sin(i * 9.1));
	sizeOut = size;

	float m = 0.5 + 0.5 * sin(t * ShapeMorphSpeed + i * 11.3);
	float blend = smoothstep(0.12, 0.88, m);

	float dSphere = length(q) - size;

	float3 b = abs(q) - size * 0.72;
	float dBox = length(max(b, 0.0)) + min(max(b.x, max(b.y, b.z)), 0.0) - size * 0.14;

	float2 tor = float2(length(q.xz) - size * 0.78, q.y);
	float dTorus = length(tor) - size * 0.30;

	//Which pair this solid melts between is its own: half cycle sphere<->box, half box<->torus.
	return frac(i * 0.381) < 0.5 ? lerp(dSphere, dBox, blend) : lerp(dBox, dTorus, blend);
}

float3 ShapeNormal(float3 p, float i)
{
	float s;
	const float e = 0.25;
	float2 k = float2(1.0, -1.0);

	//The tetrahedral four-tap gradient - four SDF evaluations instead of six.
	return normalize(
		k.xyy * ShapeSdf(p + k.xyy * e, i, s) +
		k.yyx * ShapeSdf(p + k.yyx * e, i, s) +
		k.yxy * ShapeSdf(p + k.yxy * e, i, s) +
		k.xxy * ShapeSdf(p + k.xxy * e, i, s));
}

//The glow of a point along the ray, from the ray's closest approach to it - a pure gaussian, no march. This
//is the whole of an orb and the whole of a spark: the analytic soft half of the scene's sharp/soft contrast.
float RayGlow(float3 origin, float3 direction, float3 center, float sigma)
{
	float3 toCenter = center - origin;
	float along = max(dot(toCenter, direction), 0.0);
	float3 nearest = toCenter - direction * along;
	float d2 = dot(nearest, nearest);

	return exp(-d2 / (2.0 * sigma * sigma));
}

struct DreamVertexInput
{
	float4 Position : POSITION0;
};

struct DreamVertexOutput
{
	float4 Position : SV_POSITION;
	float3 Ray : TEXCOORD0;
};

DreamVertexOutput DreamVS(DreamVertexInput input)
{
	DreamVertexOutput output;
	output.Position = float4(input.Position.xy, 0.0, 1.0);

	//The corner unprojected to the far plane; the pixel shader normalizes the interpolated ray.
	float4 far = mul(float4(input.Position.xy, 1.0, 1.0), InverseViewProjection);
	output.Ray = far.xyz / far.w - CameraPosition;

	return output;
}

float4 DreamPS(DreamVertexOutput input) : COLOR
{
	float3 direction = normalize(input.Ray);
	float t = DreamTime;

	float3 color = Background(direction);

	//--- The soft orbs: huge, slow, blurred - luminous presences breathing through the marbling. Each takes
	//its own palette phase and swells on its own cycle, so at any moment some are waxing while others fade.
	[unroll]
	for (int o = 0; o < ORB_COUNT; o++)
	{
		float fo = (float)o;
		float3 center = float3(
			cos(t * 0.009 + fo * 2.1) * ShapeOrbitRadius * 1.25,
			15.0 + 60.0 * sin(t * 0.007 + fo * 3.3),
			sin(t * 0.011 + fo * 1.3) * ShapeOrbitRadius * 1.25);

		float breath = 0.35 + 0.65 * (0.5 + 0.5 * sin(t * 0.05 + fo * 2.6));
		float glow = RayGlow(CameraPosition, direction, center, OrbRadius * (0.7 + 0.3 * sin(fo * 7.0)));

		color += Palette(fo * 0.21 + t * 0.006) * (glow * OrbBrightness * breath);
	}

	//--- The sparks: small, fast, sharp - they cross between the orbs in seconds. Three samples down each
	//spark's own recent path make the head a comet: the trail is what reads as speed at any frame rate, the
	//fireworks' lesson.
	[unroll]
	for (int s = 0; s < SPARK_COUNT; s++)
	{
		float fs = (float)s;
		float rate = SparkSpeed * (0.7 + 0.6 * frac(fs * 0.617));

		[unroll]
		for (int trail = 0; trail < 3; trail++)
		{
			float tt = t - (float)trail * 0.06;
			float3 center = float3(
				sin(tt * rate + fs * 9.7) * 130.0,
				30.0 + 55.0 * sin(tt * rate * 0.7 + fs * 5.1),
				cos(tt * rate * 1.3 + fs * 3.9) * 130.0);

			float amp = SparkBrightness * (trail == 0 ? 1.0 : (trail == 1 ? 0.4 : 0.16));
			color += Palette(fs * 0.13 + 0.37) * (RayGlow(CameraPosition, direction, center, 2.2) * amp);
		}
	}

	//--- The solids: the sharp half of the scene. Each carries an analytic bounding sphere; the ray is
	//tested against those first, and only a crossed bound is marched, inside its own interval. The bounds
	//are generous (the smooth morph never leaves them) and the branch is coherent across a shape's screen
	//area, which is what makes it worth having.
	float bestT = 1e9;
	float bestShape = -1.0;

	[unroll]
	for (int i = 0; i < SHAPE_COUNT; i++)
	{
		float fi = (float)i;
		float3 center = ShapeCenter(fi, t);
		float bound = ShapeSize * 1.9;

		float3 oc = CameraPosition - center;
		float b = dot(oc, direction);
		float c = dot(oc, oc) - bound * bound;
		float disc = b * b - c;

		[branch]
		if (disc > 0.0)
		{
			float t0 = max(-b - sqrt(disc), 0.0);
			float t1 = -b + sqrt(disc);

			//Sphere-trace just this shape inside [t0, t1]. The interval is a few shape-widths, so the
			//march converges in far fewer steps than a whole-scene trace would.
			float rayT = t0;
			float size;

			[loop]
			for (int march = 0; march < 28; march++)
			{
				float d = ShapeSdf(CameraPosition + direction * rayT, fi, size);
				if (d < 0.02 || rayT > t1) break;
				rayT += d * 0.9;
			}

			if (rayT <= t1 && rayT < bestT)
			{
				bestT = rayT;
				bestShape = fi;
			}
		}
	}

	[branch]
	if (bestShape >= 0.0)
	{
		float3 hit = CameraPosition + direction * bestT;
		float3 normal = ShapeNormal(hit, bestShape);

		//Ambient occlusion off the solid's own field: four probes up the normal, each asking how much
		//less room there is than an open surface would have. It is what darkens the torus's inner ring
		//and the melt seams mid-morph - the CONTACT the first build lacked, whose absence is half of what
		//separates a modern render from a screensaver (nothing in one ever shades anything else).
		float occlusion = 0.0;
		float probe = 1.4;
		float sizeUnused;

		[unroll]
		for (int a = 1; a <= 4; a++)
		{
			float reach = probe * (float)a;
			occlusion += (reach - ShapeSdf(hit + normal * reach, bestShape, sizeUnused)) / reach * pow(0.55, (float)a);
		}

		float ao = saturate(1.0 - 1.3 * occlusion);

		//A solid is lit by the dream itself: its own palette colour glowing from inside, the marbled sky
		//mirrored off its surface, and a fresnel rim that lifts its silhouette out of the background - the
		//sharp edge the soft orbs exist to contrast with. The emission floor is high (a solid whose
		//palette phase lands dark would vanish as a silhouette), but the AO is allowed to press even the
		//emission down: a hallucination has no unlit objects, and still its folds have depth.
		float3 own = Palette(bestShape * 0.17 + t * 0.010);
		float fresnel = pow(1.0 - saturate(dot(normal, -direction)), 3.0);
		float3 mirrored = Background(reflect(direction, normal));

		float3 shapeColor = own * ShapeEmission * (0.55 + 0.45 * fresnel) * (0.35 + 0.65 * ao)
			+ mirrored * ShapeReflection * (0.4 + 0.6 * fresnel) * ao;

		//The far solids sink into the marbling rather than popping against it - a touch of the background
		//over distance, the haze idea with colour instead of grey.
		float fade = saturate(bestT / 600.0);
		color = lerp(shapeColor, color, fade * 0.5);
	}

	return float4(color, 1.0);
}

technique Dream
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL DreamVS();
		PixelShader = compile PS_SHADERMODEL DreamPS();
	}
};
