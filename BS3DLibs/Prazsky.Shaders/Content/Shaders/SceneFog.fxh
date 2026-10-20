//A scene's ground mist and aerial haze (#963), one copy for every shader that draws a surface the mist lies over:
//the aurora's snow, its far stand and its sky include it, and so does InstancedModel's ShadePixel, so the trees,
//the logs and the snags standing in the mist are veiled by the same arithmetic as the snow at their feet. A scene
//without mist pushes SceneFogDensity 0 (SceneRenderer does, every frame, through the shared instanced effect), and
//every caller gates on it with a [branch]: non-divergent, and nothing inside takes a derivative.
//
//The mist is HEIGHT fog: dense at SceneFogBaseY and thinning by e for every SceneFogScaleHeight above it, so it pools
//in the clearing among the trunks while the crowns and the hills stand out of it. Its optical depth along the ray from
//the eye to a point is the closed form of that exponential's integral - no march, no texture. A start distance keeps
//it off everything near the lens (the island, the gun and the cluster stand inside it and are never veiled), and a
//patchiness term, one gradient-noise tap at the point, breaks the layer into banks drifting with the wind. The includer
//includes Clouds.fxh first (CloudNoise).

float3 SceneFogColor;        //the mist's own radiance (linear): what a fully veiled point shows
float SceneFogDensity;       //optical depth a unit of path at the base height; 0 = no mist anywhere
float SceneFogBaseY;         //world Y the density is stated at
float SceneFogScaleHeight;   //how far up the density falls by e
float SceneFogStart;         //world units of path from the eye that carry no mist
float SceneFogHaze;          //a thin height-independent haze on top, optical depth a unit of path
float SceneFogPatchiness;    //0..1: how deeply the banks break the layer
float2 SceneFogDrift;        //world XZ offset of the banks' field (the wind carries them)

//The fraction of a point's own radiance the mist takes away between the eye and the point, 0..1.
float SceneFogAmount(float3 eye, float3 worldPosition)
{
    float3 ray = worldPosition - eye;
    float distanceToPoint = length(ray);
    float path = max(distanceToPoint - SceneFogStart, 0.0);

    //The exponential's mean along the ray: exp(-k (y - base)) integrated from the eye's height to the point's and divided
    //by the rise. A level ray is the limit 1, which the guard takes before the division can.
    float k = 1.0 / max(SceneFogScaleHeight, 0.01);
    float rise = ray.y * k;
    float mean = abs(rise) > 1e-3 ? (1.0 - exp(-rise)) / rise : 1.0 - 0.5 * rise;
    float atEye = exp(-(eye.y - SceneFogBaseY) * k);

    //The banks: the layer is thinner or thicker where the point stands, never absent
    float bank = 1.0 + SceneFogPatchiness * CloudNoise((worldPosition.xz + SceneFogDrift) * 0.018) * 1.6;

    float optical = path * (SceneFogDensity * atEye * mean * max(bank, 0.15) + SceneFogHaze);
    return 1.0 - exp(-min(optical, 30.0));
}

//A shaded colour seen through the mist. Callers gate on SceneFogDensity > 0 themselves when the rest of their work
//should be skipped too; this one is safe to call ungated.
float3 ApplySceneFog(float3 color, float3 eye, float3 worldPosition)
{
    [branch]
    if (SceneFogDensity > 0.0)
        color = lerp(color, SceneFogColor, SceneFogAmount(eye, worldPosition));

    return color;
}
