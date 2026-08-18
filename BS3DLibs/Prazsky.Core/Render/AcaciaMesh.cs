using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A savanna acacia: a short tapering trunk (<see cref="Trunk"/>) under a wide, <b>flat-topped umbrella
    /// canopy</b> (<see cref="Canopy"/>) — the parasol silhouette that says "savanna" at a glance. Two meshes,
    /// because it is two materials — bark and foliage — which a single instanced draw cannot tint differently
    /// (the diffuse colour is per-draw, not per-instance). The pattern is <see cref="TreeMesh"/>'s: one object,
    /// two <see cref="IProceduralMesh"/>es, two renderers sharing one instance matrix.
    /// <para>
    /// It replaces the flat billboard (<c>Acacia.fx</c>) that read as a paper cutout (#202): a real surface of
    /// revolution has volume from every angle, where a camera-facing quad only ever shows one silhouette. Both
    /// parts are <see cref="LatheMesh"/>es, so the acacia keeps the codebase's procedural-mesh toolkit rather
    /// than an asset — the canopy is exactly the shape a lathe is for: a flat top run out to a thin rim, then a
    /// domed underside curving back in to a thick middle, so it is fat in the centre and thins to the sides.
    /// The rim's wobble is what stops the umbrella reading as a machined disc.
    /// </para>
    /// <para>
    /// Everything structural is rolled from the <paramref name="seed"/> — the trunk's flare and the wobble
    /// phase of both parts — so two variants differ in texture, not merely in proportions (the caller builds a
    /// few at different sizes). Profiles trace <b>top → outside → underside</b>, the direction
    /// <see cref="LatheMesh"/> documents; traced the other way the solid comes out inside out.
    /// </para>
    /// </summary>
    public sealed class AcaciaMesh : IDisposable
    {
        /// <summary>The trunk: a short tapering bark cylinder with a root flare, under the canopy's middle.</summary>
        public LatheMesh Trunk { get; }

        /// <summary>The canopy, sitting on the trunk's top: a wide, flat-topped, lumpy umbrella of foliage.</summary>
        public LatheMesh Canopy { get; }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="trunkBaseRadius">Trunk radius up the flank; the root flare at the ground is wider.</param>
        /// <param name="trunkTopRadius">Trunk radius where the canopy takes over (a trunk tapers).</param>
        /// <param name="trunkHeight">Trunk height to the underside of the canopy's middle.</param>
        /// <param name="canopyRadius">Half-width of the umbrella — the widest thing about the tree.</param>
        /// <param name="canopyThickness">Top-to-underside depth of the canopy at its thick middle.</param>
        /// <param name="seed">Rolls the flare and the wobble phases. The same seed always builds the same tree.</param>
        /// <param name="segments">Facets around the trunk axis. The canopy uses its own, higher, count.</param>
        public AcaciaMesh(GraphicsDevice graphicsDevice,
            float trunkBaseRadius, float trunkTopRadius, float trunkHeight,
            float canopyRadius, float canopyThickness, int seed = 0, int segments = 8)
        {
            Random rng = new(seed);

            //Spreads the meshes' wobble patterns apart; the golden angle keeps consecutive seeds from landing
            //on nearby phases of the low-frequency terms — TreeMesh's own reasoning.
            float phase = seed * 2.39996f;

            //Top rim, down the flank, out into a root flare, and in along the buried underside. No top cap: the
            //canopy closes over the trunk's top, so a cap would never be seen.
            float flare = 1.2f + 0.25f * (float)rng.NextDouble();
            var trunkProfile = new List<LathePoint>
            {
                new(trunkTopRadius,          trunkHeight,        wobble: 0.7f),
                new(trunkBaseRadius,         trunkHeight * 0.35f, wobble: 1f),
                new(trunkBaseRadius * flare, 0f,                 crease: true, wobble: 1f), //root flare into the ground
                new(0f,                      0f)
            };

            Trunk = new LatheMesh(graphicsDevice, trunkProfile, segments,
                irregularityAmplitude: trunkBaseRadius * (0.06f + 0.05f * (float)rng.NextDouble()),
                irregularityPhase: phase);

            Canopy = BuildCanopy(graphicsDevice, canopyRadius, canopyThickness, trunkHeight, rng, phase);
        }

        /// <summary>
        /// The umbrella canopy: a flat top run out to a thin rim, then a domed underside curving back in to a
        /// thick middle. The rim is the widest ring and where the top and underside meet, so the edge is a
        /// knife rather than a wall — an acacia's canopy has no thickness at its edge. The underside centre
        /// hangs lowest (a canopy is fat in the middle), sitting on the trunk's top so the trunk shows beneath
        /// the spread. Wobble on every ring, heaviest at the rim, is what breaks the disc into foliage.
        /// </summary>
        private static LatheMesh BuildCanopy(GraphicsDevice graphicsDevice, float radius, float thickness,
            float baseY, Random rng, float phase)
        {
            float top = baseY + thickness;              //flat top plane
            float rimY = top - thickness * 0.14f;       //rim just under the top: the umbrella reads flat-topped
            float underY = top - thickness * 0.62f;     //underside curving down and in

            //Top centre → flat plateau → thin rim → domed underside → thick middle. The middle underside sits
            //at baseY (on the trunk top); the flat top is `thickness` above it.
            var profile = new List<LathePoint>
            {
                new(0f,             top,   wobble: 0.35f),
                new(radius * 0.62f, top,   wobble: 0.7f),
                new(radius,         rimY,  wobble: 1f),   //widest, thinnest — the silhouette the eye traces
                new(radius * 0.55f, underY, wobble: 0.85f),
                new(0f,             baseY, wobble: 0.3f)
            };

            //Fourteen facets, for RockMesh's / the conifer's reason: the wobble runs at 3, 7 and 13 waves per
            //revolution, and too few facets alias the 7-wave term into a lateral shift, which the rim (the one
            //ring the eye traces) would show. Amplitude ~15% of the radius, rolled, so no two canopies lump the
            //same way.
            return new LatheMesh(graphicsDevice, profile, segments: 14,
                irregularityAmplitude: radius * (0.13f + 0.06f * (float)rng.NextDouble()),
                irregularityPhase: phase);
        }

        public void Dispose()
        {
            Trunk?.Dispose();
            Canopy?.Dispose();
        }
    }
}
