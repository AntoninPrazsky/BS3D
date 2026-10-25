using Prazsky.Core.Render;

namespace Prazsky.Core
{
    public partial class SkyDome
    {
        //The sea mirrors the sky, so its whole mood follows the dome and a bright one gives a breezy sea
        //rather than a moody one; the savanna wants the set's warmest gold horizon; the tropical beach
        //wants the brightest blue in the set (dome 1, a clear sunny sky over a warm horizon — white
        //sand and turquoise water are the postcard, and they read as one under it).
        private const byte SEA_DOME = 13;
        private const byte SAVANNA_DOME = 14;
        private const byte TROPICAL_DOME = 1;

        //The volcano wants a sky that stays out of the way, because its ground is the light. Dome 9 is a dim
        //mauve-and-slate dusk with no bright band and no sun disc beside the cone — picked by looking, over
        //the darker-zenithed 16 whose cream horizon and sun both compete with the crater.
        private const byte VOLCANO_DOME = 9;

        //Mars has a dome built for it (#277) rather than a pick among the general-purpose ones — dome 19 IS the
        //Martian sky.
        private const byte MARS_DOME = 19;

        //The storm has a dome built for it too (#219): dome 20 is high air — a deep blue zenith over a PALE
        //BLUE-WHITE horizon, which is the one thing none of the others has. The argument that once forced it was
        //the terrain grid's edge; the storm is a field of billboards now and has no mesh edge, so it stays on the
        //plainer ground that this is what altitude looks like and is what keeps white cloud reading as white cloud.
        private const byte STORM_DOME = 20;

        //And the polar icesheet (#222): dome 13, a teal-grey horizon into indigo with its sun at 13 degrees.
        //Chosen off a photographed sweep rather than by taste, because this scene's content IS the material
        //and the light is what a material shows: at 55 degrees (dome 11) a flat field's ndotl is nearly
        //constant and the sastrugi only read through their own trough shading; at 42 (dome 17) the ice reads
        //white on white, the cyan never firing because transmission needs the sun BEHIND the ice; at 4 with a
        //cream horizon (dome 16) the sheet takes the warm light and reads golden-brown, which is a real look
        //and not this one.
        private const byte POLAR_DOME = 13;

        //The forest (#961): a dark wood wants a dusk that sits with it and not a bright sky over it, and the
        //fireflies want the dusk. It entered under whatever dome was up, and the one the Game starts on, 13, is
        //a flat saturated teal that covered half the sky above the black hills. Dome 15 is the late twilight
        //(sun 11 degrees, so the canopy still throws its shadows): an indigo deck with a teal skirt at the
        //horizon, the hills going blue-black under it and the broadleaf crowns keeping their green. Picked off
        //a sweep of all twenty under the Broken deck at the play camera, among the domes that keep the sun above
        //the shadow map's floor and are not red or pink. Dome 9, the volcano's blue hour, was the runner-up and
        //is the quieter one: no bright saturated sky at all, but its hills went the brown of bare earth.
        private const byte FOREST_DOME = 15;

        //The outback (#967) has a dome built for it: 21, a deep blue zenith over a warm white skyline and a gold sun
        //at 21 degrees. It took whatever dome was up, and the one the Game starts on, 13, is a teal sky over red
        //ground; a sweep of all twenty on the plain found none that the references of the real place drew (every
        //one garish, flat or a dusk), and the far formations stand DARK against a pale skyline only under this one.
        private const byte OUTBACK_DOME = 21;

        //The meadow (#1019) is the first chapter's Bliss hill, and every one of its levels names dome 1, the clear
        //sunny blue: a meadow that kept whatever dome was up flew the benchmark under the forest's late twilight just
        //before it, and showed the menu a purple dusk after a forest level. The owner: "very positive and bright...
        //the sky should be azure"
        private const byte MEADOW_DOME = 1;

        /// <summary>
        /// The dome a scene brings with it, or 0 when it states none and keeps whatever dome is up (#595).
        /// <para>
        /// One table for every executable. It stood as private constants in the Game (<c>BS3DGame.SetScene</c>)
        /// and again in the Testbed (<c>DefaultSkyDome</c>), whose own remark said they agreed "today; nothing
        /// makes them" — and a scene has to stand under the same sky in the instrument that judges it and in the
        /// game that ships it.
        /// </para>
        /// <para>
        /// Space deliberately states none, although its dome is inert: it is neither drawn (Space.fx covers the
        /// whole frame) nor read (SpaceLightingConfig states the light rig instead), so changing the player's dome
        /// behind their back to no visible effect would be a silent side effect rather than a setting.
        /// </para>
        /// </summary>
        public static byte SceneDefault(SceneKind scene) => scene switch
        {
            SceneKind.Sea => SEA_DOME,
            SceneKind.Savanna => SAVANNA_DOME,
            SceneKind.Tropical => TROPICAL_DOME,
            SceneKind.Volcano => VOLCANO_DOME,
            SceneKind.Mars => MARS_DOME,
            SceneKind.Storm => STORM_DOME,
            SceneKind.Polar => POLAR_DOME,
            SceneKind.Forest => FOREST_DOME,
            SceneKind.Outback => OUTBACK_DOME,
            SceneKind.Meadow => MEADOW_DOME,
            _ => 0,
        };
    }
}
