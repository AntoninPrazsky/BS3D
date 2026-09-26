using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using System;
using System.IO;
using System.Linq;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// The command line: <see cref="Main"/>, which refuses an unknown flag, writes the campaign in play order and
    /// runs the gates, and the runners behind the flags that ask a question of level files instead of
    /// writing any (<c>--sagfile=</c>, <c>--clearfile=</c>, <c>--arrival</c>) or of the written set
    /// (<c>--sag</c>, <see cref="RunSagGate"/>). Part of <see cref="Program"/> because the play order is a list
    /// of its designs. Split out of <c>Program.cs</c> in #597.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The flags <see cref="Main"/> reads, exactly and by prefix — the one list the refusal below checks.</summary>
        private static readonly string[] Flags = { "--sag", "--clear", "--arrival" };
        private static readonly string[] ValuedFlags = { "--sag=", "--sagfile=", "--clearfile=", "--arrivalfile=" };

        private static int Main(string[] args)
        {
            //REFUSED BEFORE ANYTHING IS WRITTEN (#574). This is the one tool that writes into the tracked tree,
            //and every flag below is read by looking for itself, so a typo was simply not found: "--sagg" or
            //"--clearfiles=x" ran a full regeneration of every level instead of the probe asked for, and "-sag"
            //- one dash - became the output directory. Exit 2, the usage error, so a script can tell it apart
            //from a gate's refusal (1).
            string[] plain = args.Where(a => !a.StartsWith("-", StringComparison.Ordinal)).ToArray();
            string[] unknown = args.Where(a => a.StartsWith("-", StringComparison.Ordinal)
                && !Flags.Contains(a, StringComparer.Ordinal)
                && !ValuedFlags.Any(f => a.StartsWith(f, StringComparison.Ordinal))).ToArray();

            if (unknown.Length > 0 || plain.Length > 1)
            {
                foreach (string a in unknown) Console.WriteLine($"Unknown option '{a}'.");
                if (plain.Length > 1) Console.WriteLine($"More than one output directory: {string.Join(", ", plain)}.");
                Console.WriteLine("Usage: LevelGen [<output dir>] [--sag[=<name,...>]] [--sagfile=<file,...>] [--clear]"
                    + " [--clearfile=<file,...>] [--arrival] [--arrivalfile=<file,...>]");
                return 2;
            }

            //The output directory is still the first PLAIN argument, exactly as it was; the flags are named so
            //a path can never be mistaken for one. See RunSagGate for what --sag costs and why it is opt-in.
            string dirArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            bool sag = args.Any(a => a == "--sag" || a.StartsWith("--sag=", StringComparison.Ordinal));
            string[] sagOnly = args
                .Where(a => a.StartsWith("--sag=", StringComparison.Ordinal))
                .SelectMany(a => a["--sag=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            //⚠ A LEVEL FILE THE SET HAS NEVER HEARD OF (#333), and it generates nothing at all: it hangs the
            //files named and exits. The set is the campaign, so a level built to try a mechanic out is not in
            //it — and until this existed the only way to put such a level in front of the probe was to edit
            //the campaign, which the next run of this tool overwrites. Same shape as the Game's `levelfile=`,
            //and for the same reason.
            string[] sagFiles = args
                .Where(a => a.StartsWith("--sagfile=", StringComparison.Ordinal))
                .SelectMany(a => a["--sagfile=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            if (sagFiles.Length > 0) return RunSagFiles(sagFiles) ? 0 : 1;

            //THE SHORTEST CLEAR (#458). The gate itself runs on every invocation and costs milliseconds - see
            //ClearProbe for why the floor answers most of the pack before a move is played. `--clear` adds the
            //beam, which is the only way to get a number out of a level the gate stops searching, and
            //`--clearfile=` asks it of a level file the set has never heard of, exactly as `--sagfile=` does.
            LevelGates.DeepClear = args.Any(a => a == "--clear");

            string[] clearFiles = args
                .Where(a => a.StartsWith("--clearfile=", StringComparison.Ordinal))
                .SelectMany(a => a["--clearfile=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            if (clearFiles.Length > 0) return RunClearFiles(clearFiles) ? 0 : 1;

            //SHOT ARRIVAL (#514). Asks, of the level files named, which landings a straight shot can actually
            //get to and how far round the orbit the gun has to walk to get to them. Opt-in and file-scoped for
            //now: it is a measurement of the shipped pack before it is a gate, for the reason RunArrivalFiles
            //states — a check whose job is to refuse a design has to be shown not to refuse the designs that
            //already play.
            string[] arrivalFiles = args
                .Where(a => a.StartsWith("--arrivalfile=", StringComparison.Ordinal))
                .SelectMany(a => a["--arrivalfile=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            //`--arrival` on its own is the whole shipped pack, which is what anyone actually wants to see and
            //what a 120-path command line was standing in the way of. `--arrivalfile=` stays for a level the
            //set has never heard of, exactly as `--sagfile=` and `--clearfile=` do.
            if (arrivalFiles.Length == 0 && args.Any(a => a == "--arrival"))
            {
                try
                {
                    arrivalFiles = Directory.GetFiles(FindLevelsDirectory(), "*.json")
                        .Where(p => !string.Equals(Path.GetFileName(p), "Levels.json", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                catch (DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    return 1;
                }
            }

            if (arrivalFiles.Length > 0) return RunArrivalFiles(arrivalFiles) ? 0 : 1;

            try
            {
                LevelEmitter.OutDir = dirArg ?? FindLevelsDirectory();
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }

            Console.WriteLine($"Writing to {LevelEmitter.OutDir}");

            //IN PLAY ORDER, AND IN BLOCKS (#194). A block is one scene, one dome, one music theme and one
            //statable style, and it is contiguous — a chunk of the BLOCKS table by position IS a block, which
            //is what #184's block-completion celebration needs and what a flat list could not give it. The
            //chunks were all ten levels, then #295's Eruption shipped five and #369 filled it back to ten;
            //the table carries each block's size either way.
            //
            //The campaign's light drains out of it as it goes: green noon, gold afternoon, the desert's cool
            //late light, violet dusk, underground dark, airless black — and since #182, past the black, deep
            //space. Difficulty ramps with it, and so does how much of each block is new — one new level in
            //the first block, then two, five, three, four, one at the Quarry, and five again in the Nebula,
            //which closes on the one level that plays every colour the game has.
            //
            //THE COIL IS INSERTED AT 3 RATHER THAN APPENDED (#207), and both halves of that are decisions.
            //Appending it would have put a bright hot chapter after the airless black one and taken the last
            //word off Colossus, which is the level the whole ramp is built to arrive at; slotted third, the
            //desert is the step the light was missing between the savanna's gold and the mountains' violet.
            //Nothing had to be retuned to move eleven levels down the order, because the unlock ramp
            //is a function of POSITION and not of any design — see MinStarsAt, which was written that way for
            //exactly this.
            //
            //THE NEBULA IS APPENDED, and that reverses the second half of #207's reasoning deliberately (the
            //owner's ask, #182): the campaign's last word moves off Colossus onto the Nebula's finale. The
            //first half survives intact, because the block is not a bright chapter after the airless black
            //one — space is the step PAST airless black, the ramp continuing outward rather than turning
            //back. Colossus keeps every rule it had and closes the Quarry; what it hands over is only the
            //campaign-complete moment. The Nebula's designs live in their own array below rather than in this
            //one, because WriteLevelSet appends Colossus after everything in THIS array — five designs added
            //here would land Colossus at the tail of the NEBULA's five and file Comet under the Quarry. The
            //blocks would still be contiguous (names fall out of positions, nothing refuses the set); they
            //would celebrate the wrong levels.
            //
            //ONE RECORDED DECISION IS REVERSED HERE and it is worth naming rather than leaving to be noticed.
            //The three pictures used to be deliberately INTERLEAVED with the geometric levels, "because they
            //are all gentle by design, and three easy levels back to back is a lull rather than a ramp". They
            //are a block now. What changed is the thing that reasoning rested on: the campaign was one flat
            //ramp of fourteen, where a run of easy levels is simply a flat stretch of it. A block is a chapter
            //with its own scene, sky and music, and a chapter of pictures is a change of register rather than a
            //stall — provided the block itself ramps, which is what Elephant and Zebra are for: their symbols are
            //drawn in three inks and two, so neither has the single big payoff that makes the shipped three
            //gentle, and the block's best single shot falls 40 %, 21 %, 9 % across its last three levels. The lull
            //was real; five gentle levels would still be one.
            Design[] designs =
            {
                //1. THE MEADOW - "Rings". Solids of revolution in concentric shells or angular sectors: every
                //colour is a plate of dozens, so one matching ball takes a whole shell. The block that teaches
                //what a colour group is, in the cheapest scene in the game under the one clear blue dome.
                One(), Bullseye(), Toadstool(), Pinwheel(), Diabolo(), Shuttle(), Amphora(), Saturn(), Fountain(), Gem(),

                //2. THE SAVANNA - "The Gallery". Flat drawn walls, read off a bitmap written in the source.
                Heart(), Smiley(), Star(), Elephant(), Moon(), Paw(), Meerkat(), Giraffe(), Balloon(), Zebra(),

                //3. THE DESERT - "The Coil" (#207). Every layout here hangs on SLENDER LINKS, so the cluster
                //springs and swings instead of sitting there: strands twisted round each other, a ledge winding
                //round a thin core, a woven shell, a weight on four ropes, a closed loop. The one block whose
                //style is a statement about the PHYSICS rather than about the silhouette — and the block that
                //finally plays in the desert, which no level did.
                Rope(), Minaret(), Basket(), Pendulum(), Pendant(), Web(), Crane(), Mobile(), Bridge(), Knot(),

                //4. THE MOUNTAINS - "The Tower". The layout is deeper than the camera frames, so a level's
                //length is its height and it is worked from the underside up as the glass hands it down.
                //
                //COLUMN OPENS IT since #206, where Crown did before, and that reverses the reasoning written
                //into Crown itself (see its Sky comment, rewritten with this). The block's stated style is "the
                //layout is deeper than the camera frames", and Crown is the one member that is NOT: it is the
                //only 16-level field here, framed whole. Opening on it therefore spent the chapter's first
                //level on the one that does not demonstrate what the chapter is. Column is the plainest tall
                //level in the game — a column reaching out of shot, no second idea in it — so it states the
                //block's premise in its first minute. The cost is that it is also the LARGEST budget in the
                //game (90 shots, ceiling every 5), so the chapter opens on its longest level; Crown moving to
                //second keeps its teaching intact, the axis and the drain up the middle of it reading just as
                //well behind the premise as ahead of it.
                //
                //PAGODA CLOSES IT since #501: the owner's play found it "quite big and hard — the hardest of
                //the chapter", and the rule for the order inside a block (#413) is that difficulty may trend
                //upward as long as hard and easy alternate, with the hardest last. It sat fifth, in the middle,
                //where #255 put it as one of the five new designs; the five after it move up one. Pylon's own
                //comment called itself the finale for its physics thesis — it keeps the thesis and gives up
                //the slot. The unlock ramp is a function of position (MinStarsAt), so the swap costs it
                //nothing; the gates and ScoreSim ran over the regenerated set.
                Column(), Crown(), Horn(), Helix(), Spyglass(), Belfry(), Organ(), Pylon(), Lean(), Pagoda(),

                //5. THE AURORA - "The Silhouettes" (#491). Picture walls again, but drawn by the local image
                //generator rather than by hand: black cut-outs over a pale check, painted from the fourth level
                //on. INSERTED rather than appended - the Mirage keeps the campaign's last word (#420's ruling) -
                //and inserted HERE because it is the night between the Tower's dusk and the Reveal's cavern, and
                //a picture chapter is the breather after the Tower's long climbs. Fish opens (the plainest shape,
                //one ink), Anchor closes (four parts in three inks). Every gate after it moves up twenty stars,
                //which MinStarsAt does by position and a save survives by file name.
                Fish(), Umbrella(), Bell(), Cat(), Teapot(), Key(), Rocket(), Coronet(), Guitar(), Anchor(),


                //6. THE CAVERN - "The Reveal". An outer body with a differently-shaped thing standing inside
                //it; clearing the outside is the payoff (#161).
                //SPRING BEFORE SHIP since #413, on the owner's playtest: Ship rated higher difficulty than
                //Spring and stood in front of it, which the tool's own ratio agrees with (Ship 2.45 shots a
                //group against Spring's 4.80). One swap; nothing else in the block moved.
                Onion(), Chest(), Fossil(), Mango(), Spark(), Grotto(), Scales(), Spring(), Ship(), Lantern(),

                //7. THE MOON - "The Quarry". Chunky lattice-aligned blocks of colour, five or six of them, and
                //no plate to trigger anywhere: every shot is a shot at a handful of balls. Colossus closes it,
                //from WriteLevelSet.
                Mosaic(), Prism(), Hopper(), Trilithon(), Gantry(), Fault(), Crib(), Highwall(), Static()
            };

            //8. THE NEBULA (#182) - the arena in deep space, and the block the five #152 colours arrive in,
            //one or two per level until the finale plays all thirteen. Every level is TALL and OPEN in the
            //Helix's sense - the silhouette turns and changes as it descends, so the player reads what is
            //coming - and each is a different KIND of tall, the Tower's own rule (#160). See the block's
            //region for why a second tall block exists at all when the Coil recorded that only the Tower
            //should be one. The block lives in its own array because WriteLevelSet appends Colossus after
            //everything in the array above: five designs added THERE would still make contiguous blocks
            //(the names fall out of positions, so nothing refuses the set) but would misfile them - Comet
            //labelled the Quarry's, Colossus labelled the Nebula's, and THE QUARRY COMPLETE celebrating on
            //the wrong level. Only DescribeBlock's non-gating MIXED print would show it.
            //ORRERY CLOSES THE BLOCK since #413, where Garland did - the owner's playtest ("very demanding
            //but nice, I'd picture this as the chapter's last") and the tool agreeing: 52 standing groups
            //against 72 shots is 1.38 a group, the block's tightest by a factor of two and at the hard edge
            //of the whole game. THE COLOUR RAMP SURVIVES IT, which is the only thing that could have
            //refused the swap: Kepler, Orrery and Garland all play thirteen, so the finale still plays
            //every colour the game has. What it costs is stated rather than hidden - Garland is the harder
            //DRAW (thirteen live colours against Orrery's release quanta) and now stands second-to-last, so
            //the block ends on the tighter budget rather than on the scarcer magazine.
            //⚠ WISHBONE'S DIP (fourth, right after Carousel) IS LEFT STANDING and it is not an oversight:
            //inside this block's own colour ramp there is no move that fixes it. Wishbone plays six colours
            //where Vortex and Carousel play five, so pulling it earlier puts a six-colour level in front of
            //two five-colour ones, and the only other lever is its budget (54 shots on 11 groups, 4.91) -
            //which is a level-design change and not an ordering one. #413 says so itself.
            Design[] nebula = { Comet(), Vortex(), Carousel(), Wishbone(), Sail(), Analemma(), Binary(), Kepler(), Garland(), Orrery() };

            //9. THE VOLCANO - "The Eruption" (#295). THE GLOW IS THE LOAD: the molten seams are what
            //everything hangs by, so reading where a level shines is reading where it will break - and every
            //level here HAPPENED IN A DIRECTION, a bearing the shape carries (the torn flank, the downhill
            //run, the downwind rake, the leaning column). The arc job is the light returning after the void,
            //GEOLOGICALLY - the earth glowing by itself - one step before the dawn hands the light back
            //received (and two before the finale's neon, since #300 put the cities in day order). Five
            //levels until #369, which filled the block to ten with the five that bring the BOMB and the ZAP
            //into the campaign (#368) - the volcano being the one place a bomb does not have to explain
            //itself. See the block's own region for the statement in full and for the engineering law every
            //design here obeys (a designed breakaway is always the lowest thing on its own load path).
            //CAUSEWAY MOVED FROM SECOND TO EIGHTH (#413), and the level the owner asked about is the one
            //that did NOT move. The playtest note was Breach ("a fairly demanding, big level - not sure it
            //should be first"), and the measurement answers it: at 1.71 shots a group Breach is the second
            //GENTLEST level in the block, which is what its own doc already claimed and what an opener
            //should be - it reads imposing because it is big, not because it is tight. Causeway behind it
            //reads 0.87, the tightest budget in the campaign after Caldera's 0.71 and the subject of #414,
            //so the block's real ordering fault was the SECOND level rather than the first. It now sits
            //beside Caldera, where the two tightest in the chapter belong.
            Design[] volcano =
                { Breach(), Meander(), Volley(), Plume(), Vent(), Sill(), Fume(), Causeway(), Caldera(), Paroxysm() };

            //10. THE CITY AT DAWN - "The Spectrum" (#253). One HUE FAMILY a level, swept through the whole
            //body as a gradient: white to cyan to blue to navy and back, a heat ramp, a green one, a twilight
            //one, and the wheel entire on the finale. No new colour is involved anywhere - a family is a
            //subset and an ordering of the fixed thirteen - and no sweep is a stack of floors, because a
            //floor of one colour is the anchor of everything under it and would end the level on one ball.
            //(#302 added BANDED TIERS to four of these without breaking that sentence - see the block's
            //geometry region header for the rule a tier obeys.)
            //The light ramp's return COMPLETES here: after the volcano's geological glow, this is light
            //RECEIVED again - dawn breaking over the city where the arena has always stood, its neon still
            //off. It closed the campaign until #300 moved it ahead of the Arcade, on the owner's ruling that
            //the neon city reads as coming AFTER the normal one - which it does, the way a day does: the
            //campaign's last stretch is now ONE DAY IN THIS CITY, dawn to night.
            //
            //THE ORDER WITHIN THE BLOCK IS THE SHIPPED PLAY ORDER, AND IT STOPPED BEING THE MEASURED
            //DIFFICULTY RAMP WITH #302. It was slotted by standing-group count (6, 8, 9, 10, 15, 14, 19, 22,
            //26, 31 when #255's five were interleaved), and #302 is the record of why that figure ranks the
            //wrong quantity twice over: it says nothing about whether the remainder HANGS (the sag probe's
            //question - four of these ten lost to gravity before the ceiling moved), and on a design with
            //tiers it prices shots that cascade (one cleared tier orphans everything below it, so Pleat's
            //probe runs clear on a third of the budget). Re-measured after the tiers the counts run 6, 17,
            //9, 35, 15, 19, 26, 22, 26 and 31 - Pleat's 35 at fourth position outranks the Turbine - so the
            //order no longer tracks the count, deliberately. The families are not what ramps - a green
            //level is no harder than a blue one.
            //
            //THE ICICLE STILL OPENS IT AND BOLT NOW CLOSES IT (#413). The opener is unchanged and for the
            //reason it always was, being the plainest body here. The other end is the owner's playtest
            //ruling: "Bolt should be the chapter's last level." What that overrides is worth naming, since
            //this comment used to state it as settled - the Turbine closed the block because it had closed
            //the CAMPAIGN, and #300 moved the campaign's last word to the Arcade a chapter later, so the
            //argument had already outlived itself. The Turbine keeps ninth and keeps its finale figure
            //(1.68 a group, tighter than Bolt's 2.32): the block now ends on the level the owner wants to
            //end on rather than on the tightest ratio, which is the same call #300 made about the cities.
            //Two more moves come from the same playtest, both of them a level put where it plays: TRELLIS
            //AHEAD OF PLEAT (3.47 against 1.37 - it stood behind the block's tightest level and reads
            //easier than it), and KILN PULLED FORWARD to fourth ("nice, large level, not very difficult").
            //Its designs live in their own array for the same reason the Nebula's and the Arcade's do - see
            //WriteLevelSet.
            Design[] spectrum = { Icicle(), Pinecone(), Hourglass(), Kiln(), Trellis(), Pleat(), Totem(), Girandole(), Turbine(), Bolt() };

            //11. THE NEON CITY - "The Arcade" - THE CAMPAIGN'S LAST BLOCK since #300. Five HOLLOW pixel-art
            //solids: the Gallery's drawn symbols given a third dimension, wrapped onto a die, a stepped
            //temple, a slot reel, a donut and a globe, so a level's picture is read by walking the gun round
            //it. Every one is framed whole, which is the deliberate opposite of the tall blocks before it -
            //an object meant to be RECOGNISED has to be in shot.
            //The light ramp ends where the day does. It used to end on the Spectrum's dawn, with the neon
            //city justified as the only lit place reachable straight after the void ("past the void there is
            //no darker place to go"); #295's volcano then gave the return a geological first step and #300
            //swapped the two cities on the owner's ruling. Read in play order the ending is now: dawn breaks
            //over the city (the Spectrum), the day passes through ten hue families, and the campaign ends
            //AFTER DARK with the city turning its own lights on - the made light as the finale's celebration,
            //over the same skyline, where the arena has always stood. The last word rides the set's last
            //entry, so it moves off Turbine onto Globe - and the block that measures tightest (1.33-1.65 a
            //group against the Spectrum's 1.37-6.67) now sits last, which is #300's other half: the campaign
            //climaxes where it ends.
            //THE ZIGGURAT OPENS IT SINCE #413, where the Cube did, and the two halves of that came from
            //opposite directions. The owner's playtest called the ziggurat a "nice, simple level" that
            //belongs near the chapter's start and reported the CUBE as severe (#359); the tool says the
            //same thing in its own terms - the cube reads 1.33 shots a group, the tightest in a block whose
            //whole band is 1.33 to 1.65, so the chapter opened on its hardest level. The cube takes sixth.
            //Globe still closes it and still closes the campaign (#300): that end was never in question.
            //Its designs live in their own array for the same reason the Nebula's do - see WriteLevelSet.
            Design[] arcade = { Ziggurat(), Reel(), Donut(), Ghost(), Cabinet(), Cube(), Tetra(), Giza(), Trophy(), Globe() };

            //13. THE DREAM - "The Mirage" (#323/#325), THE CAMPAIGN'S LAST BLOCK, and the first chapter in
            //the game whose subject is a RULE rather than a shape. Ten levels, and they are two fives: the
            //first five hang the TRANSPARENT ball - the shell with no colour in it until a shot lands beside
            //it and gives it one - and the last five hang the ROCK, which takes no colour ever and which no
            //shot removes. ONE NEW KIND A LEVEL, never both, which is the owner's own instruction and the
            //reason the block is ten and not five: a player meeting two new rules in one level learns
            //neither.
            //
            //THE GLASS COMES FIRST, also the owner's call, and it is the right way round on the ramp as well.
            //The transparent ball is the GENEROUS rule - one shot into a pocket of glass pays several times
            //over, and the five levels here are built to pay - where the rock is the rule that says no. A
            //chapter that opened on the ball you cannot shoot would be a chapter that opened by taking
            //something away.
            //
            //THE SCENE IS THE DREAM, the one fully-built backdrop no shipped level had ever named (the note
            //in docs/scenes.md that said so is retired by this block). It is not a leftover: the dream's own
            //signature is hard glassy solids tumbling and melting into one another under a sky of slow
            //marbled colour, so the chapter about a ball that is glass until it becomes a colour plays in
            //the one place where that is what the BACKDROP is already doing. The dome number is inert here -
            //the dream replaces the sky and states its own light rig - and every level names the same one
            //anyway so DescribeBlock has something to agree with.
            //
            //THE LIGHT RAMP does not continue, and that is deliberate rather than an oversight. The campaign
            //walked the day down from green noon to the neon city after dark (see the Arcade), and the day
            //ENDED there. What follows a day is not a later hour of it; the eleventh chapter is the one
            //place the arena is not, and the balls stop obeying the rules the other hundred levels taught.
            //Its designs live in their own array for the reason the Nebula's, the Eruption's, the Spectrum's
            //and the Arcade's do - see WriteLevelSet.
            //THREE MOVES FROM THE OWNER'S PLAYTEST (#413), and the two fives are untouched as fives - the
            //glass half still comes first and one new kind still arrives a level, which is the block's own
            //law and was never in question. TREFOIL OPENS instead of Facet ("very nice, pleasant level -
            //should be this chapter's first!"), which costs the one thing Facet's doc claims for itself:
            //that the chapter's first shot teaches the glass. It is a cost and not a wash, so it is written
            //into both designs - Trefoil is glass too and teaches it a level later. KEYSTONE PULLED FORWARD
            //to second of the rock five ("clears very quickly by shooting upward"), and CAIRN CLOSES THE
            //BLOCK AND THE CAMPAIGN where Obsidian did: the owner's note on Obsidian was "very simple level,
            //I don't know if it should be last. Probably not!", and the tool reads it the loosest in the
            //block at 2.94 shots a group against Cairn's 1.76. Cairn is also the level whose own doc asks
            //for "bookkeeping of a kind nothing before it has asked for" - four chambers, each with the
            //four colours in a different order - which is a finale's job.
            //12. THE GRID (#420) - the arena inside the machine, and the one block whose style is a THESIS
            //rather than a family of silhouettes: every level is a NAMED MATHEMATICAL CONSTRUCTION. The scene
            //(#393) is built on that same sentence - its floor is a Hilbert curve rather than a noise field,
            //"named mathematics rather than noise" - so the chapter is the cluster answering the backdrop.
            //It is also the campaign's first block to carry a SPECIAL: the wildcard, taught the Eruption's
            //way, cheap and unmissable on Sierpinski and a tool on Gyroid. See Designs/Block12_Grid.cs for
            //what each construction is and for the block's one standing danger, which is thinness.
            Design[] grid =
            {
                Menger(), Sierpinski(), Cantor(), Koch(), Hilbert(),
                Helicoid(), Phyllotaxis(), Gyroid(), Life(), Tesseract(),
            };

            Design[] mirage =
            {
                Trefoil(), Facet(), Harlequin(), Diadem(), Solitaire(),
                Anvil(), Keystone(), Seam(), Obsidian(), Cairn(),
            };

            bool ok = true;
            foreach (Design design in designs) ok &= LevelEmitter.Emit(design);
            foreach (Design design in nebula) ok &= LevelEmitter.Emit(design);
            foreach (Design design in volcano) ok &= LevelEmitter.Emit(design);
            foreach (Design design in spectrum) ok &= LevelEmitter.Emit(design);
            foreach (Design design in arcade) ok &= LevelEmitter.Emit(design);
            foreach (Design design in grid) ok &= LevelEmitter.Emit(design);
            foreach (Design design in mirage) ok &= LevelEmitter.Emit(design);

            LevelSet set = CampaignSet.WriteLevelSet(designs, nebula, volcano, spectrum, arcade, grid, mirage);

            //The gate that hangs the levels instead of reading them (#301/#302). Off the WRITTEN SET rather
            //than off the designs above, for two reasons: the set is where a level's budget and ceiling step
            //actually live, and it is the only list that includes the hand-drawn Colossus - a shipped level
            //this gate has as much business asking about as any generated one.
            if (sag) ok &= RunSagGate(set, sagOnly);

            //A non-zero exit so this can be put in front of a commit: a level that fails the checks is a
            //level that plays wrong, and the whole point of generating them is that nobody has to notice
            if (!ok) Console.WriteLine("At least one level FAILED its checks - see above.");

            return ok ? 0 : 1;
        }

        /// <summary>
        /// Hangs level FILES rather than set entries (#333) — a level that is not in the campaign yet, which
        /// is what a level built to try a mechanic on always is.
        /// <para>
        /// <b>Its two figures are stated rather than read</b>, since a file outside the set carries neither:
        /// no budget (the run ends when nothing removable is left standing) and a glass that holds still. That
        /// is the gentler of the two pressures on purpose — what such a level is being asked is whether its
        /// own shape hangs, and a ceiling step would mix the other pressure into the answer. #288's own
        /// distinction, kept on the side of the question being asked.
        /// </para>
        /// </summary>
        private static bool RunSagFiles(string[] paths)
        {
            Console.WriteLine("=== sag probe: the named level FILES, no budget and the glass at rest ===");

            bool ok = true;

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  {Path.GetFileName(path),-16} MISSING - not on disk");
                    ok = false;
                    continue;
                }

                SagProbe.Run[] runs = SagProbe.Play(path, int.MaxValue, 0, trace: paths.Length == 1);
                SagProbe.Run worst = SagProbe.Worst(runs);
                int sags = runs.Count(r => r.Outcome == SagProbe.Outcome.Sagged);

                Console.WriteLine($"  {Path.GetFileName(path),-16} sagged {sags} of {runs.Length}; worst: "
                    + $"{worst.Outcome,-11} after {worst.Shots,3} shot(s)"
                    + $", closest the line came {worst.WorstClearance,6:F2}");

                int heavy = SagProbe.CountHeavy(path);
                if (heavy == 0) continue;

                SagProbe.Run[] plain = SagProbe.HeavyBaseline(path, int.MaxValue, 0);
                SagProbe.Run plainWorst = SagProbe.Worst(plain);
                int plainSags = plain.Count(r => r.Outcome == SagProbe.Outcome.Sagged);

                Console.WriteLine($"      {heavy} heavy ball(s); the same level at ordinary mass sagged "
                    + $"{plainSags} of {plain.Length}, closest {plainWorst.WorstClearance,6:F2}"
                    + $" - the mass is worth {plainWorst.WorstClearance - worst.WorstClearance,5:F2} of clearance");
            }

            return ok;
        }

        /// <summary>
        /// Asks the shortest-clear gate (#458) about level FILES rather than about the designs this tool
        /// writes — a scratch level, a level being redrawn, or the one shipped level nothing here generates.
        /// <b>Colossus is the reason it exists</b>: it is hand-drawn, so <see cref="LevelGates.Validate"/> never sees it,
        /// and "every shipped level needs at least four shots" is a claim about the campaign rather than about
        /// the generator. Always deep, since a file asked about one at a time is worth the beam's tenth of a
        /// second.
        /// </summary>
        private static bool RunClearFiles(string[] paths)
        {
            Console.WriteLine("=== shortest clear: the named level FILES ===");

            bool ok = true;

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  {Path.GetFileName(path),-16} MISSING - not on disk");
                    ok = false;
                    continue;
                }

                Level level = Level.Load(path);
                ClearProbe.Reading clear = ClearProbe.Measure(level.Map, deep: true);

                Console.WriteLine($"  {Path.GetFileName(path),-16} {clear.Removable,4} removable balls;"
                                  + $" shortest clear: {clear.Describe()}"
                                  + (clear.TooCheap ? "  <-- CLEARS TOO CHEAPLY" : string.Empty));

                if (clear.TooCheap) ok = false;
            }

            return ok;
        }

        /// <summary>
        /// <b>What #514 asks of the shipped pack, before anything is gated on it.</b> For every landing a shot
        /// could be aimed at on the intact field, it asks which station round the orbit a straight shot can
        /// actually arrive from, and reports three things: landings no station on the whole orbit reaches (a
        /// fault, if any exist), landings the <b>opening stance alone</b> cannot reach, and the furthest walk
        /// any landing demands.
        /// <para>
        /// The middle number is the measurable form of #457's question — whether a level needs the A/D control
        /// its tutorial card has not taught — and the first is the one that could ever refuse a design. It is a
        /// report and not a gate deliberately: a check that refuses level designs has to be shown first not to
        /// refuse the designs that already play, and the way to show that is to print what it says about all of
        /// them and look.
        /// </para>
        /// <para>
        /// ⚠ It reads the <b>intact</b> field, which is the pessimistic end of the answer and is stated as
        /// such: a cut opens lines that were closed, so the true figure over a whole play can only be better
        /// than this one. Nothing here should be read as "this many landings are unreachable in play".
        /// </para>
        /// </summary>
        private static bool RunArrivalFiles(string[] paths)
        {
            Console.WriteLine($"=== shot arrival: {paths.Length} level file(s), on the intact field and after each cut ===");

            bool ok = true;

            //Hoisted out of the loop below: a stackalloc inside one grows the frame once per level (CA2014),
            //and the buffer is the same size for every field anyway.
            Span<XZLevel> found = stackalloc XZLevel[BallsMap.MAX_NEIGHBORS];

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  {Path.GetFileName(path),-16} MISSING - not on disk");
                    ok = false;
                    continue;
                }

                Level level = Level.Load(path);
                BallsMap map = new(level.Map);
                StaticBall[,,] array = map.GetStaticBallsArray();

                int sizeX = map.StageSizeX, sizeZ = map.StageSizeZ, levels = map.Levels;
                int n = sizeX * sizeZ * levels;

                bool[] present = new bool[n];
                for (int l = 0; l < levels; l++)
                    for (int x = 0; x < sizeX; x++)
                        for (int z = 0; z < sizeZ; z++)
                            present[(l * sizeX + x) * sizeZ + z] = array[x, z, l] != null;

                ArrivalProbe probe = new(map);
                XZLevel size = new(sizeX, sizeZ, levels);

                int landings = 0, unreachable = 0, needsWalk = 0, fromRest = 0;
                float furthest = 0f;

                for (int l = 0; l < levels; l++)
                    for (int x = 0; x < sizeX; x++)
                        for (int z = 0; z < sizeZ; z++)
                        {
                            int cell = (l * sizeX + x) * sizeZ + z;
                            if (present[cell]) continue;

                            //A landing is an empty cell that touches something standing — the same definition
                            //ClearProbe's own move generation uses, so the two probes are asking about the
                            //same set of shots.
                            bool touches = false;
                            int count = BallsMap.FillNeighboringCells(new XZLevel(x, z, l), size, found);
                            for (int i = 0; i < count && !touches; i++)
                                touches = present[(found[i].Level * sizeX + found[i].X) * sizeZ + found[i].Z];

                            if (!touches) continue;

                            landings++;
                            int station = probe.ArrivalStation(present, cell);

                            if (station < 0) { unreachable++; continue; }

                            float walk = ArrivalProbe.WalkDegrees(station);
                            if (walk > 0f) needsWalk++; else fromRest++;
                            if (walk > furthest) furthest = walk;
                        }

                //⚠ AND THE SAME QUESTION ASKED OF THE LEVEL AS IT IS PLAYED (#514). The figures above are
                //the intact cluster, which is the pessimistic end: a cut opens lines that were closed, so a
                //landing reachable from no station now very often is one later. ClearProbe replays the line
                //that cleared the level through BallsMap itself, and hands the field over after each cut —
                //the game's own map code, not a second copy of the rules — so the same probe can be asked
                //again at each state and the two numbers stand side by side.
                int played = 0, playedLandings = 0, playedUnreachable = 0;

                //A heap array rather than the stack span above: a Span is a ref struct and cannot be captured
                //by the lambda below.
                XZLevel[] scratch = new XZLevel[BallsMap.MAX_NEIGHBORS];

                ClearProbe.Measure(level.Map, deep: false, afterMove: cut =>
                {
                    StaticBall[,,] now = cut.GetStaticBallsArray();
                    bool[] standing = new bool[n];

                    for (int l = 0; l < levels; l++)
                        for (int x = 0; x < sizeX; x++)
                            for (int z = 0; z < sizeZ; z++)
                                standing[(l * sizeX + x) * sizeZ + z] = now[x, z, l] != null;

                    played++;

                    for (int l = 0; l < levels; l++)
                        for (int x = 0; x < sizeX; x++)
                            for (int z = 0; z < sizeZ; z++)
                            {
                                int cell = (l * sizeX + x) * sizeZ + z;
                                if (standing[cell]) continue;

                                bool touches = false;
                                int count = BallsMap.FillNeighboringCells(new XZLevel(x, z, l), size, scratch);
                                for (int i = 0; i < count && !touches; i++)
                                    touches = standing[(scratch[i].Level * sizeX + scratch[i].X) * sizeZ + scratch[i].Z];

                                if (!touches) continue;

                                playedLandings++;
                                if (probe.ArrivalStation(standing, cell) < 0) playedUnreachable++;
                            }
                });

                float playedShare = playedLandings == 0 ? 0f : 100f * playedUnreachable / playedLandings;
                string afterCuts = played == 0
                    ? "  (no proven line to replay)"
                    : $"  after {played} cut(s): {playedLandings,5} landings, {playedUnreachable,4} from no station ({playedShare:F0} %)";

                Console.WriteLine($"  {Path.GetFileName(path),-16} {landings,5} landings;"
                                  + $" {fromRest,5} from the opening stance;"
                                  + $" {needsWalk,5} need a walk;"
                                  + $" {unreachable,4} from no station"
                                  + $" ({(landings == 0 ? 0f : 100f * unreachable / landings):F0} %);"
                                  + $" furthest {furthest,5:F1} deg"
                                  + afterCuts);
            }

            return ok;
        }

        /// <summary>
        /// How few shots a level may leave unspent by the dearest order that cleared it before the sag table
        /// marks it THIN (#414). A <b>ranking and not a verdict</b>, like everything else that table prints:
        /// this probe shoots at random and a random player is worse than the one a budget is priced for, so a
        /// thin margin here is a thing to go and look at rather than a refusal.
        /// <para>
        /// Six, and the figure is the owner's playtest rather than a round number: Causeway's worst clearing
        /// order spent <b>50 of 52</b> and its own design doc called that measured and safe, while the report
        /// from play was that players run out of balls on it. Two is inside the range one ricochet or one
        /// colour misread costs. Six is two of those plus one, which is the least that reads as margin.
        /// </para>
        /// </summary>
        private const int CLEAR_MARGIN_TO_REPORT = 6;

        /// <summary>
        /// Hangs every level of the set in the real simulation and plays it, printing what the death line had
        /// to say — see <see cref="SagProbe"/> for what it does and why nothing else in this tool could have
        /// caught what it catches.
        /// <para>
        /// <b>It is opt-in (<c>--sag</c>), and unlike everything else here it REFUSES NOTHING.</b> Two
        /// separate reasons, and only the first is about cost. The other gates read a layout and cost
        /// milliseconds, so they run on every invocation and stand in front of a commit; this one steps a
        /// nine-hundred-body simulation through whole levels several ways, which is minutes for the pack.
        /// <c>--sag=Pylon,Orrery</c> narrows it to the levels named, which is the form to use while
        /// iterating on one, and turns on a shot-by-shot trace when it is given exactly one.
        /// </para>
        /// <para>
        /// <b>⚠ The second reason is that its threshold is fitted to twelve levels.</b> The owner's playtest
        /// gives three levels known finishable (<see cref="Amphora"/>, <see cref="Giza"/>,
        /// <see cref="Saturn"/>) against the nine known not to be, and
        /// <see cref="SagProbe.SAG_RUNS_TO_REPORT"/> separates them cleanly on that set — seven of the nine
        /// named, none of the three. That is a real calibration and it is still twelve levels, so a run of
        /// this prints a <i>ranking</i> and refuses nothing: the levels it names are where to look first, not
        /// a verdict to redraw a design on. Doing that off an uncalibrated number is #288's mistake with a
        /// better tool, and the whole of <see cref="SagProbe"/> exists because that mistake was made once.
        /// </para>
        /// </summary>
        /// <returns>
        /// Always true. The signature is kept so the day this is calibrated it can start refusing without the
        /// call site moving — and it is written out rather than made void so that nobody has to guess whether
        /// a silent exit code meant the levels passed.
        /// </returns>
        private static bool RunSagGate(LevelSet set, string[] only)
        {
            Console.WriteLine();
            Console.WriteLine("=== sag probe: every level hung in the real simulation and played ===");
            Console.WriteLine("    (clear margin = shots left unspent by the dearest order that actually cleared it)");
            Console.WriteLine("    (clearance = how far the lowest ball stayed above the death line;"
                              + " negative is under it and still inside the swing allowance)");
            Console.WriteLine("    ⚠ A RANKING, NOT A VERDICT - it refuses nothing and fails nothing."
                              + " See RunSagGate's doc for what it is not yet entitled to decide.");

            bool ok = true;

            for (int i = 0; i < set.Count; i++)
            {
                LevelSetEntry entry = set.Levels[i];

                if (only.Length > 0 && !only.Any(name =>
                        entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

                string path = Path.Combine(LevelEmitter.OutDir, entry.File);

                //A missing file IS reported as a failure, and it is the one thing here that can be: it is a
                //fact about the disk rather than a verdict about a design.
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  {i + 1,2}. {entry.Name,-12} MISSING - {entry.File} is not on disk");
                    ok = false;
                    continue;
                }

                //Both are optional in the format and both absences mean "no pressure": no budget, and a glass
                //that holds still. A run with no budget still ends - the probe stops when nothing matchable is
                //left standing - so the cap can honestly be the largest there is.
                int shots = entry.Shots ?? int.MaxValue;
                int ceilingStep = entry.CeilingStep ?? 0;

                //Narrowed to one level, the shot-by-shot comes with it: a verdict says a level sags, and only
                //the trace says at which cut, which is the form an author can act on.
                SagProbe.Run[] runs = SagProbe.Play(path, shots, ceilingStep, trace: only.Length == 1);
                SagProbe.Run worst = SagProbe.Worst(runs);
                int sags = runs.Count(r => r.Outcome == SagProbe.Outcome.Sagged);
                //PRINTED, NOT THRESHOLDED (#556): how many of those losses came with the glass still at rest. A
                //second, lower threshold on this count was measured over the whole pack and refused - Amphora, the
                //calibration's known-finishable level, loses 4 of 5 all at rest, so any threshold low enough to
                //name Moon names it too. See "The sag gate" in docs/formats-and-tools.md.
                int restSags = runs.Count(r => r.Outcome == SagProbe.Outcome.Sagged && !r.CeilingHadMoved);

                //HOW OFTEN, not whether - the fraction is the reading, because "some order loses this level"
                //turned out to be true of levels that play perfectly well. The threshold is calibrated against
                //the owner's playtest (SagProbe.SAG_RUNS_TO_REPORT); it does NOT set `ok`, because a threshold
                //fitted to twelve levels is not yet entitled to refuse a design - see this method's doc.
                bool sagged = sags >= SagProbe.SAG_RUNS_TO_REPORT;

                //THE CLEAR MARGIN (#414), which is a different question from everything above it and was
                //readable here all along without being named: how many shots the DEAREST order that actually
                //cleared the level left unspent. The sag probe asks whether the cluster survives; a level can
                //survive every order and still be one the player runs out of balls on, which is exactly what
                //the owner reported on Causeway - its worst clearing order spent 50 of 52 and the doc called
                //that measured and safe. Two shots is inside the range a real player loses to one ricochet.
                //
                //⚠ It is taken over the CLEARING runs only. A run that ended OutOfShots spent the whole budget
                //by definition, so folding those in would price every level at a margin of zero and say
                //nothing - and this probe shoots at random, so its running out is not evidence (the outcome's
                //own note says so). A level no order cleared has no margin to report rather than a margin of
                //nought; it is the sag lines above that speak for those.
                int[] cleared = runs.Where(r => r.Outcome == SagProbe.Outcome.Cleared).Select(r => r.Shots).ToArray();
                int? margin = entry.Shots.HasValue && cleared.Length > 0 ? entry.Shots.Value - cleared.Max() : null;

                Console.WriteLine($"  {i + 1,2}. {entry.Name,-12} sagged {sags} of {runs.Length} ({restSags} at rest); worst: "
                    + $"{worst.Outcome,-11} after {worst.Shots,3} shot(s) of "
                    + $"{(entry.Shots.HasValue ? entry.Shots.Value.ToString() : "∞"),3}"
                    + $", closest the line came {worst.WorstClearance,6:F2}"
                    + (margin is int m
                        ? $", clear margin {m,3}" + (m < CLEAR_MARGIN_TO_REPORT ? " <-- THIN" : string.Empty)
                        : ", clear margin    -")
                    + (sagged
                        //Which pressure ended it, because that is the distinction #288 got the wrong way
                        //round: a level that sags with the glass still at rest is a LAYOUT fault, and no
                        //value of ceilingStep can reach it.
                        ? worst.CeilingHadMoved
                            ? "  <-- SAGGED (the glass had stepped)"
                            : "  <-- SAGGED WITH THE GLASS AT REST - a layout fault"
                        : string.Empty));

                //A HEAVY LEVEL IS HUNG TWICE (#333), and the second pass is the only way to read the first:
                //the mass is a sag the author asked for, so what this gate has to separate is a sag that is
                //the MECHANIC from a sag that is the LAYOUT with a heavy ball standing in it. Neither the
                //threshold nor the death line moves for an authored sag - see SagProbe.HeavyBaseline for why
                //that would forgive a level that cannot be played.
                int heavy = SagProbe.CountHeavy(path);
                if (heavy == 0) continue;

                SagProbe.Run[] plain = SagProbe.HeavyBaseline(path, shots, ceilingStep);
                SagProbe.Run plainWorst = SagProbe.Worst(plain);
                int plainSags = plain.Count(r => r.Outcome == SagProbe.Outcome.Sagged);

                Console.WriteLine($"      {heavy} heavy ball(s); the same level at ordinary mass sagged "
                    + $"{plainSags} of {plain.Length}, closest {plainWorst.WorstClearance,6:F2}"
                    + $" - the mass is worth {plainWorst.WorstClearance - worst.WorstClearance,5:F2} of clearance"
                    + (sagged
                        ? plainSags >= SagProbe.SAG_RUNS_TO_REPORT
                            ? "  <-- THE LAYOUT, not the mass: it sags at ordinary mass too"
                            : "  <-- THE MASS: this level hangs too much off its heavy ball(s)"
                        : string.Empty));
            }

            return ok;
        }

        /// <summary>
        /// The game's <c>Game\Levels</c>, found by walking up from wherever this was built to the repository
        /// root. The tool lives at a known depth inside the repo but is <i>run</i> from its bin directory,
        /// whose depth depends on the configuration and target framework, so the walk is by landmark rather
        /// than by counting <c>..</c> — and an explicit directory can always be passed instead.
        /// </summary>
        private static string FindLevelsDirectory()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Game", "Levels");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException(
                $"No 'Game\\Levels' directory above '{AppContext.BaseDirectory}'. Pass the output directory as an argument.");
        }
    }
}
