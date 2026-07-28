using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The round platform the game is played on: a cast-concrete drum with a dressed stone top, a moulded
    /// coping around its rim and a drain bored through the middle. It replaces the plain extruded washer
    /// the arena used to be — a cylinder with a hole, whose every edge was a raw 90° cut.
    /// <para>
    /// <b>Two meshes, because it is two materials.</b> <see cref="Cap"/> is the dressed stone — the flat
    /// top the balls rest on and the coping that finishes it — and <see cref="Body"/> is the rough
    /// concrete under it: the wall, the string course, the base and the bore's shaft. A colour is a
    /// per-draw uniform, so one mesh could only ever be one material; splitting the profile at the
    /// coping's drip is what lets the stone be stone and the concrete be concrete. They are built from one
    /// polyline and meet on a shared point, so they cannot drift apart.
    /// </para>
    /// <para>
    /// <b>The shaping is what does the work, and it is all downward.</b> There are no drawn shadows here,
    /// so an edge reads only through the hemisphere ambient — which is exactly enough: an upward-facing
    /// chamfer takes the sky and draws a bright ring, and the underside of the coping's overhang takes the
    /// ground and draws a dark one. That pair of lines around the rim is most of the difference between a
    /// platform and an extruded circle. Every one of them is cut <i>below</i> the top plane, because the
    /// physics floor is a flat disc at y = 0 and anything raised above it would be a lip that balls pass
    /// straight through.
    /// </para>
    /// <para>
    /// Origin is the centre of the top face (y = 0); the solid descends to y = -height.
    /// </para>
    /// </summary>
    public sealed class IslandMesh : IDisposable
    {
        /// <summary>The dressed stone: the flat top and the moulded coping down to its drip.</summary>
        public LatheMesh Cap { get; private set; }

        /// <summary>The rough concrete drum under the stone: wall, string course, base and the bore's shaft.</summary>
        public LatheMesh Body { get; private set; }

        /// <summary>
        /// How far in from the outer radius the flat top ends and the coping's fall begins. The physics
        /// floor should stop here rather than at the outer radius, or a ball rests on air over the wash.
        /// </summary>
        public const float COPING_WIDTH = 1.3f;

        /// <summary>
        /// Outer radius of the flat, level part of the top — the disc that is actually walkable, and what
        /// a collision mesh built for this platform should use as its own outer edge.
        /// </summary>
        public static float FloorRadius(float outerRadius) => outerRadius - COPING_WIDTH;

        //How far the concrete is set back from the coping's outer face. The overhang is the point: it is
        //what casts the dark line the rim reads by, and what keeps the coping a distinct member rather
        //than the top of one flush wall.
        private const float WALL_INSET = 0.55f;

        //Peak radial wander of the concrete, in world units. Kept under WALL_INSET so the drum never
        //swells past the coping it hangs under - the stone rim stays a true circle (it is cut, and its
        //junction with the drain's gold bead has to close exactly), and only the cast material below it
        //is irregular.
        private const float IRREGULARITY = 0.3f;

        /// <param name="boreRadius">Radius of the central hole the drain funnel's mouth fills.</param>
        /// <param name="outerRadius">Radius of the coping's outer face — the platform's widest point.</param>
        /// <param name="height">Drop from the top face to the underside.</param>
        /// <param name="segments">Facets around the platform.</param>
        public IslandMesh(GraphicsDevice graphicsDevice, float boreRadius, float outerRadius, float height, int segments)
        {
            float r = outerRadius;

            //The stone, from the bore outward. The top face is flat and level out to the coping, then
            //falls: a small chamfer off the face, a wash across the coping's head and a bullnose turning
            //down into its vertical face. The three run smoothly into one another so the nose reads round;
            //the arris where the flat top ends is a crease, or the last span of the floor would shade as a
            //curve and the top would look domed.
            //(A lathe's two end points crease by construction — they have no span across the junction to
            //be smoothed with — so only the creases in the middle of a run are marked.)
            var cap = new List<LathePoint>
            {
                new(boreRadius,      0f),                     //the bore's lip, under the drain's gold bead
                new(r - COPING_WIDTH, 0f,    crease: true),   //the flat top ends; everything past here falls away
                new(r - 1.05f,       -0.12f),                 //chamfer off the face onto the coping
                new(r - 0.34f,       -0.22f),                 //the coping's wash - a shallow fall to shed water
                new(r - 0.12f,       -0.36f),                 //the bullnose begins to turn down
                new(r,               -0.66f),                 //its outer arris: the platform's widest point
                new(r,               -1.55f, crease: true),   //the coping's vertical face
                new(r - WALL_INSET,  -1.82f)                  //the drip: the coping oversails the concrete
            };

            //The concrete, carrying on from the drip and back to the bore. The wall is battered slightly
            //inward, flares out into a string course a third of the way down, and then splays into a plinth
            //that carries the foot back out to very nearly the coping's radius. The wander fades in below
            //the drip and out again at the underside, so the shared edge with the stone and the bore the
            //drain has to meet are both left as true circles.
            //
            //The plinth flares rather than chamfering in, and that is load-bearing twice over. A base that
            //widens is how a real one carries weight — one that tapers reads as balanced on a point — and
            //the foot's radius is also what covers the hole the terrain scenes cut out of the ground under
            //the platform (see the callers' TerrainHoleRadius). The island floats half a unit over that
            //ground, so a foot drawn in much narrower than the platform opens a slot at every grazing angle
            //and the sky shows through it as a pale ring: an earlier profile chamfered in to r - 1.15 and
            //did exactly that.
            var body = new List<LathePoint>
            {
                new(r - WALL_INSET,  -1.82f),                                  //shared with the cap's last point
                new(r - 0.68f,       -2.05f,                   wobble: 0.35f),
                new(r - 0.78f,       -3.2f,                    wobble: 1f),    //the wall, battered
                new(r - 0.42f,       -3.62f, crease: true,     wobble: 1f),    //the string course flares out
                new(r - 0.4f,        -4.2f,  crease: true,     wobble: 1f),    //its face
                new(r - 0.66f,       -4.46f,                   wobble: 0.8f),  //its drip, cut back under
                new(r - 0.44f,       -4.8f,                    wobble: 0.6f),  //the plinth splays out
                new(r - 0.2f,        -height, crease: true,    wobble: 0.35f), //its foot, all but flush with the coping
                new(boreRadius + 0.4f, -height, crease: true),                  //the underside
                new(boreRadius,      -height + 0.4f, crease: true),             //chamfer up into the bore
                new(boreRadius,      0f)                                        //the shaft, closing on the cap
            };

            Cap = new LatheMesh(graphicsDevice, cap, segments);
            Body = new LatheMesh(graphicsDevice, body, segments, IRREGULARITY);
        }

        public void Dispose()
        {
            Cap?.Dispose();
            Cap = null;
            Body?.Dispose();
            Body = null;
        }
    }
}
