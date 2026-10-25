using Microsoft.Xna.Framework;
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
        Pad,

        /// <summary>A melting solid (#779): a soft rolled rim whose lower half pours down in drips over a lobed,
        /// sagging drum, the underside hanging a little at every drip (the dream).</summary>
        Melt
    }

    /// <summary>
    /// The round platform the game is played on: a cast-concrete drum with a dressed stone top, a moulded
    /// coping around its rim and a drain bored through the middle. It replaces the plain extruded washer
    /// the arena used to be — a cylinder with a hole, whose every edge was a raw 90° cut. Since #533 that is
    /// one of the <see cref="IslandShape"/> silhouettes; the remarks below describe the authored stone, and
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
        /// <param name="reduced">The Potato path's cut (#779): the melting shape at <paramref name="segments"/> facets and
        /// without its drips, which are four times the facets and a dense run of rings for a detail the Raspberry Pi's
        /// island, with no marble and no relief, has nothing to show off. The other shapes ignore it.</param>
        public IslandMesh(GraphicsDevice graphicsDevice, float boreRadius, float outerRadius, float height, int segments,
            float dishDepth, IslandShape shape = IslandShape.Stone, bool reduced = false)
        {
            Shape = shape;
            Build(graphicsDevice, boreRadius, outerRadius, height, segments, dishDepth, shape, reduced, out LatheMesh cap, out LatheMesh body);
            Cap = cap;
            Body = body;
        }

        private static void Build(GraphicsDevice device, float bore, float r, float height, int segments, float dish,
            IslandShape shape, bool reduced, out LatheMesh capMesh, out LatheMesh bodyMesh)
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
            int capSegments = segments;
            Func<Vector3, float> capHeightField = null;
            Func<float, float, float> radialField = null;

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
                    //
                    //And CRATERS, as geometry: the dish between the bore's lip and the arris is lathed in dense
                    //rings rather than one span, and a height field (PadCraters) cuts real bowls with raised
                    //rims into it, so a crater is a hole with its own shading and silhouette - the owner's
                    //ruling of 2026-09-24, after a first cut drew them in the shader's height field and they
                    //smeared over the slab's edge like a decal ("a crater is never bent; it is a hole"). The
                    //cap runs at four times the facets so the bowls are round; the drum keeps SEGMENTS, and the
                    //ring the two share wobbles by 0.05 of the wander, so its chords differ by nothing visible.
                    wander = 0.35f;
                    cap = new() { lip };
                    for (float ring = bore + PAD_RING_STEP; ring < r - COPING_WIDTH - PAD_RING_STEP * 0.5f; ring += PAD_RING_STEP)
                        cap.Add(new(ring, -dish * (1f - (ring - bore) / (r - COPING_WIDTH - bore))));
                    cap.Add(arris);
                    cap.AddRange(new LathePoint[]
                    {
                        new(r - 0.30f, -0.06f),
                        new(r - 0.20f, -0.18f, crease: true, wobble: 0.05f),        //the slab's arris
                        new(r - 0.20f, -1.82f, crease: true, wobble: 0.05f)         //its cut edge, shared
                    });
                    body = new()
                    {
                        new(r - 0.20f, -1.82f, crease: true, wobble: 0.05f),
                        new(r - 0.20f, -2.60f, wobble: 0.1f),                       //the edge continues
                        new(r + 0.05f, -3.20f, wobble: 0.6f),                       //the spoil banks against it
                        new(r + 0.45f, -4.20f, wobble: 1f),
                        new(r + 0.75f, -height, crease: true, wobble: 0.8f)         //and spreads onto the ground
                    };
                    capSegments = segments * 4;
                    capHeightField = PadCraters(bore, r - COPING_WIDTH);
                    break;

                case IslandShape.Melt:
                    //A MELTING SOLID (#779), the dream's own vocabulary turned on the island: every reference
                    //(779-zimage, 779-klein) draws the platform as a soft pearl disc whose rolled rim pours down in
                    //glossy drips, over an underside that melts like the scene's tumbling solids. So the rim is a
                    //puffy roll (no arris below the floor's), the drum a soft wall swelling a little under it, and
                    //the whole edge wanders like the floe. The drips are a radial field (MeltDrips): each one is the
                    //roll carried on straight down the wall, proud of it, to a bulb and a round end. Their run is cut
                    //in rings MELT_RING_STEP apart and four times the stone's facets, or a drip a unit wide and a
                    //few long would be one facet and one span. The one foot that rounds under well inside the rim: the
                    //dream floats, with no terrain hole for a foot to cover.
                    wander = 0.45f;
                    cap = new()
                    {
                        lip, arris,
                        new(r - 0.75f, -0.05f, wobble: 0.2f),
                        new(r - 0.25f, -0.32f, wobble: 0.5f)
                    };
                    LathePoint[] run = MeltRun(r);
                    List<LathePoint> dense = reduced ? new List<LathePoint>(run) : Subdivided(run, MELT_RING_STEP);
                    int turn = dense.FindIndex(point => point.Y <= -1.70f + 1e-4f);
                    cap.AddRange(dense.GetRange(0, turn + 1));
                    body = dense.GetRange(turn, dense.Count - turn);
                    body.AddRange(new LathePoint[]
                    {
                        new(r - 1.90f, -4.95f, wobble: 0.8f),                        //rounding under
                        new(r - 3.00f, -5.15f, wobble: 0.6f),                        //the underside, sagging a little
                        new(r - 6.00f, -5.20f, wobble: 0.4f),
                        new(bore + 2f, -5.05f, wobble: 0.2f)
                    });
                    if (!reduced)
                    {
                        capSegments = segments * 4;
                        radialField = MeltDrips(run);
                    }
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
            capMesh = new LatheMesh(device, cap, capSegments, wander, heightField: capHeightField, radialField: radialField);
            //The body takes the cap's facet count: a shape whose cap is cut finer (the lunar pad's craters) shares its edge rings with
            //the body, and rings of 512 and 128 facets meet in a row of T-junctions a rasteriser rounds into hairline cracks (#974)
            //The two take the same radial field where they take one at all (the melting rim's drips), so the ring they share is
            //pushed identically on each, as it is displaced identically by the wander
            bodyMesh = new LatheMesh(device, body, capSegments, wander, radialField: radialField);
        }

        //The lunar pad's dish is lathed in rings this far apart (world units), so a crater a unit across is
        //cut from several rings and a few facets rather than from one quad.
        private const float PAD_RING_STEP = 0.22f;

        /// <summary>
        /// The craters of the lunar pad (#538), as a height field for <see cref="LatheMesh"/>: a fixed
        /// scatter of bowls between the bore's lip and the floor's arris, many small and a few big (the
        /// radius is drawn as a power of a uniform), none overlapping, none reaching either ring — placed
        /// entirely inside them and faded to nothing over the last half unit besides, so the lip the drain's
        /// bead closes on and the arris the physics floor ends at stay exact circles. Each is a parabolic bowl
        /// with a raised rim on a Gaussian astride its edge: the shape a small impact leaves. The depth is
        /// capped at 0.20 of a unit, so a ball rolling over the biggest floats by well under half its radius
        /// for the frames it takes to cross — the physics floor is the plain dish underneath.
        /// </summary>
        private static Func<Vector3, float> PadCraters(float bore, float floorRadius)
        {
            var rng = new Random(538);
            var craters = new List<(float X, float Z, float Radius, float Depth)>();
            float inner = bore + 1.2f, outer = floorRadius - 1.0f;

            for (int attempt = 0; attempt < 600 && craters.Count < 16; attempt++)
            {
                float radius = 0.45f + 1.9f * MathF.Pow((float)rng.NextDouble(), 2.2f);
                float span = outer - inner - 2f * radius;
                if (span <= 0f) continue;

                float ring = inner + radius + (float)rng.NextDouble() * span;
                float angle = (float)rng.NextDouble() * MathHelper.TwoPi;
                float x = MathF.Cos(angle) * ring, z = MathF.Sin(angle) * ring;

                bool clear = true;
                foreach (var c in craters)
                {
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(c.X, c.Z)) < (radius + c.Radius) * 1.3f) { clear = false; break; }
                }
                if (!clear) continue;

                craters.Add((x, z, radius, MathF.Min(0.20f, 0.11f * radius)));
            }

            return position =>
            {
                float lift = 0f;
                var xz = new Vector2(position.X, position.Z);

                foreach (var c in craters)
                {
                    float d = Vector2.Distance(xz, new Vector2(c.X, c.Z)) / c.Radius;
                    if (d > 1.7f) continue;

                    float bowl = d < 1f ? -c.Depth * (1f - d * d * d) : 0f;   //steeper at the wall than a parabola, flatter in the floor
                    float rimOffset = (d - 1f) / 0.22f;
                    float rim = 0.32f * c.Depth * MathF.Exp(-rimOffset * rimOffset);
                    lift += bowl + rim;
                }

                //Faded to nothing at the two rings that must stay exact
                float r = xz.Length();
                float fade = Math.Clamp((r - bore - 0.3f) / 0.6f, 0f, 1f) * Math.Clamp((floorRadius - 0.3f - r) / 0.6f, 0f, 1f);
                return lift * fade;
            };
        }

        //The melting rim's drips (#779): how many, the longest one's run down the side from the roll's equator, how wide one
        //is across its middle and how far its bulb swells back out from the tongue's run (world units). Fat drops, as the pearl
        //references hang them, not long runs: a long drip barely narrowing read as a bar stood on the wall. The longest stops
        //1.2 above the drum's turn under (y -4.4), so a drip never runs round under the platform, where the drop cinematic's
        //lens is kept out (DropCinematic's STONE_CLEARANCE).
        private const int MELT_DRIPS = 30;
        private const float MELT_DRIP_LONGEST = 2.4f;
        private const float MELT_DRIP_HALF_WIDTH = 0.6f;
        private const float MELT_DRIP_PROUD = 0.35f;

        //How far apart the rings of the drips' run are cut (world units along the profile)
        private const float MELT_RING_STEP = 0.16f;

        /// <summary>
        /// The melting rim's outer profile from the roll's equator (its widest) down the side (#779): what the drips run
        /// over, single-valued in height. The cap ends where it turns under (y -1.70) and the drum carries on from there.
        /// </summary>
        internal static LathePoint[] MeltRun(float outerRadius) => new LathePoint[]
        {
            new(outerRadius - 0.02f, -0.80f, wobble: 0.8f),                         //the roll's widest
            new(outerRadius - 0.12f, -1.30f, wobble: 1f),
            new(outerRadius - 0.55f, -1.70f, wobble: 1f),                           //turning under: the cap ends here
            new(outerRadius - 0.95f, -1.95f, wobble: 1f),                           //the hollow under the roll
            new(outerRadius - 1.05f, -2.60f, wobble: 1f),                           //the wall
            new(outerRadius - 0.90f, -3.50f, wobble: 1f),                           //swelling a little
            new(outerRadius - 1.15f, -4.40f, wobble: 1f)
        };

        /// <summary>
        /// A profile with every span longer than <paramref name="step"/> cut into equal pieces, the radius, the height and
        /// the wobble interpolated along it: the same polyline in more rings, so a field can shape it finely. The new points
        /// are never creases.
        /// </summary>
        private static List<LathePoint> Subdivided(IReadOnlyList<LathePoint> profile, float step)
        {
            var dense = new List<LathePoint> { profile[0] };
            for (int k = 1; k < profile.Count; k++)
            {
                LathePoint from = profile[k - 1], to = profile[k];
                float length = new Vector2(to.Radius - from.Radius, to.Y - from.Y).Length();
                int pieces = Math.Max(1, (int)MathF.Ceiling(length / step));
                for (int i = 1; i < pieces; i++)
                {
                    float t = (float)i / pieces;
                    dense.Add(new LathePoint(MathHelper.Lerp(from.Radius, to.Radius, t), MathHelper.Lerp(from.Y, to.Y, t),
                        wobble: MathHelper.Lerp(from.Wobble, to.Wobble, t)));
                }
                dense.Add(to);
            }
            return dense;
        }

        /// <summary>
        /// The drips of the melting rim (#779), as a radial field for both lathes: a fixed scatter of drips round the rim,
        /// each the roll carried on straight down the side from its equator - a tongue whose face runs down at the roll's
        /// own radius less a slow taper, so it stands proud of the hollow and the wall under it - swelling to a bulb near
        /// its end and closing there in a round tip. Across, each is a rounded ridge. Nothing above the equator moves (the
        /// floor's arris, the lip and the roll's crown stay exact), and nothing under <paramref name="run"/>'s last point.
        /// <para>
        /// <paramref name="run"/> is the outer profile the drips run over, from the equator down, single-valued in height,
        /// so the push at a height is the drip's face less the profile's own radius there.
        /// </para>
        /// </summary>
        internal static Func<float, float, float> MeltDrips(IReadOnlyList<LathePoint> run)
        {
            float top = run[0].Y, bottom = run[run.Count - 1].Y, face = run[0].Radius - 0.12f;
            var rng = new Random(779);
            var drips = new (float Angle, float HalfWidth, float Length)[MELT_DRIPS];
            for (int i = 0; i < MELT_DRIPS; i++)
            {
                //Evenly spaced with a jitter, so they never bunch or leave a long bare stretch; mostly short with a few long
                //ones (a power of a uniform), the widths varying a little
                float angle = MathHelper.TwoPi * (i + 0.15f + 0.7f * (float)rng.NextDouble()) / MELT_DRIPS;
                float length = MELT_DRIP_LONGEST * (0.45f + 0.55f * MathF.Pow((float)rng.NextDouble(), 1.4f));
                float halfWidth = MELT_DRIP_HALF_WIDTH * (0.75f + 0.5f * (float)rng.NextDouble());
                drips[i] = (angle, halfWidth / face, length);
            }

            return (angle, y) =>
            {
                if (y >= top || y < bottom) return 0f;

                //The profile's own radius at this height, off the run (straight spans between its points)
                float profile = run[run.Count - 1].Radius;
                for (int k = 1; k < run.Count; k++)
                {
                    if (y >= run[k].Y)
                    {
                        float t = (run[k - 1].Y - y) / (run[k - 1].Y - run[k].Y);
                        profile = MathHelper.Lerp(run[k - 1].Radius, run[k].Radius, t);
                        break;
                    }
                }

                float push = 0f;
                foreach (var drip in drips)
                {
                    float along = (top - y) / drip.Length;   //0 at the equator, 1 at the tip
                    if (along >= 1f) continue;

                    //The bulb swells over the last third, and the tip closes on a quarter circle
                    float bulb = MathF.Max(0f, 1f - MathF.Abs(along - 0.78f) / 0.25f);
                    float tip = along > 0.88f ? MathF.Sqrt(MathF.Max(0f, 1f - (along - 0.88f) * (along - 0.88f) / (0.12f * 0.12f))) : 1f;
                    //Wide where it leaves the roll, narrowing as it runs, swelling again at the bulb
                    float halfWidth = drip.HalfWidth * (1.3f - 0.8f * along + 0.55f * bulb) * tip;
                    if (halfWidth <= 0f) continue;

                    float across = MathF.IEEERemainder(angle - drip.Angle, MathHelper.TwoPi) / halfWidth;
                    if (across * across >= 1f) continue;

                    //The tongue's face: the roll's own carried straight down, so the drip leaves the roll with no ledge, tapering
                    //in as it runs, and the bulb swelling back out to the roll's face - never past the roll's widest, which is
                    //the platform's (MeltIslandTests)
                    float tongue = face - 0.4f * along + MELT_DRIP_PROUD * bulb;
                    float shape = 1f - across * across;
                    //A rounded ridge across, falling to the wall in a soft curve (a semicircle stood on it with vertical
                    //sides and read as a bar)
                    push = MathF.Max(push, MathF.Max(0f, tongue - profile) * shape * shape * (3f - 2f * shape) * tip);
                }
                return push;
            };
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
