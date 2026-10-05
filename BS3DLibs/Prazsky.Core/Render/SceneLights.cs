using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The point lights a scene carries of its own, on top of the sun and the dome-derived ambient — <b>real
    /// lights that illuminate</b>, not emissive surfaces that only glow (the city's windows and its neon signs
    /// glow and light nothing). Built for the current <see cref="SceneKind"/> once a frame and pushed onto the
    /// one <b>shared</b> instanced effect, which is what makes them reach everything at once: the balls, the
    /// island and its drain, the host's cannon or gun, and the city all draw through that effect, so they take
    /// these lights under whatever sky dome is up. It existed line-for-line in both the Testbed and the Game
    /// until #75.
    /// <para>
    /// Some scenes carry lights of their own: the volcano's crater and its travelling flow fronts, the
    /// savanna's campfire, the neon city's ring of magenta and cyan, space's planetshine, the Moon's
    /// earthshine and the big top's footlights. The others push a count of zero once and then cost nothing — see the early-out in
    /// <see cref="Apply"/>.
    /// </para>
    /// <para>
    /// <b>The savanna grass shader's copy is deliberately not this one.</b>
    /// <see cref="SceneRenderer"/>'s <c>DrawSavanna</c> writes the same four uniform names on its own grass
    /// effect with the count hard-set to 1, out of its own arrays; that is a different effect with a different
    /// count, so this class must not try to own it and the two must not be unified. What the two paths do
    /// share is the campfire itself — <see cref="SceneRenderer.SavannaCampfirePosition"/>,
    /// <see cref="SceneRenderer.SavannaCampfireRange"/> and <see cref="SceneRenderer.CampfireColor"/> — which
    /// is what keeps the light on the grass and the light on the balls in step.
    /// </para>
    /// <para>
    /// A host that wants no scene lights simply never constructs this: the map editor does not, so
    /// <c>SceneLightCount</c> there keeps the HLSL uniform default of 0 and it lights no balls. That is the
    /// reason the shader-side loop has to cost nothing at a count of zero, and why this class never pushes a
    /// count it was not asked for.
    /// </para>
    /// </summary>
    public sealed class SceneLights
    {
        /// <summary>
        /// Light slots, <b>matched by <c>MAX_SCENE_LIGHTS</c> in <c>InstancedModel.fx</c> and
        /// <c>Savanna.fx</c></b> (and by <c>SceneRenderer</c>'s own grass-side arrays). Raising it here
        /// without raising it in the shaders silently writes past what the shader loop reads.
        /// </summary>
        public const int MaxLights = 8;

        //The three slot arrays, allocated once here and written in place. Two reasons they are fields and not
        //locals: Apply runs every frame (the campfire flickers, so this cannot be a set-once), and a fresh
        //array per frame would be a managed allocation on the gameplay path. What goes in them is only ever a
        //real light — an emissive surface that merely glows belongs in its own shader, not in a slot.
        private readonly Vector3[] _lightPosition = new Vector3[MaxLights];
        private readonly Vector3[] _lightColor = new Vector3[MaxLights];
        private readonly float[] _lightRange = new float[MaxLights];

        //Resolved once in the constructor, for the same per-frame reason: Effect.Parameters["name"] is a
        //linear scan over the instanced effect's ~70 parameters, and Apply would pay four of them a frame.
        private readonly EffectParameter _lightPositionParam;
        private readonly EffectParameter _lightColorParam;
        private readonly EffectParameter _lightRangeParam;
        private readonly EffectParameter _lightCountParam;

        //What was last pushed. A scene with no lights only has to send its zero once — nothing in Apply
        //touches the arrays while the count is zero, so re-sending them every frame writes four parameters
        //that cannot have changed. Seeded at -1 rather than 0, so the first frame always pushes, whatever
        //the scene turns out to be.
        private int _lastCount = -1;

        //The one light a caller may add for a single frame (#389): a blast's flash. Stated per frame and
        //consumed by the next Apply, so a flash can never outlive the frame that asked for it — and a host
        //that never asks (the front end, the Testbed, the editor) never lights one. A range of zero is "none".
        private Vector3 _flashPosition;
        private Vector3 _flashColor;
        private float _flashRange;

        /// <summary>
        /// The volcano's flows for a host that draws no <see cref="SceneRenderer"/> (#795): the Raspberry Pi's Potato
        /// path builds no backdrop, and without one it had no lamps to light the cluster with, which on a dusk dome left
        /// the balls near black. It solves a <see cref="VolcanoLava"/> of its own, on the same config and seed, and
        /// <see cref="Apply"/> takes the lamps from it when it is handed no renderer. A host with a renderer leaves this
        /// null and the renderer's own is read.
        /// </summary>
        public VolcanoLava Volcano { get; set; }

        /// <summary>
        /// Adds one short-lived light to the <b>next</b> <see cref="Apply"/> only (#389) — a blast lighting up
        /// the balls, the island and the gun around it for the fraction of a second it lasts. It takes the
        /// first slot the scene has left free and is dropped when there is none: the savanna's ring of
        /// campfires can fill all <see cref="MaxLights"/>, and a flash that evicted a fire would put that fire
        /// out for a frame, which is a flicker rather than a flash.
        /// </summary>
        /// <param name="color">Linear radiance, as every slot here takes it.</param>
        /// <param name="range">Where the light has fallen to nothing; zero or less adds nothing.</param>
        public void SetFlash(Vector3 position, Vector3 color, float range)
        {
            _flashPosition = position;
            _flashColor = color;
            _flashRange = range;
        }

        /// <summary>
        /// Caches the four parameter references off the shared instanced effect — the effect the balls, the
        /// island, the cannon/gun and the city all draw through, since one push has to reach all of them.
        /// </summary>
        public SceneLights(Effect instancedEffect)
        {
            _lightPositionParam = instancedEffect.Parameters["SceneLightPosition"];
            _lightColorParam = instancedEffect.Parameters["SceneLightColor"];
            _lightRangeParam = instancedEffect.Parameters["SceneLightRange"];
            _lightCountParam = instancedEffect.Parameters["SceneLightCount"];
        }

        /// <summary>
        /// Builds and pushes this frame's scene lights. Allocates nothing: the three arrays are the
        /// component's own and are overwritten in place.
        /// </summary>
        /// <param name="scene">The scene being drawn; decides which set of lights (if any) is built.</param>
        /// <param name="sceneRenderer">Where the campfire and the planetshine come from — the same source the
        /// grass shader and the flame billboard read, which is what keeps them consistent. Null on the Potato path
        /// (#795), which has none: then the volcano's lamps come from <see cref="Volcano"/>, the neon ring from
        /// <paramref name="neonLook"/> as always, and the scenes whose lights only a backdrop knows (the campfires, the
        /// big top, the planetshine, the earthshine, the lightning) push none.</param>
        /// <param name="neonLook">The neon city's ring (count, range, radius, height, colours), read only when
        /// <paramref name="scene"/> is <see cref="SceneKind.NeonCity"/>. Passed by reference, never copied.</param>
        /// <param name="wallClock">Must be the same clock the caller feeds <see cref="SceneFrame.Time"/>, or
        /// the campfire's light and its flame billboard flicker out of step. A wall clock, not simulation
        /// time: the fire keeps burning while the simulation is paused or slowed.</param>
        public void Apply(SceneKind scene, SceneRenderer sceneRenderer, NeonConfig neonLook, float wallClock)
        {
            int count = 0;

            //The seven guards below are mutually exclusive by construction — a scene is the volcano, or the
            //savanna, or the neon city, or space, or the Moon, or the storm, or the big top (each TryGet returns
            //false for every kind but its own), and no SceneKind satisfies two of them. So this order is an order
            //and not a precedence: do not read it as one, and do not write an eighth branch that relies on being
            //tested last. The storm's own branch is additionally self-gating in TIME as well as in kind —
            //between strikes its TryGet returns false and the scene takes no slot at all. The ONE branch that is a
            //precedence is the renderer-less one (#795): it has to stand after the volcano, which can light without
            //a renderer, and before every branch that reads one.
            if (scene == SceneKind.Volcano && (sceneRenderer?.VolcanoLava ?? Volcano) is VolcanoLava lava)
            {
                //The volcano is the scene whose GROUND is the light, and the only one whose lamps MOVE: the
                //crater sits still while the rest are flow fronts travelling down the flank, so a slot is a
                //front rather than a fixture. Everything comes out of the flows' own figures for the same reason
                //the campfire's light comes out of the renderer — the flank's own shader draws those rivers, and a
                //lamp beside the river it is lighting is worse than no lamp at all.
                count = lava.LightCount;

                for (int light = 0; light < count; light++)
                {
                    _lightPosition[light] = lava.LightPosition(light, wallClock);
                    _lightColor[light] = lava.LightColor(wallClock, light);
                    _lightRange[light] = lava.LightRange;
                }
            }
            else if (sceneRenderer == null)
            {
                //No renderer (the Potato path): only the neon ring, which is config, and not a backdrop's
                if (scene == SceneKind.NeonCity) count = BuildNeonRing(neonLook);
            }
            else if (scene == SceneKind.Savanna)
            {
                //The ring of campfires on the grass around the island, each flickering off the same wall clock
                //its own flame billboard does — one clock, so a light and its fire cannot fall out of step,
                //and each fire's own phase, so the ring does not beat in unison.
                count = sceneRenderer.SavannaCampfireCount;

                for (int fire = 0; fire < count; fire++)
                {
                    _lightPosition[fire] = sceneRenderer.SavannaCampfirePosition(fire);
                    _lightColor[fire] = sceneRenderer.CampfireColor(wallClock, fire);
                    _lightRange[fire] = sceneRenderer.SavannaCampfireRange;
                }
            }
            else if (sceneRenderer.TryGetSpacePlanetshine(scene, out Vector3 shinePosition, out Vector3 shineColor, out float shineRange))
            {
                //Planetshine: the light the planet throws back on the island's flank. A real light rather than
                //more ambient, so it is directional and so the metallic drain beads — which have almost
                //nothing but reflections to show — get a highlight back out of it.
                _lightPosition[0] = shinePosition;
                _lightColor[0] = shineColor;
                _lightRange[0] = shineRange;
                count = 1;
            }
            else if (sceneRenderer.TryGetMoonEarthshine(scene, out Vector3 earthPosition, out Vector3 earthColor, out float earthRange))
            {
                //Earthshine: the planetshine's argument at the Earth's colour — the cool fill the Earth
                //throws back onto the island, directional and able to put a highlight into the gold beads.
                _lightPosition[0] = earthPosition;
                _lightColor[0] = earthColor;
                _lightRange[0] = earthRange;
                count = 1;
            }
            else if (sceneRenderer.TryGetStormFlash(scene, wallClock, out Vector3 flashPosition, out Vector3 flashColor, out float flashRange))
            {
                //The lightning: a lamp far below the island, on the planetshine's own recipe, alight only
                //for the fraction of a second a strike lasts (the TryGet returns false between strikes, so
                //most frames take no slot at all). It is here because it is the ONLY channel that reaches
                //the play camera — the deck throwing the flash sits below the island's own occluding line
                //and is largely out of that frame, while its light is not. See TryGetStormFlash.
                _lightPosition[0] = flashPosition;
                _lightColor[0] = flashColor;
                _lightRange[0] = flashRange;
                count = 1;
            }
            else if (scene == SceneKind.Circus)
            {
                //The big top's footlights (#690): a ring of warm lamps on the curb, so the island's drum - a vertical wall
                //the key from the rig overhead only grazes - is lit from whichever side the camera stands on; and the
                //pools the three island spots throw on its cap, moving with the spots' own wandering aim
                count = Math.Min(sceneRenderer.CircusLightCount, MaxLights);

                for (int i = 0; i < count; i++)
                {
                    _lightPosition[i] = sceneRenderer.CircusLightPosition(i, wallClock);
                    _lightColor[i] = sceneRenderer.CircusLightColor(i, wallClock);
                    _lightRange[i] = sceneRenderer.CircusLightRange(i);
                }
            }
            else if (scene == SceneKind.NeonCity)
            {
                count = BuildNeonRing(neonLook);
            }

            //And a flash over the scene's own lamps, in the first slot they left free (#389). Consumed here
            //whether or not it found one, so a flash lights the frame that asked for it and no other.
            if (_flashRange > 0f)
            {
                if (count < MaxLights)
                {
                    _lightPosition[count] = _flashPosition;
                    _lightColor[count] = _flashColor;
                    _lightRange[count] = _flashRange;
                    count++;
                }

                _flashRange = 0f;
            }

            //A scene with no lights only needs the count pushed the first time it goes to zero — nothing
            //above touches the arrays while the count is zero, so once the zero has gone out there is nothing
            //left that could have changed. Measured as four parameter writes a frame saved, on the ten
            //scenes that carry no lights of their own.
            if (count == 0 && _lastCount == 0) return;

            _lightPositionParam.SetValue(_lightPosition);
            _lightColorParam.SetValue(_lightColor);
            _lightRangeParam.SetValue(_lightRange);
            _lightCountParam.SetValue(count);

            _lastCount = count;
        }

        //A ring of alternating magenta and cyan around the island, so the near towers, the island and the balls
        //actually take the neon's colour rather than the windows merely glowing at them. Answers how many slots it took.
        private int BuildNeonRing(NeonConfig neonLook)
        {
            int count = Math.Min(neonLook.LightCount, MaxLights);

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * MathHelper.TwoPi;
                _lightPosition[i] = new Vector3(MathF.Cos(angle) * neonLook.LightRadius, neonLook.LightHeight, MathF.Sin(angle) * neonLook.LightRadius);
                _lightColor[i] = (i % 2 == 0) ? neonLook.Magenta.ToVector3() : neonLook.Cyan.ToVector3();
                _lightRange[i] = neonLook.LightRange;
            }

            return count;
        }
    }
}
