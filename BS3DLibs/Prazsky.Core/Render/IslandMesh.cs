using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The silhouette an island wears (#533): one cross-section per family of scenes, resolved where the
    /// material already is (<c>ArenaIsland.LookFor</c>). Every shape keeps the figures everything standing on
    /// the island reads — the outer radius, the walkable top's arris at <see cref="IslandMesh.FloorRadius"/>
    /// and y = 0, the bore the drain's gold bead closes on, and a foot all but flush with the rim that covers
    /// the hole the terrain scenes cut under the platform — and changes only what stands between them.
    /// </summary>
    public enum IslandShape
    {
        /// <summary>The authored dressed stone on a cast drum: coping, wash, bullnose, string course, plinth.</summary>
        Stone,

        /// <summary>A flat-topped basalt block with a stepped rim: two tiers cut in hard steps, the drum
        /// recessed between them, and vertical joints on every face that read as columns (the volcano).</summary>
        Basalt,

        /// <summary>An ice floe: a rounded lip that turns under into a hollow the water has eaten out of the
        /// rim, the whole edge wandering as a broken floe's does (the polar sheet, the aurora).</summary>
        Ice,

        /// <summary>A low coral platform: a soft weathered nose with no arris anywhere below the floor, the
        /// drum a pitted lump (the tropical beach, the sea).</summary>
        Coral,

        /// <summary>A machined disc: a flat top out to a 45° chamfer, a true cylinder with a recessed groove
        /// round it, a stepped foot, no wander at all (space, the grid).</summary>
        Machined,

        /// <summary>A poured-concrete plinth: a hard arris, a straight battered side, a shallow foot, only a
        /// cast surface's trace of wander (the two cities).</summary>
        Plinth,

        /// <summary>A monolith's stub (#538): the top rounds over a shoulder into one leaning sweep of rock down to
        /// a foot that splays back out — no coping, no course, no plinth — wandering more than the stone, with
        /// the renderer's flutes cut down the side (the outback).</summary>
        Monolith,

        /// <summary>A landing pad cut into regolith (#538): a thin flat slab with a plain cut edge, and round its
        /// foot the spoil the cut threw up, banked against the edge and spreading past the rim onto the ground
        /// (the Moon).</summary>
        Pad
    }

    /// <summary>
    /// The round platform the game is played on: a cast-concrete drum with a dressed stone top, a moulded
    /// coping around its rim and a drain bored through the middle. It replaces the plain extruded washer
    /// the arena used to be — a cylinder with a hole, whose every edge was a raw 90° cut. Since #533 that is
    /// one of six silhouettes (<see cref="IslandShape"/>); the remarks below describe the authored stone, and
    /// the invariants they state hold for every shape.
    /// <para>
    /// <b>Two meshes, because it is two materials.</b> <see cref="Cap"/> is the dressed stone — the dished
    /// top the balls roll down and the coping that finishes it — and <see cref="Body"/> is the rough
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
    /// physics floor mirrors the walkable top exactly (its outer arris at y = 0) and anything raised above
    /// it would be a lip that balls pass straight through.
    /// </para>
    /// <para>
    /// <b>The walkable top is a shallow dish, not a flat disc</b>: level at its outer arris
    /// (<see cref="FloorRadius"/>, y = 0) and falling <c>dishDepth</c> to the bore's lip, so a ball that
    /// lands on the stone rolls into the drain instead of coming to rest on it. The collision floor a
    /// caller builds (<c>FunnelPhysics.Build</c>) has to be given the same <c>dishDepth</c>, or balls rest
    /// on air over the drawn stone.
    /// </para>
    /// <para>
    /// <b>What a shape may and may not move (#533).</b> The arris at <see cref="FloorRadius"/> is the
    /// physics floor's edge and stays a true circle at y = 0 in every shape — its ring never wobbles, or the
    /// drawn floor would reach past the collision and a ball would sink through stone. The bore's lip is where
    /// the drain's gold bead closes and never wobbles either. The foot stays within a fifth of a unit of the
    /// rim at the underside, because it is what covers the hole the terrain scenes cut out of the ground
    /// (see the callers' <c>TerrainHoleRadius</c>). Everything between those three is the shape's own, and a
    /// rim that wanders (the ice, the coral) wanders on the cap and the drum alike by the same amplitude and
    /// the same per-ring wobble at their shared point, which is what <see cref="LatheMesh"/> needs to keep the
    /// seam closed. The #533 references (a ring of columns standing as a parapet round the basalt block's top)
    /// showed the one thing no shape here may do — nothing stands above the floor's plane at the rim.
    /// </para>
    /// <para>
    /// Origin is the centre of the top face's outer arris (y = 0); the solid descends to y = -height.
    /// </para>
    /// </summary>
    public sealed class IslandMesh : IDisposable
    {
        /// <summary>The dressed stone: the flat top and the moulded coping down to its drip.</summary>
        public LatheMesh Cap { get; private set; }

        /// <summary>The rough concrete drum under the stone: wall, string course, base and the bore's shaft.</summary>
        public LatheMesh Body { get; private set; }

        /// <summary>Which silhouette this is.</summary>
        public IslandShape Shape { get; }

        /// <summary>How many shapes there are, for a caller keeping one mesh per shape.</summary>
        public static readonly int SHAPE_COUNT = Enum.GetValues<IslandShape>().Length;

        /// <summary>
        /// How far in from the outer radius the walkable top ends and the coping's fall begins. The physics
        /// floor should stop here rather than at the outer radius, or a ball rests on air over the wash.
        /// </summary>
        public const float COPING_WIDTH = 1.3f;

        /// <summary>
        /// Outer radius of the walkable part of the top — the dish's own arris, the one circle of it that
        /// sits at y = 0 — and what a collision mesh built for this platform should use as its outer edge.
        /// </summary>
        public static float FloorRadius(float outerRadius) => outerRadius - COPING_WIDTH;

        //How far the concrete is set back from the coping's outer face. The overhang is the point: it is
        //what casts the dark line the rim reads by, and what keeps the coping a distinct member rather
        //than the top of one flush wall.
        private const float WALL_INSET = 0.55f;

        //Peak radial wander of the authored concrete, in world units. Kept under WALL_INSET so the drum never
        //swells past the coping it hangs under - the stone rim stays a true circle (it is cut, and its
        //junction with the drain's gold bead has to close exactly), and only the cast material below it
        //is irregular. The other shapes state their own (see Build): a floe's whole rim wanders, a machined
        //disc's nothing does.
        private const float IRREGULARITY = 0.3f;

        /// <param name="boreRadius">Radius of the central hole the drain funnel's mouth fills.</param>
        /// <param name="outerRadius">Radius of the coping's outer face — the platform's widest point.</param>
        /// <param name="height">Drop from the top face to the underside.</param>
        /// <param name="segments">Facets around the platform.</param>
        /// <param name="dishDepth">How far the walkable top falls from its outer arris
        /// (<see cref="FloorRadius"/>, y = 0) to the bore's lip — the dish that rolls a landed ball into
        /// the drain. The bore's lip, and with it the drain's rim, sits this far below y = 0.</param>
        /// <param name="shape">Which silhouette to build (#533); the authored stone by default.</param>
        public IslandMesh(GraphicsDevice graphicsDevice, float boreRadius, float outerRadius, float height, int segments,
            float dishDepth, IslandShape shape = IslandShape.Stone)
        {
            Shape = shape;
            Build(graphicsDevice, boreRadius, outerRadius, height, segments, dishDepth, shape, out LatheMesh cap, out LatheMesh body);
            Cap = cap;
            Body = body;
        }

        private static void Build(GraphicsDevice device, float bore, float r, float height, int segments, float dish,
            IslandShape shape, out LatheMesh capMesh, out LatheMesh bodyMesh)
        {
            //Every shape starts at the bore's lip and rises to the floor's arris; every drum ends on the
            //underside and closes back up the bore's shaft. The three points the invariants above name.
            LathePoint lip = new(bore, -dish);
            LathePoint arris = new(r - COPING_WIDTH, 0f, crease: true);
            LathePoint[] shaft =
            {
                new(bore + 0.4f, -height, crease: true),          //the underside
                new(bore,        -height + 0.4f, crease: true),   //chamfer up into the bore
                new(bore,        -dish)                           //the shaft, closing on the cap's lip
            };

            List<LathePoint> cap;
            List<LathePoint> body;
            float wander;

            switch (shape)
            {
                case IslandShape.Basalt:
                    //A block cut in two hard tiers: the top tier's own face and tread, then the drum recessed
                    //under it and stepping back out to the foot. The rings wander a little more than the
                    //stone's so no two tiers are quite concentric, and the vertical joints the renderer cuts
                    //on every face (ArenaIsland's relief for this shape) read as the columns between them.
                    wander = 0.45f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.95f, -0.06f),                                    //a hair off the floor
                        new(r - 0.85f, -0.50f, crease: true, wobble: 0.5f),        //the top tier's face
                        new(r - 0.85f, -0.95f, crease: true, wobble: 0.5f),
                        new(r,         -0.95f, crease: true, wobble: 0.5f),        //its tread
                        new(r,         -1.55f, crease: true, wobble: 0.6f),        //the lower tier's face
                        new(r - WALL_INSET, -1.82f, wobble: 0.6f)                  //shared with the drum
                    };
                    body = new()
                    {
                        new(r - WALL_INSET, -1.82f, wobble: 0.6f),
                        new(r - 0.35f, -2.20f, crease: true, wobble: 0.8f),        //the drum's upper course
                        new(r - 0.35f, -3.30f, crease: true, wobble: 1f),
                        new(r - 0.85f, -3.40f, crease: true, wobble: 1f),          //recessed a course
                        new(r - 0.85f, -4.30f, crease: true, wobble: 0.9f),
                        new(r - 0.2f,  -4.40f, crease: true, wobble: 0.5f),        //out to the foot
                        new(r - 0.2f,  -height, crease: true, wobble: 0.3f)
                    };
                    break;

                case IslandShape.Ice:
                    //A floe: the lip turns over in a round and then UNDER, the water having eaten a hollow out
                    //of the rim, and the whole edge wanders by half a unit - a floe's rim is broken, not cut.
                    //The arris ring does not wander (the floor's edge), the bore does not (the bead), and the
                    //wander fades in between the two.
                    wander = 0.55f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.60f, -0.10f, wobble: 0.4f),
                        new(r - 0.15f, -0.45f, wobble: 0.7f),
                        new(r,         -0.90f, wobble: 0.9f),                       //the round lip's widest
                        new(r - 0.35f, -1.40f, wobble: 1f),                         //turning under
                        new(r - 0.90f, -1.82f, crease: true, wobble: 1f)           //shared: the hollow's mouth
                    };
                    body = new()
                    {
                        new(r - 0.90f, -1.82f, crease: true, wobble: 1f),
                        new(r - 1.40f, -2.50f, wobble: 1f),                         //the hollow
                        new(r - 1.10f, -3.40f, wobble: 1f),
                        new(r - 0.50f, -4.20f, wobble: 0.8f),                       //back out to the foot
                        new(r - 0.2f,  -height, crease: true, wobble: 0.4f)
                    };
                    break;

                case IslandShape.Coral:
                    //A weathered nose with no crease anywhere below the floor: the sea has rounded every arris
                    //off, and the drum is a lump rather than a wall. Wanders nearly as much as the floe.
                    wander = 0.40f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.90f, -0.08f, wobble: 0.3f),
                        new(r - 0.45f, -0.30f, wobble: 0.5f),
                        new(r - 0.10f, -0.75f, wobble: 0.6f),
                        new(r,         -1.25f, wobble: 0.7f),                       //the nose's widest
                        new(r - 0.30f, -1.82f, wobble: 0.8f)                        //shared with the drum
                    };
                    body = new()
                    {
                        new(r - 0.30f, -1.82f, wobble: 0.8f),
                        new(r - 0.55f, -2.70f, wobble: 1f),
                        new(r - 0.35f, -3.60f, wobble: 1f),
                        new(r - 0.20f, -4.40f, wobble: 0.8f),
                        new(r - 0.10f, -height, crease: true, wobble: 0.5f)
                    };
                    break;

                case IslandShape.Machined:
                    //A disc off a lathe in the other sense: the top runs flat out to a 45-degree chamfer, the
                    //side is a true cylinder with one recessed groove, the foot a step. No wander at all -
                    //a machined part is what the authored stone's wander exists to stop the island reading as,
                    //and here that is the point.
                    wander = 0f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.50f, -0.02f),                                    //the flat top's outer edge
                        new(r,         -0.52f, crease: true),                       //the chamfer
                        new(r,         -1.55f, crease: true),                       //the rim's face
                        new(r - WALL_INSET, -1.60f, crease: true)                  //a crisp undercut, shared
                    };
                    body = new()
                    {
                        new(r - WALL_INSET, -1.60f, crease: true),
                        new(r - WALL_INSET, -2.50f, crease: true),
                        new(r - 0.80f, -2.50f, crease: true),                       //the groove
                        new(r - 0.80f, -2.85f, crease: true),
                        new(r - WALL_INSET, -2.85f, crease: true),
                        new(r - WALL_INSET, -4.55f, crease: true),
                        new(r - 0.2f,  -4.55f, crease: true),                       //the foot's step
                        new(r - 0.2f,  -height, crease: true)
                    };
                    break;

                case IslandShape.Plinth:
                    //Poured concrete: a hard arris where the top meets the side, a straight side battered
                    //barely at all, a shallow foot. The trace of wander a cast surface has and no more.
                    wander = 0.12f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.06f, -0.04f),
                        new(r,         -0.12f, crease: true),                       //the arris
                        new(r,         -1.55f, crease: true),
                        new(r - WALL_INSET, -1.82f)                                 //shared with the drum
                    };
                    body = new()
                    {
                        new(r - WALL_INSET, -1.82f),
                        new(r - 0.60f, -2.10f, wobble: 0.15f),
                        new(r - 0.62f, -3.90f, wobble: 0.15f),
                        new(r - 0.2f,  -4.15f, crease: true, wobble: 0.1f),         //the foot
                        new(r - 0.2f,  -height, crease: true, wobble: 0.1f)
                    };
                    break;

                case IslandShape.Monolith:
                    //A monolith's stub (#538): the top rounds over a shoulder into a side that leans in a little
                    //and runs in one sweep to a foot that splays back out - no coping, no course, no plinth, the
                    //one body of rock Uluru is - wandering more than the stone. The renderer cuts the flutes
                    //into the side (ArenaIsland's relief for this shape: wide, shallow, wandering joints).
                    wander = 0.5f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.80f, -0.10f, wobble: 0.3f),
                        new(r - 0.30f, -0.45f, wobble: 0.6f),                       //the shoulder
                        new(r,         -1.20f, wobble: 0.8f),
                        new(r - 0.10f, -1.82f, wobble: 0.9f)                        //shared with the drum
                    };
                    body = new()
                    {
                        new(r - 0.10f, -1.82f, wobble: 0.9f),
                        new(r - 0.30f, -3.00f, wobble: 1f),                         //leaning in
                        new(r - 0.35f, -4.00f, wobble: 1f),
                        new(r - 0.2f,  -height, crease: true, wobble: 0.6f)         //the foot splays back out
                    };
                    break;

                case IslandShape.Pad:
                    //A landing pad cut into regolith (#538): a thin flat slab with a plain vertical edge, and
                    //round its foot the SPOIL the cut threw up, banked against the edge and spreading past the
                    //rim onto the ground - the one shape whose foot stands outside the coping's radius, which
                    //the foot rule (cover the terrain hole) allows: wider covers more, and the Moon's ground is
                    //at the foot's own height. The slab's edge is cut and barely wanders; the bank wanders like
                    //heaped ground.
                    wander = 0.35f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.30f, -0.06f),
                        new(r - 0.20f, -0.18f, crease: true, wobble: 0.05f),        //the slab's arris
                        new(r - 0.20f, -1.82f, crease: true, wobble: 0.05f)         //its cut edge, shared
                    };
                    body = new()
                    {
                        new(r - 0.20f, -1.82f, crease: true, wobble: 0.05f),
                        new(r - 0.20f, -2.60f, wobble: 0.1f),                       //the edge continues
                        new(r + 0.05f, -3.20f, wobble: 0.6f),                       //the spoil banks against it
                        new(r + 0.45f, -4.20f, wobble: 1f),
                        new(r + 0.75f, -height, crease: true, wobble: 0.8f)         //and spreads onto the ground
                    };
                    break;

                default:
                    //THE AUTHORED STONE, exactly as it was before #533. The stone, from the bore outward: the
                    //top face is a shallow dish rising from the bore's lip to its arris at the coping, then
                    //falls: a small chamfer off the face, a wash across the coping's head and a bullnose
                    //turning down into its vertical face. The three run smoothly into one another so the
                    //nose reads round; the arris where the walkable top ends is a crease, or the last span of
                    //the floor would shade as a curve and the top would look domed. (A lathe's two end
                    //points crease by construction — they have no span across the junction to be smoothed
                    //with — so only the creases in the middle of a run are marked.)
                    wander = IRREGULARITY;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 1.05f,       -0.12f),                 //chamfer off the face onto the coping
                        new(r - 0.34f,       -0.22f),                 //the coping's wash - a shallow fall to shed water
                        new(r - 0.12f,       -0.36f),                 //the bullnose begins to turn down
                        new(r,               -0.66f),                 //its outer arris: the platform's widest point
                        new(r,               -1.55f, crease: true),   //the coping's vertical face
                        new(r - WALL_INSET,  -1.82f)                  //the drip: the coping oversails the concrete
                    };

                    //The concrete, carrying on from the drip and back to the bore. The wall is battered
                    //slightly inward, flares out into a string course a third of the way down, and then
                    //splays into a plinth that carries the foot back out to very nearly the coping's radius.
                    //The wander fades in below the drip and out again at the underside, so the shared edge
                    //with the stone and the bore the drain has to meet are both left as true circles.
                    //
                    //The plinth flares rather than chamfering in, and that is load-bearing twice over. A base
                    //that widens is how a real one carries weight — one that tapers reads as balanced on a
                    //point — and the foot's radius is also what covers the hole the terrain scenes cut out
                    //of the ground under the platform (see the callers' TerrainHoleRadius). The island
                    //floats half a unit over that ground, so a foot drawn in much narrower than the platform
                    //opens a slot at every grazing angle and the sky shows through it as a pale ring: an
                    //earlier profile chamfered in to r - 1.15 and did exactly that.
                    body = new()
                    {
                        new(r - WALL_INSET,  -1.82f),                                  //shared with the cap's last point
                        new(r - 0.68f,       -2.05f,                   wobble: 0.35f),
                        new(r - 0.78f,       -3.2f,                    wobble: 1f),    //the wall, battered
                        new(r - 0.42f,       -3.62f, crease: true,     wobble: 1f),    //the string course flares out
                        new(r - 0.4f,        -4.2f,  crease: true,     wobble: 1f),    //its face
                        new(r - 0.66f,       -4.46f,                   wobble: 0.8f),  //its drip, cut back under
                        new(r - 0.44f,       -4.8f,                    wobble: 0.6f),  //the plinth splays out
                        new(r - 0.2f,        -height, crease: true,    wobble: 0.35f)  //its foot, all but flush with the coping
                    };
                    break;
            }

            body.AddRange(shaft);

            //Both lathes take the same amplitude and leave the phase at 0, so the point they share is
            //displaced identically on each - the seam rule in LatheMesh's own remarks. The authored stone's
            //cap has no wobbling ring at all, which is what kept its rim a true circle; a shape whose rim
            //wanders wobbles the cap's rings too, by the same per-ring figures the drum's first ring takes.
            capMesh = new LatheMesh(device, cap, segments, wander);
            bodyMesh = new LatheMesh(device, body, segments, wander);
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
