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
    /// Four of the twelve scenes carry lights: the savanna's campfire, the neon city's ring of magenta and
    /// cyan, space's planetshine and the Moon's earthshine. The other eight push a count of zero once and
    /// then cost nothing — see the early-out in <see cref="Apply"/>.
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
        /// grass shader and the flame billboard read, which is what keeps them consistent.</param>
        /// <param name="neonLook">The neon city's ring (count, range, radius, height, colours), read only when
        /// <paramref name="scene"/> is <see cref="SceneKind.NeonCity"/>. Passed by reference, never copied.</param>
        /// <param name="wallClock">Must be the same clock the caller feeds <see cref="SceneFrame.Time"/>, or
        /// the campfire's light and its flame billboard flicker out of step. A wall clock, not simulation
        /// time: the fire keeps burning while the simulation is paused or slowed.</param>
        public void Apply(SceneKind scene, SceneRenderer sceneRenderer, NeonConfig neonLook, float wallClock)
        {
            int count = 0;

            //The four guards below are mutually exclusive by construction — a scene is the savanna, or the
            //neon city, or space, or the Moon (each TryGet returns false for every kind but its own), and no
            //SceneKind satisfies two of them. So this order is an order and not a precedence: do not read it
            //as one, and do not write a fifth branch that relies on being tested last.
            if (scene == SceneKind.Savanna)
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
            else if (scene == SceneKind.NeonCity)
            {
                //A ring of alternating magenta and cyan around the island, so the near towers, the island and
                //the balls actually take the neon's colour rather than the windows merely glowing at them
                count = Math.Min(neonLook.LightCount, MaxLights);

                for (int i = 0; i < count; i++)
                {
                    float angle = i / (float)count * MathHelper.TwoPi;
                    _lightPosition[i] = new Vector3(MathF.Cos(angle) * neonLook.LightRadius, neonLook.LightHeight, MathF.Sin(angle) * neonLook.LightRadius);
                    _lightColor[i] = (i % 2 == 0) ? neonLook.Magenta.ToVector3() : neonLook.Cyan.ToVector3();
                    _lightRange[i] = neonLook.LightRange;
                }
            }

            //A scene with no lights only needs the count pushed the first time it goes to zero — nothing
            //above touches the arrays while the count is zero, so once the zero has gone out there is nothing
            //left that could have changed. Measured as four parameter writes a frame saved, on the eight
            //scenes that carry no lights of their own.
            if (count == 0 && _lastCount == 0) return;

            _lightPositionParam.SetValue(_lightPosition);
            _lightColorParam.SetValue(_lightColor);
            _lightRangeParam.SetValue(_lightRange);
            _lightCountParam.SetValue(count);

            _lastCount = count;
        }
    }
}
