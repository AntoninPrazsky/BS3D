using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which environment the arena stands in. City is the default; Sea, Savanna, Desert, Mountain, Meadow,
    /// NeonCity, Forest and Outback swap the city (and only the city) for open water, a savanna, a Sahara of
    /// dunes, a snowy range, a flowering meadow, the same city lit up in neon, a forest clearing, or the
    /// red-rock Australian outback. Tropical swaps it for a beach — sand, palms and mossy rocks around the
    /// island, a turquoise lagoon beyond it, and the green far shore that closes the horizon. Volcano swaps it
    /// for the flank of an erupting cone: black basalt cut by rivers of lava, fountains over the crater and
    /// drifting ash — the first scene whose <b>ground is the light</b>. Mars swaps it for rust-red cratered
    /// ground under a dusty, horizon-bright sky of its own — the Moon's crater field ported and retextured,
    /// but kept an ordinary atmospheric backdrop rather than a second sky-replacing scene, because the real
    /// Mars (unlike the Moon) keeps a thin atmosphere. <see cref="Polar"/> swaps it for an icesheet (#222):
    /// a flat white expanse carved into sastrugi, a crevassed pressure ridge at distance, and a material that
    /// is white where it reflects the sky and cyan where the light goes through it.
    /// <para>
    /// <see cref="Space"/> is the one that is not like the others: it replaces the <b>sky</b> rather than the
    /// ground, so the island floats in deep space and there is no terrain, no horizon and no weather at all.
    /// The dream and the cavern followed it; the <see cref="Moon"/> is the first to take <b>both</b> halves at
    /// once — real cratered ground under a replaced, atmosphere-free sky (see <see cref="SceneCatalog.ReplacesSky"/>).
    /// </para>
    /// <para>
    /// <b>Every executable can reach every one of them, and that was not always true (#380).</b> The Testbed's
    /// NumPad2 and the map editor's V both walked a seven-long prefix — "the scenes a map is authored against" —
    /// and the other ten were reachable in the Testbed only with <c>scene=</c> and in the editor only by loading
    /// a level that named one. The premise died as the campaign was built: fifty of its hundred and ten levels
    /// are authored in space, the dream, the cavern, the Moon and the volcano, every one of them past that
    /// prefix, so the editor could not return such a level to its own backdrop without reloading the file. Both
    /// keys now walk the whole enum through <see cref="SceneCatalog.NextScene"/>, off the enum itself, so an
    /// eighteenth kind cannot be added and left unreachable.
    /// </para>
    /// <para>
    /// <b>New kinds are appended, never inserted.</b> Nothing persists the enum numerically — a level stores
    /// its backdrop as a scene name (<see cref="SceneCatalog.ParseKey"/>) — but the declared order is what the
    /// scene picker, <see cref="SceneCatalog.DisplayName"/>, the ambience bed and both cycling keys all index by.
    /// <b>A new kind is one row in <see cref="SceneCatalog"/>'s table</b>, and the catalog refuses to load
    /// without it (#580).
    /// </para>
    /// </summary>
    public enum SceneKind { City, Sea, Savanna, Desert, Mountain, Meadow, NeonCity, Forest, Space, Dream, Cavern, Moon, Outback, Tropical, Volcano, Mars, Storm, Polar, Aurora, Grid }

    /// <summary>
    /// One row of <see cref="SceneCatalog"/>: everything about a <see cref="SceneKind"/> that is a fact of the
    /// KIND rather than of anything drawn — what it is called, how a command line or a level file spells it,
    /// and the three classifications every host asks about it. No GPU type appears here, which is the point:
    /// the level format reads this table and must not need a renderer to do it (#580).
    /// </summary>
    public sealed class SceneInfo
    {
        /// <summary>The kind this row describes; row <c>i</c> of the table is kind <c>i</c>, checked at load.</summary>
        public SceneKind Kind { get; }

        /// <summary>The name for a menu or a log line. Display text, not a parse key.</summary>
        public string DisplayName { get; }

        /// <summary>The spelling a level file writes and every <c>scene=</c> switch takes, lowercase.</summary>
        public string ParseKey { get; }

        /// <summary>Other spellings <see cref="SceneCatalog.TryParse"/> takes for the same kind, lowercase.</summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>See <see cref="SceneCatalog.ReplacesSky"/>.</summary>
        public bool ReplacesSky { get; }

        /// <summary>See <see cref="SceneCatalog.IsSolidTerrainScene"/>.</summary>
        public bool SolidTerrain { get; }

        /// <summary>See <see cref="SceneCatalog.AnimatesLightRig"/>.</summary>
        public bool AnimatedRig { get; }

        /// <summary>A row. Flags left out are false; only the few kinds that are something say so.</summary>
        public SceneInfo(SceneKind kind, string displayName, string parseKey, string[] aliases = null,
            bool replacesSky = false, bool solidTerrain = false, bool animatedRig = false)
        {
            Kind = kind;
            DisplayName = displayName;
            ParseKey = parseKey;
            Aliases = aliases ?? Array.Empty<string>();
            ReplacesSky = replacesSky;
            SolidTerrain = solidTerrain;
            AnimatedRig = animatedRig;
        }
    }

    /// <summary>
    /// <b>The one table of facts about each <see cref="SceneKind"/></b> (#580): its display name, its parse key
    /// and aliases, and whether it replaces the sky, stands on solid terrain or animates its light rig.
    /// <para>
    /// It replaced five parallel tables keyed on the same enum, all of them on <see cref="SceneRenderer"/>: a
    /// positional name array that silently mislabelled every scene after a reordered member, a twenty-arm parse
    /// switch, a second copy of the one irregular spelling (<c>"neon"</c>) in the level file's converter, and
    /// three hand-kept membership lists. Each was right because somebody had remembered the others. Now a new
    /// kind is one row, and the type refuses to initialise — so every executable refuses to start — until
    /// every member of the enum has exactly one row at its own index and no two spellings collide
    /// (<see cref="Check"/>).
    /// </para>
    /// <para>
    /// It lives apart from the renderer, with no GPU type in it, so the level file format
    /// (<c>Prazsky.BS3D.Levels</c>) reads scene names without depending on the class that draws them.
    /// <see cref="SceneRenderer"/>'s old static members (<c>SceneName</c>, <c>TryParseScene</c>,
    /// <c>ReplacesSky</c>, …) forward here, so no caller had to change.
    /// </para>
    /// </summary>
    public static class SceneCatalog
    {
        //In the declared order of SceneKind. "Mountains" reads better than the singular enum member and is
        //deliberately not "corrected" to match it; the parse keys are the singular ones, because those are what
        //a command line already takes. "mountain" and "neon" rather than "mountains" and "neoncity": they are
        //the names that already existed when one parse replaced the Testbed's if/else chain and the Game's
        //switch (#75).
        private static readonly SceneInfo[] ROWS =
        {
            new(SceneKind.City, "City", "city"),
            new(SceneKind.Sea, "Sea", "sea"),
            new(SceneKind.Savanna, "Savanna", "savanna", solidTerrain: true),
            new(SceneKind.Desert, "Desert", "desert", solidTerrain: true),
            new(SceneKind.Mountain, "Mountains", "mountain", solidTerrain: true),
            new(SceneKind.Meadow, "Meadow", "meadow", solidTerrain: true),
            new(SceneKind.NeonCity, "Neon City", "neon"),
            new(SceneKind.Forest, "Forest", "forest", solidTerrain: true),
            new(SceneKind.Space, "Space", "space", replacesSky: true),
            new(SceneKind.Dream, "Dream", "dream", replacesSky: true),
            new(SceneKind.Cavern, "Cavern", "cavern", replacesSky: true),
            new(SceneKind.Moon, "Moon", "moon", replacesSky: true, solidTerrain: true),
            new(SceneKind.Outback, "Outback", "outback", solidTerrain: true),
            new(SceneKind.Tropical, "Tropical", "tropical", solidTerrain: true),
            new(SceneKind.Volcano, "Volcano", "volcano", solidTerrain: true),
            new(SceneKind.Mars, "Mars", "mars", solidTerrain: true),
            //⚠ The STORM is deliberately not solid terrain, and it was for one build. It was classified as
            //terrain because it was DRAWN as terrain — a displaced grid at the island's foot — and the owner
            //rejected exactly that: the scene has no ground in it. With the deck gone there is nothing to cut the
            //island's footprint out of and nothing an opaque pit shaft would be standing in, so the drain looks
            //straight through onto sky and cloud the way it does over the space scene.
            new(SceneKind.Storm, "Storm", "storm"),
            //"ice" as well as "polar": the scene is named for where it is and remembered for what it is made of,
            //and a spelling refused in silence is a run that quietly plays in the city.
            new(SceneKind.Polar, "Polar", "polar", aliases: new[] { "ice" }, solidTerrain: true),
            new(SceneKind.Aurora, "Aurora", "aurora", replacesSky: true, solidTerrain: true, animatedRig: true),
            new(SceneKind.Grid, "Grid", "grid", aliases: new[] { "tron" }, replacesSky: true, solidTerrain: true),
        };

        //Every parse key and alias, built once by Check. Case-insensitive: the switch it replaced lowercased its input.
        private static readonly Dictionary<string, SceneKind> PARSE = Check(ROWS);

        /// <summary>
        /// Refuses a table that is not exactly one row per <see cref="SceneKind"/> member at the member's own
        /// index, or whose spellings collide, and builds the parse map off it. Run once, at type
        /// initialisation, over the shipped table — and by the tests over deliberately broken ones, which is
        /// what shows the refusal fires (BestPractices.md §10).
        /// </summary>
        /// <exception cref="InvalidOperationException">The table is inconsistent; the message says how.</exception>
        internal static Dictionary<string, SceneKind> Check(IReadOnlyList<SceneInfo> rows)
        {
            SceneKind[] members = Enum.GetValues<SceneKind>();

            if (rows.Count != members.Length)
                throw new InvalidOperationException($"SceneCatalog has {rows.Count} rows for {members.Length} SceneKind members; every member needs exactly one.");

            Dictionary<string, SceneKind> parse = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows.Count; i++)
            {
                SceneInfo row = rows[i];

                if (row == null || (int)row.Kind != i || members[i] != row.Kind)
                    throw new InvalidOperationException($"SceneCatalog row {i} is {row?.Kind.ToString() ?? "null"}, not {members[i]}: rows are indexed by the enum's value and must be in its declared order.");

                if (string.IsNullOrWhiteSpace(row.DisplayName))
                    throw new InvalidOperationException($"SceneCatalog row {row.Kind} has no display name.");

                Add(row.ParseKey, row.Kind);
                foreach (string alias in row.Aliases) Add(alias, row.Kind);
            }

            return parse;

            void Add(string key, SceneKind kind)
            {
                if (string.IsNullOrWhiteSpace(key) || key != key.ToLowerInvariant())
                    throw new InvalidOperationException($"SceneCatalog row {kind} has an empty or non-lowercase spelling '{key}'.");

                if (!parse.TryAdd(key, kind))
                    throw new InvalidOperationException($"SceneCatalog spelling '{key}' names both {parse[key]} and {kind}.");
            }
        }

        /// <summary>Every row, in the enum's declared order.</summary>
        public static IReadOnlyList<SceneInfo> All => ROWS;

        /// <summary>The row of <paramref name="kind"/>.</summary>
        public static SceneInfo Get(SceneKind kind) => ROWS[(int)kind];

        /// <summary>How many <see cref="SceneKind"/>s there are; a scene picker and a random pick size off it.</summary>
        public static int Count => ROWS.Length;

        /// <summary>
        /// The scene's name for a menu or a log line. Display text, not a parse key — see
        /// <see cref="TryParse"/> for the spellings a command line takes.
        /// </summary>
        public static string DisplayName(SceneKind kind) => ROWS[(int)kind].DisplayName;

        /// <summary>The spelling a level file writes for <paramref name="kind"/> and <see cref="TryParse"/> reads back.</summary>
        public static string ParseKey(SceneKind kind) => ROWS[(int)kind].ParseKey;

        /// <summary>
        /// Parses the names every executable's <c>scene=</c> switch takes and a level file stores, so one
        /// benchmark or screenshot script drives any of them unchanged — the Testbed grew an if/else chain, the
        /// Game a switch and the two had to be kept in step by hand until #75. Case-insensitive; a null or an
        /// unknown name is false.
        /// </summary>
        public static bool TryParse(string name, out SceneKind kind)
        {
            if (name != null && PARSE.TryGetValue(name, out kind)) return true;

            kind = default;
            return false;
        }

        /// <summary>
        /// The next scene in the enum, wrapping — what a cycling key in an authoring tool wants. It replaced a
        /// <c>CycleLength</c> constant of 7 that both cycling keys took their modulus from (#380): a prefix is
        /// a count, and a count written next to an enum is a thing that ages every time the enum grows. Nothing
        /// here counts the scenes, so a twenty-first kind is reachable in both programs the moment it is
        /// declared — the same argument <c>BallStyles.Next</c> already makes for the ball materials, in the
        /// program that exists to choose between them.
        /// <para>
        /// Off <see cref="Enum.GetValues{TEnum}"/> rather than <see cref="Count"/>, because the question is
        /// "what members does this enum have" and not "how long is the table". They agree by construction —
        /// <see cref="Check"/> refuses a table where they do not.
        /// </para>
        /// </summary>
        public static SceneKind NextScene(SceneKind kind)
        {
            SceneKind[] all = Enum.GetValues<SceneKind>();

            return all[(Array.IndexOf(all, kind) + 1) % all.Length];
        }

        /// <summary>
        /// True for the scenes that replace the SKY rather than the ground — space, the dream, the cavern,
        /// the Moon, the aurora and the Grid. The caller draws no dome and no cloud deck in these, suppresses
        /// the cloud shadow on the instanced effect, clears to black (the pass covers every pixel; black is
        /// what would show if it ever did not), and takes the scene's own light rig through
        /// <see cref="SceneRenderer.TryGetLightRig"/>.
        /// <para>
        /// <b>The Moon (#125) was the first scene in this set AND in <see cref="IsSolidTerrainScene"/>; the
        /// aurora (#205) was the second, the Grid (#393) is the third.</b> The two families were exact
        /// complements of what they draw — a dome over ground, or a backdrop with no ground — until the Moon
        /// wanted real cratered ground under a black, starlit, domeless sky. Every question this flag answers
        /// (dome, clouds, clear colour, light rig) each of them answers the sky-replacing way, and every
        /// question <see cref="IsSolidTerrainScene"/> answers (the terrain hole, the pit shaft,
        /// <see cref="OpenBelow"/>) each answers the terrain way; no caller asks either flag anything the
        /// other one owns, which is what makes holding both memberships sound — three times over now.
        /// </para>
        /// </summary>
        public static bool ReplacesSky(SceneKind kind) => ROWS[(int)kind].ReplacesSky;

        /// <summary>
        /// True for the solid-ground backdrops — mountains, meadow, savanna, desert, forest, outback, the
        /// tropical beach, the volcano, Mars, the icesheet, the Moon, the aurora and the Grid — whose terrain is a
        /// flat clearing at the island's foot with the island's footprint cut out of it
        /// (<see cref="SceneRenderer.TerrainHoleRadius"/>), and which therefore need the dark pit shaft drawn
        /// behind the drain's glass: a hole alone lets the ~55 %-opaque glass show what is behind it straight
        /// through and the drain reads as a glass ring lying on the ground. The sea fills the drain with water,
        /// the two cities have their own canyon falling away below the island, and space, the dream, the cavern
        /// and the storm have nothing down there to hide a ball against — none of them needs it.
        /// <para>
        /// The Moon, the aurora and the Grid are here <b>and</b> in <see cref="ReplacesSky"/> — the first
        /// three scenes in both families (the note there says why that is sound). Each needs the shaft for
        /// the terrain reason with the sky-replacing twist: without it the drain's glass would show the
        /// <i>void</i> through a hole in the ground, which reads as a glass ring over open sky (a starfield
        /// for the Moon and the aurora, the Grid's own near-black nothing for the Grid). The tropical beach is
        /// the first scene with water <i>and</i> this membership — its water starts past the beach, well
        /// outside the hole, so under the island there is sand and the shaft answers for it exactly as it does
        /// for the meadow.
        /// </para>
        /// <para>
        /// It existed as a private copy in the Testbed and the Game until #75, and the forest was once missing
        /// from <b>both</b> — which is the exact failure a duplicated classification invites, and the reason
        /// this and every other question about a <see cref="SceneKind"/> are answered in one place.
        /// </para>
        /// </summary>
        public static bool IsSolidTerrainScene(SceneKind kind) => ROWS[(int)kind].SolidTerrain;

        /// <summary>
        /// Whether there is a vantage <b>under</b> the island from which the balls pouring out of the drain can
        /// still be seen — what the drop cinematic asks before it decides whether to dive beneath the stone or
        /// stay above it and look down the drain's throat.
        /// <para>
        /// It is exactly the complement of <see cref="IsSolidTerrainScene"/>, and that is a consequence rather
        /// than a coincidence: the pit shaft those scenes need is opaque and near-black, so the very thing that
        /// makes the drain read from above is what closes the view from below. Defined as the negation so a
        /// twelfth scene is one decision instead of two silently disagreeing lists — it was two hand-kept sets
        /// in different files until #75. Split them again only if a scene ever wants the shaft and the dive
        /// both, and say why on the spot.
        /// </para>
        /// </summary>
        public static bool OpenBelow(SceneKind kind) => !IsSolidTerrainScene(kind);

        /// <summary>
        /// True for a scene whose own light rig moves with time (<see cref="SceneRenderer.TryGetLightRig"/>'s
        /// wall-clock argument), so a host has to step it (<see cref="SkyLightRig.StepSceneLight"/>) rather than
        /// derive it once per scene switch. Only the aurora's, since #462.
        /// </summary>
        public static bool AnimatesLightRig(SceneKind kind) => ROWS[(int)kind].AnimatedRig;
    }
}
