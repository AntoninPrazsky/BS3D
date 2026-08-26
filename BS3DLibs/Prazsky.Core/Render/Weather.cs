using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What kind of sky is over the arena (#221). One weather for the whole game until then: every scene
    /// that showed clouds at all showed the <i>same</i> field — one coverage, one feature size, one wind,
    /// over all eighteen domes — and the only variation the deck knew was <c>off</c>, which is what the
    /// four sky-replacing scenes take.
    /// <para>
    /// <b>A curated vocabulary rather than raw dials</b>, which is the music's own argument arriving on the
    /// sky: a level naming <c>overcast</c> asks for a sky somebody authored and can be shown a picture of,
    /// where a level carrying eleven floats asks for whatever those floats happened to be, and a mistyped
    /// one is a sky nobody can name. The bundles are <see cref="WeatherLook"/>'s; this enum is only which.
    /// </para>
    /// </summary>
    public enum WeatherPreset
    {
        /// <summary>Nothing at all: the dome, the sun and no deck. What a scene wanting its own sky takes.</summary>
        Clear,

        /// <summary>Fair-weather cumulus, well apart, most of the sky open. The game's own weather until #221.</summary>
        Scattered,

        /// <summary>More cloud than sky, still with holes the sun comes through — the working sky of a cloudy day.</summary>
        Broken,

        /// <summary>Closed over: a high flat sheet, the sun a bright patch behind it, the ground evenly lit.</summary>
        Overcast,

        /// <summary>A heavy low deck with the light nearly out of it: dark, fast and shredded.</summary>
        Storm,
    }

    /// <summary>
    /// One weather, as the numbers that draw it — the bundle a <see cref="WeatherPreset"/> names. It carries
    /// both halves of the deck: the <b>shape</b> (where cloud is at all, how big, how fast) and the
    /// <b>character</b> (how it erodes, how opaque it is, how deep its billow hangs, how dark its shadow
    /// goes), because a fair-weather cumulus and a storm front differ in far more than how much sky they
    /// cover. Until #221 the shape was a handful of settable properties and the character was constants
    /// shared by every executable, which is exactly why one weather was all there could be.
    /// <para>
    /// <b>Every field here is a value the shaders already took</b>; nothing new was added to
    /// <c>Clouds.fxh</c> for this. What changed is that they arrive per weather and are blended between two
    /// of them (<see cref="CloudField.Lerp"/>), rather than being pushed once at load and never again.
    /// </para>
    /// <para>
    /// <b>What one field cannot do, stated rather than discovered later</b> (#221 asks for this honestly):
    /// the field is two octaves of gradient noise plus the sky shader's finer ones, so what varies between
    /// these five is a cloud's <i>size, density, erosion, darkness and drift</i> — not its topology. A
    /// front with a leading edge sweeping across the sky, lenticular wave clouds standing still in a wind,
    /// and a thunderhead's anvil are all structures this field has no term for, and <c>Storm</c> here is a
    /// heavy shredded overcast rather than a front. Giving it one would be a second field and a second set
    /// of octaves, which the marched-slab measurements in <c>docs/rendering.md</c> price out of reach.
    /// </para>
    /// </summary>
    public readonly struct WeatherLook
    {
        /// <summary>World Y the cloud plane sits at. A storm deck hangs lower than a fair-weather one, which
        /// is most of why it reads as heavy: the same cloud nearer the eye subtends more sky.</summary>
        public readonly float PlaneY;

        /// <summary>Noise units per world unit; the reciprocal is roughly one weather feature across.</summary>
        public readonly float Scale;

        /// <summary>Wind, in world units per second.</summary>
        public readonly Vector2 Wind;

        /// <summary>Where the coverage threshold sits: negative opens the sky, positive closes it over.</summary>
        public readonly float CoverageBias;

        /// <summary>How sharply the field crosses that threshold — the edge between cloud and sky.</summary>
        public readonly float CoverageGain;

        /// <summary>Least sun that reaches through the thickest cloud, and how fast the shadow deepens.</summary>
        public readonly float ShadowFloor;

        public readonly float ShadowGain;

        /// <summary>How hard the fine octaves chew at the shape the weather layer drew.</summary>
        public readonly float DetailStrength;

        /// <summary>Opacity of the densest cloud. Well over 1 means a cloud reaches solid before its edges do.</summary>
        public readonly float Opacity;

        /// <summary>How deep the underside hangs off the plane, and how hard the tilted facets swing the light.</summary>
        public readonly float FormStrength;

        public readonly float ShapeStrength;

        /// <summary>How far the per-cloud character field swings the detail strength either way.</summary>
        public readonly float CharacterStrength;

        /// <summary>The shadowed underside, in <b>linear radiance</b> — a quantity of light, not a paint
        /// colour, so nothing decodes it. It is what carries most of a storm's darkness.</summary>
        public readonly Vector3 ShadowColor;

        public WeatherLook(float planeY, float scale, Vector2 wind, float coverageBias, float coverageGain,
            float shadowFloor, float shadowGain, float detailStrength, float opacity, float formStrength,
            float shapeStrength, float characterStrength, Vector3 shadowColor)
        {
            PlaneY = planeY;
            Scale = scale;
            Wind = wind;
            CoverageBias = coverageBias;
            CoverageGain = coverageGain;
            ShadowFloor = shadowFloor;
            ShadowGain = shadowGain;
            DetailStrength = detailStrength;
            Opacity = opacity;
            FormStrength = formStrength;
            ShapeStrength = shapeStrength;
            CharacterStrength = characterStrength;
            ShadowColor = shadowColor;
        }

        /// <summary>
        /// One weather part of the way to another — every field lerped, so a change of sky is a sky
        /// <i>changing</i> rather than one frame's cut. See <see cref="CloudField.SetWeather"/> for why the
        /// fade is the field's own business and not the caller's.
        /// </summary>
        public static WeatherLook Lerp(in WeatherLook from, in WeatherLook to, float amount) => new(
            MathHelper.Lerp(from.PlaneY, to.PlaneY, amount),
            MathHelper.Lerp(from.Scale, to.Scale, amount),
            Vector2.Lerp(from.Wind, to.Wind, amount),
            MathHelper.Lerp(from.CoverageBias, to.CoverageBias, amount),
            MathHelper.Lerp(from.CoverageGain, to.CoverageGain, amount),
            MathHelper.Lerp(from.ShadowFloor, to.ShadowFloor, amount),
            MathHelper.Lerp(from.ShadowGain, to.ShadowGain, amount),
            MathHelper.Lerp(from.DetailStrength, to.DetailStrength, amount),
            MathHelper.Lerp(from.Opacity, to.Opacity, amount),
            MathHelper.Lerp(from.FormStrength, to.FormStrength, amount),
            MathHelper.Lerp(from.ShapeStrength, to.ShapeStrength, amount),
            MathHelper.Lerp(from.CharacterStrength, to.CharacterStrength, amount),
            Vector3.Lerp(from.ShadowColor, to.ShadowColor, amount));
    }

    /// <summary>
    /// The five authored skies, and the one parse that turns a level file's word into one of them (#221).
    /// </summary>
    public static class WeatherLooks
    {
        /// <summary>
        /// <b>Scattered is the weather the game had</b>, and its numbers are the pre-#221 defaults to the
        /// last digit — so a scene that says nothing about its sky renders exactly what it rendered before
        /// there was anything to say. Every other preset is stated as a departure from it, and the four
        /// departures are the four things a sky can do: go away, close in, close over, and turn ugly.
        /// </summary>
        private static readonly WeatherLook Scattered = new(
            planeY: 190f, scale: 1f / 450f, wind: new Vector2(4.5f, 2f),
            coverageBias: 0.02f, coverageGain: 2.8f,
            shadowFloor: 0.38f, shadowGain: 1.3f,
            detailStrength: 2.3f, opacity: 2.4f,
            formStrength: 60f, shapeStrength: 1.4f, characterStrength: 0.7f,
            shadowColor: new Vector3(0.18f, 0.21f, 0.28f));

        //CLEAR is Scattered with the threshold driven under the field's own floor. The two octaves are
        //weighted 0.62/0.38 about zero, so the weather layer cannot reach below -1: a bias of -1.2 leaves
        //nothing above the threshold anywhere, at any drift, for ever - which is what "no cloud" has to
        //mean, rather than "very little cloud, most days". Everything else is left at Scattered's figure
        //deliberately: with no cloud to draw, none of it is reachable, and a preset whose unreachable
        //values differ from its neighbour's is a preset that jumps when it is faded through.
        private static readonly WeatherLook Clear = new(
            planeY: 190f, scale: 1f / 450f, wind: new Vector2(4.5f, 2f),
            coverageBias: -1.2f, coverageGain: 2.8f,
            shadowFloor: 0.38f, shadowGain: 1.3f,
            detailStrength: 2.3f, opacity: 2.4f,
            formStrength: 60f, shapeStrength: 1.4f, characterStrength: 0.7f,
            shadowColor: new Vector3(0.18f, 0.21f, 0.28f));

        //BROKEN: more cloud than sky, and still holes. The bias carries it past half cover and the gain
        //comes DOWN, which is the half of it that matters - a lower gain is a softer crossing, so the holes
        //have ragged edges rather than being punched out of a sheet. Slightly larger features (a smaller
        //scale is a bigger cloud) and a touch more billow, because at this cover the eye reads the deck's
        //underside rather than its silhouette.
        private static readonly WeatherLook Broken = new(
            planeY: 175f, scale: 1f / 520f, wind: new Vector2(6f, 2.6f),
            coverageBias: 0.30f, coverageGain: 2.2f,
            shadowFloor: 0.30f, shadowGain: 1.5f,
            detailStrength: 2.1f, opacity: 2.6f,
            formStrength: 70f, shapeStrength: 1.5f, characterStrength: 0.6f,
            shadowColor: new Vector3(0.15f, 0.17f, 0.23f));

        //OVERCAST: closed over. The bias is past what the field can undo, so there are no holes at all, and
        //the gain is low because at full cover the gain only decides how hard the (non-existent) edges are.
        //What makes it read as a SHEET rather than as one enormous cloud is the character coming almost off
        //(0.15) and the billow coming down: a flat lid with a little relief in it, high up. The shadow floor
        //RISES - an overcast ground is evenly lit rather than dark, which is the same fact SkyLightRig's own
        //overcast palette records (a cloud deck is a big diffuse source, so losing the sun spreads the light
        //rather than removing it).
        private static readonly WeatherLook Overcast = new(
            planeY: 215f, scale: 1f / 620f, wind: new Vector2(5f, 2.2f),
            coverageBias: 0.85f, coverageGain: 1.6f,
            shadowFloor: 0.55f, shadowGain: 0.9f,
            detailStrength: 1.4f, opacity: 3.0f,
            formStrength: 35f, shapeStrength: 0.9f, characterStrength: 0.15f,
            shadowColor: new Vector3(0.12f, 0.13f, 0.16f));

        //STORM: the ugly one, and the preset #219's scene is waiting on. It is Overcast's cover carried on a
        //deck that hangs a third lower, drifts half again as fast, and is eroded far harder - the detail
        //strength and the character swing both go UP, which is what makes it shredded and uneven where the
        //overcast is smooth. Its darkness is nearly all in the shadow colour: the undersides go to a quarter
        //of Scattered's radiance, which the ACES curve leaves as real black rather than as grey, and the
        //shadow floor drops to the lowest in the set so the ground under it genuinely loses the sun.
        //
        //It is a heavy shredded overcast and NOT a front - see WeatherLook's own remarks for what this field
        //has no term for. Calling it Storm is a promise about how it FEELS, which it keeps; a promise about
        //its structure would not be.
        private static readonly WeatherLook Storm = new(
            planeY: 140f, scale: 1f / 560f, wind: new Vector2(11f, 5f),
            coverageBias: 0.72f, coverageGain: 2.4f,
            shadowFloor: 0.16f, shadowGain: 2.1f,
            detailStrength: 3.2f, opacity: 3.4f,
            formStrength: 95f, shapeStrength: 1.9f, characterStrength: 0.9f,
            shadowColor: new Vector3(0.045f, 0.05f, 0.065f));

        /// <summary>The numbers behind one preset.</summary>
        public static WeatherLook Of(WeatherPreset preset) => preset switch
        {
            WeatherPreset.Clear => Clear,
            WeatherPreset.Broken => Broken,
            WeatherPreset.Overcast => Overcast,
            WeatherPreset.Storm => Storm,
            _ => Scattered,
        };

        /// <summary>
        /// The weather a level file's <c>"weather"</c> field names, or null when it names none. Parsed
        /// rather than cast, for the reason the scene and the music are: it is a hand-editable file, and an
        /// unknown spelling has to mean "whatever the scene wanted" rather than an exception.
        /// </summary>
        public static WeatherPreset? TryParse(string named)
        {
            if (string.IsNullOrWhiteSpace(named)) return null;

            return named.Trim().ToLowerInvariant() switch
            {
                "clear" => WeatherPreset.Clear,
                "scattered" => WeatherPreset.Scattered,
                "broken" => WeatherPreset.Broken,
                "overcast" => WeatherPreset.Overcast,
                "storm" => WeatherPreset.Storm,
                _ => null,
            };
        }

        /// <summary>The word a preset is written as, for the level files this tool chain emits.</summary>
        public static string NameOf(WeatherPreset preset) => preset switch
        {
            WeatherPreset.Clear => "clear",
            WeatherPreset.Broken => "broken",
            WeatherPreset.Overcast => "overcast",
            WeatherPreset.Storm => "storm",
            _ => "scattered",
        };
    }
}
