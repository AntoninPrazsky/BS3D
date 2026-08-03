using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using System;
using System.Numerics;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// The island's whole physics floor, and it is the drain's own surface: the sloped glass cone from its rim
    /// down to the hole, plus the flat stone ring from that rim out to the edge of the platform's level top, as
    /// one Bepu triangle mesh. Balls rest on the ring, run down the cone at its ~55° and drop through the hole;
    /// past the ring they fall off the island's edge into whatever the scene has below it. Either way the
    /// caller's kill plane takes them. Nothing else about the island collides — the drawn stone, the coping and
    /// the drum are visual only.
    /// <para>
    /// It existed line-for-line in the Testbed and in the Game's session until #75, down to the same eight
    /// triangles a segment, and the two had drifted only in the arithmetic used for the segment angle (one in
    /// <see cref="float"/> throughout, the other through a <see cref="double"/> intermediate) and in whether
    /// the ring's outer radius was hoisted out of the loop or recomputed twice per segment. This takes the
    /// float path and the hoisted radius; on a 64-segment cone neither difference was ever visible.
    /// </para>
    /// </summary>
    public static class FunnelPhysics
    {
        /// <summary>
        /// Builds the funnel-and-ring collider and adds it to <paramref name="simulation"/> as a static at
        /// <c>(0, <paramref name="topY"/>, 0)</c>. It lives for the simulation's lifetime — neither executable
        /// has ever needed to remove it, because a new level gets a new simulation — so nothing is returned:
        /// disposing the simulation and clearing the pool releases the shape and its triangle buffer.
        /// <para>
        /// Every quad goes in with <b>both</b> windings — eight triangles a segment, not four. A Bepu mesh
        /// triangle only collides on its front face, and this surface is met from above, from inside the funnel
        /// and from underneath; rather than depend on getting the winding right for all three it is made
        /// double-sided deliberately, so a ball can never slip through it.
        /// </para>
        /// </summary>
        /// <param name="simulation">The simulation the static is added to.</param>
        /// <param name="bufferPool">The pool the triangle buffer is taken from. The caller keeps ownership: the
        /// buffer is handed to the <see cref="Mesh"/> and released by the pool's own teardown, not here.</param>
        /// <param name="topY">World Y of the rim — the island's top surface, which the rim is flush with. The
        /// static's pose is what puts the locally-built cone there.</param>
        /// <param name="bottomY">World Y of the hole the balls fall through. Well above the kill plane, so a
        /// ball that drops through falls a visible distance before it is culled.</param>
        /// <param name="topRadius">The rim's radius, i.e. the drain's mouth. The hole below it is wider than a
        /// ball, so nothing collides there.</param>
        /// <param name="holeRadius">The bottom hole's radius.</param>
        /// <param name="floorRadius">Outer radius of the flat stone ring, which is the edge of the platform's
        /// <b>level</b> top rather than of the platform: the coping falls away over the last stretch out to the
        /// island's own radius, and a floor carried out to the widest point would hold a ball up on air over
        /// the wash. <c>IslandMesh.FloorRadius</c> is what answers this — computed once by the caller, since
        /// this library cannot see Prazsky.Core's meshes.</param>
        /// <param name="segments">Angular tessellation, matched to the drawn funnel's own.</param>
        public static void Build(Simulation simulation, BufferPool bufferPool, float topY, float bottomY,
            float topRadius, float holeRadius, float floorRadius, int segments)
        {
            float depth = topY - bottomY;

            //Take gives exactly the requested length, which is what the Mesh constructor is handed;
            //TakeAtLeast would round the count up and leave uninitialised triangles at the end of the buffer.
            bufferPool.Take<Triangle>(segments * 8, out Buffer<Triangle> triangles);

            for (int s = 0; s < segments; s++)
            {
                float a0 = s / (float)segments * MathF.PI * 2f;
                float a1 = (s + 1) / (float)segments * MathF.PI * 2f;

                //Local space: the rim at y = 0 and the hole at y = -depth, so the static's own pose is what
                //puts the rim flush with the island's stone top
                Vector3 t0 = Ring(a0, topRadius, 0f);
                Vector3 t1 = Ring(a1, topRadius, 0f);
                Vector3 h0 = Ring(a0, holeRadius, -depth);
                Vector3 h1 = Ring(a1, holeRadius, -depth);
                Vector3 r0 = Ring(a0, floorRadius, 0f);   //the flat ring's outer edge, where the coping begins
                Vector3 r1 = Ring(a1, floorRadius, 0f);

                int b = s * 8;

                //The cone wall, both faces
                triangles[b] = new Triangle(t0, h0, t1);
                triangles[b + 1] = new Triangle(t1, h0, h1);
                triangles[b + 2] = new Triangle(t0, t1, h0);
                triangles[b + 3] = new Triangle(t1, h1, h0);

                //The flat stone ring from the rim out to the island's level edge, both faces
                triangles[b + 4] = new Triangle(t0, t1, r1);
                triangles[b + 5] = new Triangle(t0, r1, r0);
                triangles[b + 6] = new Triangle(t0, r1, t1);
                triangles[b + 7] = new Triangle(t0, r0, r1);
            }

            static Vector3 Ring(float angle, float radius, float y) =>
                new(radius * MathF.Cos(angle), y, radius * MathF.Sin(angle));

            Mesh mesh = new(triangles, Vector3.One, bufferPool);
            TypedIndex shape = simulation.Shapes.Add(mesh);

            simulation.Statics.Add(new StaticDescription(new Vector3(0f, topY, 0f), shape));
        }
    }
}
