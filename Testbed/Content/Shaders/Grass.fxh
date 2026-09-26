//The grass's fine relief, which the meadow and the savanna share (#581): the two were line-for-line copies -
//the meadow began as one of the savanna - and both faults below were found in one and then again in the
//other, which is the case for one copy. What stays per scene is the TUNING: each declares its own
//GRASS_COMB_STRETCH and GRASS_FBM_GAIN (the same figures today, a lush lawn and a dry veld meant to be free
//to differ; the savanna's copy says what each answers), and its GrassReliefStrength/Frequency and
//WindDirection uniforms, before including this. Needs Noise.fxh (Fbm2Combed).
//
//A fine grass texture, band-limited against the footprint so it fades to smooth grass towards the horizon
//instead of aliasing.
//
//THREE OCTAVES OF GRADIENT NOISE, not the two crossed plane-wave sines this used to be. Two plane waves
//crossing ARE a lattice - that is what their interference is - and these crossed at 93.4 degrees, so the
//lattice was very nearly square and read in perspective as a field of diamonds across the middle distance
//(#117, filed against the savanna; the meadow's copy was found while fixing it). At GrassReliefFrequency 2
//the two periods were 3.14 and 1.75 world units, which is the scale the diamonds appeared at. It showed as
//strongly as it did because the field feeds PerturbNormalFromHeight, so it tilts the NORMAL and lands in the
//shading rather than merely in the colour. This is the failure Noise.fxh's own opening documents - "a sum of
//plane-wave sines keeps its planes however many terms it has" - and the one #86 removed from Mountain.fx's
//peaks. Octaves of gradient noise on a rotated domain have no planes to keep.
//
//⚠ GRASS SWAYS, IT DOES NOT TRAVEL (#276). This used to sample at `(xz + WindDirection * Time * 0.7)` — a
//flat 0.7 world units a second, for ever. At GrassReliefFrequency 2 a grass feature is half a unit, so the
//blades' own texture SLID ACROSS THE GROUND IT IS ROOTED IN at 1.4 features a second: measured on two meadow
//frames 0.6 s apart, essentially every pixel of the near field had changed. That is the crawl the owner
//reported, and no amount of retuning the speed fixes it, because a texture that translates is wallpaper
//however slowly it goes. (It also drifted UPWIND — adding to the sample position moves the pattern the other
//way — which nothing said and nothing could see, a sliding texture having no direction the eye can name.)
//
//What the wind does to grass is BEND it: the blades lean where a gust is passing and spring back behind it.
//So the lean is the gust field's own value, applied in the NOISE domain so it is a fixed fraction of a grass
//feature whatever GrassReliefFrequency is set to, and it is bounded by construction — the gust is clamped to
//[-1, 1], so the texture rocks about an eighth of a feature either side of where it is rooted and stays there.
static const float GRASS_SWAY_REACH = 0.16;

float GrassRelief(float2 xz, float footprint, float gust)
{
    float f = GrassReliefFrequency;
    float2 p = xz * f + WindDirection * (gust * GRASS_SWAY_REACH);

    //Combed along the wind, and the footprint scaled by the same factor the domain is — Fbm2BandLimited's
    //stated contract, which Fbm2Combed passes straight through.
    return Fbm2Combed(p, WindDirection, GRASS_COMB_STRETCH, 3, footprint * f) * GRASS_FBM_GAIN * GrassReliefStrength;
}
