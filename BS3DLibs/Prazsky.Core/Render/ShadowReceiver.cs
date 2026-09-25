using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One effect that reads the sun's shadow map (#471): the five parameters <c>Shadows.fxh</c> declares,
    /// looked up once and pushed together. Everything that receives a shadow — every terrain shader, the
    /// plants, and the shared instanced effect that carries the island, the gun and the balls — is one of
    /// these, and <see cref="SceneRenderer.DrawShadowMaps"/> walks an array of them.
    /// <para>
    /// <b>Why a type at all.</b> While the savanna was the only scene with a map, its three receivers were
    /// three named quintets of fields, and the shape of that code was itself the reason the feature read as a
    /// savanna feature rather than as the infrastructure it is: adding the meadow meant adding five more
    /// fields, five more lookups and five more <c>SetValue</c> lines in three places. Ten scenes later that
    /// is fifty. The lookups still happen once at load — the by-name indexer is a linear scan and this is
    /// <c>BestPractices.md</c> §1 — and the push is still a plain indexed loop over a struct array, so
    /// nothing here allocates per frame.
    /// </para>
    /// <para>
    /// <b>An effect that declares none of them is a valid, silent no-op</b> (<see cref="IsValid"/> false, both
    /// calls return immediately). That is deliberate: <c>Shadows.fxh</c> is included by the receivers that
    /// want it, and a caller may hand in an effect built before it did — the map editor's and the Testbed's
    /// shaders are the same files, but a host's own effect need not be.
    /// </para>
    /// </summary>
    internal readonly struct ShadowReceiver
    {
        private readonly EffectParameter _map, _viewProjection, _texel, _strength, _bias;

        //The ceiling's glass (#553), which casts through arithmetic in the receiver rather than through the map
        //(see Shadows.fxh's CeilingGlassShadow): where the slab stands, its size, and its cut
        private readonly EffectParameter _ceilingCentre, _ceilingSize, _ceilingCut;

        /// <summary>Looks the five up on <paramref name="effect"/>. A null effect, or one that includes no
        /// <c>Shadows.fxh</c>, yields an invalid receiver that does nothing.</summary>
        public ShadowReceiver(Effect effect)
        {
            _map = effect?.Parameters["ShadowMap"];
            _viewProjection = effect?.Parameters["ShadowViewProjection"];
            _texel = effect?.Parameters["ShadowTexel"];
            _strength = effect?.Parameters["ShadowStrength"];
            _bias = effect?.Parameters["ShadowBias"];
            _ceilingCentre = effect?.Parameters["CeilingShadowCentre"];
            _ceilingSize = effect?.Parameters["CeilingShadowSize"];
            _ceilingCut = effect?.Parameters["CeilingShadowCut"];
        }

        /// <summary>Whether this effect actually reads the map. <see cref="_strength"/> is the one that
        /// decides, because it is the uniform the receivers' <c>[branch]</c> gates on.</summary>
        public bool IsValid => _strength != null;

        /// <summary>
        /// Hands this frame's map to the effect. <paramref name="bias"/> is already in the map's own depth
        /// units (world units over its depth range), because every receiver of one map shares it.
        /// </summary>
        public void Push(Texture map, Matrix viewProjection, float texel, float strength, float bias)
        {
            if (_strength == null) return;

            _map.SetValue(map);
            _viewProjection.SetValue(viewProjection);
            _texel.SetValue(texel);
            _strength.SetValue(strength);
            _bias.SetValue(bias);
        }

        /// <summary>
        /// Hands this frame's ceiling glass to the effect (#553): the slab's centre with, in <c>w</c>, how much of
        /// the sun its uncut glass takes (0 = no plate, and the receiver skips the whole term), its half extents
        /// with its corner radius, and its cut. Read only under the map's own gate, so a frame with no map needs
        /// nothing from here. Null-checked apart from the map's five, on the same terms as they are: an effect that
        /// declares none of them does nothing.
        /// </summary>
        public void PushCeiling(Vector4 centre, Vector4 size, Vector4 cut)
        {
            if (_ceilingCentre == null) return;

            _ceilingCentre.SetValue(centre);
            _ceilingSize?.SetValue(size);
            _ceilingCut?.SetValue(cut);
        }

        /// <summary>
        /// Says "no map this frame". Strength alone, because that is what the receivers branch on: the other
        /// four are never read while it is 0, and clearing the texture reference would drop a target the next
        /// frame may want back.
        /// </summary>
        public void Disable() => _strength?.SetValue(0f);
    }
}
