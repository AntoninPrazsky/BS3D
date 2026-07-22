//Draws a savanna: open golden grassland rolling gently to a wide horizon, a flat clearing the island stands
//in that rises into low rises with distance, dotted with acacia trees (drawn separately, Acacia.fx). Third
//scene variant (NumPad2), replacing the Sahara dunes.
//
//Real geometry like the meadow and the mountains - a camera-centred grid (shared CreateGridMesh on the C#
//side) snapped to a cell so it does not swim. The one thing done differently, and the whole point of the
//rework: the terrain NORMAL is taken PER PIXEL from the height field's own gradient, not interpolated from
//per-vertex normals. A coarse mesh's per-vertex normal creates a faint facet/grid pattern across the
//surface (Mach bands at every cell edge); evaluating the gradient per pixel makes the shading smooth
//regardless of tessellation, so the grid is gone. TerrainHeight is a handful of sines, cheap to tap thrice.
//Grass is dry gold-green, varied in patches and combed by the wind; the field takes the dome's mood and the
//shared cloud shadows drift across it. Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Clouds.fxh"

float4x4 View;
float4x4 Projection;
float3 CameraPosition;
float3 SunDirection;
float3 SunColor;
float3 ZenithColor;
float3 HorizonColor;

//Where the flat grid is pinned this frame (camera XZ snapped to a cell), and the terrain shape dials
float2 OriginXZ;
float SavannaLevelY;
float HillHeight;
float ClearingRadius;
float ClearingTransition;
float ClearingRelief;

float SavannaTime;
float2 WindDirection;

//Grass (linear): the greener and the drier golden shade it varies between in patches, how much sky fills the
//flats, and the distance the field melts into the skyline over
float3 GrassColor;
float3 GrassColorDry;
float3 GrassColorBare;
float AmbientStrength;
float HorizonHazeDistance;

//Wind combing the grass: band speed, spacing and depth
float WindRippleSpeed;
float WindRippleFrequency;
float WindRippleStrength;

//Fine grass texture (a normal-tilting height field): amplitude and blades-per-world-unit
float GrassReliefStrength;
float GrassReliefFrequency;

//Gentle rolling savanna: smooth low sines, flat within the clearing around the origin (where the island
//stands) and rising into low rises with distance. Kept flatter than the meadow's hills - a savanna is open.
float TerrainHeight(float2 p)
{
	float dist = length(p);
	float ramp = smoothstep(ClearingRadius, ClearingRadius + ClearingTransition, dist);

	float rolling = 0.5 * sin(dot(p, float2(0.016, 0.012)))
		+ 0.3 * sin(dot(p, float2(-0.011, 0.020)) + 1.5)
		+ 0.2 * sin(dot(p, float2(0.026, 0.021)) + 3.0);

	//Gentle undulation even inside the clearing, so the near ground is not a dead-flat plane (two crossing
	//swells rather than one). Kept low enough that the crests clear the island's foot.
	float gentle = ClearingRelief * (sin(dot(p, float2(0.04, 0.03))) + 0.6 * sin(dot(p, float2(-0.055, 0.048)) + 2.1));

	return SavannaLevelY + gentle + HillHeight * ramp * (rolling * 0.5 + 0.5);
}

struct SavannaVertexInput
{
	float4 Position : POSITION0;
};

struct SavannaVertexOutput
{
	float4 Position : SV_POSITION;
	float3 WorldPosition : TEXCOORD0;
};

SavannaVertexOutput SavannaVS(SavannaVertexInput input)
{
	SavannaVertexOutput output;

	float2 xz = input.Position.xz + OriginXZ;
	float3 worldPosition = float3(xz.x, TerrainHeight(xz), xz.y);

	output.WorldPosition = worldPosition;
	output.Position = mul(mul(float4(worldPosition, 1.0), View), Projection);

	return output;
}

//Tangent-free normal tilt from a height field (Christian Schueler), as everywhere else in this project
float3 PerturbNormalFromHeight(float3 normal, float3 worldPosition, float height)
{
	float3 dpdx = ddx(worldPosition);
	float3 dpdy = ddy(worldPosition);

	float3 r1 = cross(dpdy, normal);
	float3 r2 = cross(normal, dpdx);

	float determinant = dot(dpdx, r1);
	float3 surfaceGradient = sign(determinant) * (ddx(height) * r1 + ddy(height) * r2);

	return normalize(abs(determinant) * normal - surfaceGradient);
}

//Fine grass texture that drifts on the wind, band-limited against the footprint so it fades to smooth grass
//towards the horizon rather than aliasing
float GrassRelief(float2 xz, float footprint)
{
	float2 p = xz + WindDirection * SavannaTime * 0.7;
	float f = GrassReliefFrequency;

	float h = 0.6 * sin(dot(p, normalize(float2(0.9, 0.3))) * f)
		+ 0.4 * sin(dot(p, normalize(float2(-0.4, 1.0))) * f * 1.8);

	return h * saturate(1.0 - footprint * f / 3.14159265) * GrassReliefStrength;
}

float4 SavannaPS(SavannaVertexOutput input) : COLOR
{
	float3 worldPosition = input.WorldPosition;
	float footprint = length(fwidth(worldPosition.xz));

	//The base terrain normal, taken PER PIXEL from the height field's gradient (three cheap taps) rather than
	//interpolated from per-vertex normals - this is what removes the coarse mesh's facet/grid pattern.
	float e = 1.5;
	float h = TerrainHeight(worldPosition.xz);
	float hx = TerrainHeight(worldPosition.xz + float2(e, 0.0));
	float hz = TerrainHeight(worldPosition.xz + float2(0.0, e));
	float3 baseNormal = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

	//Fine grass texture tilts it, so the grass catches the light unevenly and the wind reads on it
	float relief = GrassRelief(worldPosition.xz, footprint);
	float3 normal = PerturbNormalFromHeight(baseNormal, worldPosition, relief);

	//Three-tone grass: dry gold as the base, green flushes where it is lusher, and patches of bare reddish
	//earth. Sampled at several noise scales so the field reads varied and alive, not one flat tone.
	float patchLarge = CloudNoise(worldPosition.xz * 0.012) * 0.5 + 0.5;   //broad green vs gold zones
	float patchMed = CloudNoise(worldPosition.xz * 0.05 + 17.0) * 0.5 + 0.5;
	float bare = CloudNoise(worldPosition.xz * 0.09 + 60.0) * 0.5 + 0.5;   //scattered bare earth

	float3 grass = lerp(GrassColorDry, GrassColor, saturate((patchLarge - 0.35) * 2.2) * patchMed);
	grass = lerp(grass, GrassColorBare, smoothstep(0.68, 0.82, bare) * 0.7);

	//Wind combing the grass: bright and dark bands travelling downwind
	float wind = sin(dot(worldPosition.xz, WindDirection) * WindRippleFrequency + SavannaTime * WindRippleSpeed);
	grass *= 1.0 + wind * WindRippleStrength;

	//Matte grass: the sun and the sky hemisphere, dimmed by the shared cloud shadow so the same clouds that
	//drift across the sky sweep their shadows over the field
	float sunlight = CloudSunlight(worldPosition, SunDirection);
	float ndotl = saturate(dot(normal, SunDirection));
	float3 skyAmbient = lerp(HorizonColor, ZenithColor, saturate(normal.y * 0.5 + 0.5));

	float3 color = grass * (skyAmbient * AmbientStrength + SunColor * ndotl * sunlight);

	//Horizon haze: the distant field softens into the skyline
	float dist = distance(CameraPosition, worldPosition);
	float haze = saturate(dist / HorizonHazeDistance);
	color = lerp(color, HorizonColor, haze * haze);

	return float4(color, 1.0);
}

technique Savanna
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL SavannaVS();
		PixelShader = compile PS_SHADERMODEL SavannaPS();
	}
};
