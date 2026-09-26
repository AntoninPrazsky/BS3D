namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Writes every level of the campaign but the hand-drawn Colossus, and the set that orders them, and
    /// <b>validates every one through the game's own loader</b> before it is written anywhere the game will see
    /// it. A design is one <see cref="Design"/>: a silhouette, a colouring, a scene and a set of rules.
    /// <para>
    /// It exists because these levels are generated, and a generated level that is only checked by
    /// playing it is checked by nobody. The three properties it enforces are all invisible in a
    /// screenshot and all of them were got wrong at least once here — see <see cref="LevelGates.Validate"/>,
    /// <see cref="LevelGates.DropTest"/> and <see cref="LevelGates.FindLonelyBalls"/>. What it cannot check is
    /// whether the thing looks good, which is what the screenshot skill is for.
    /// </para>
    /// <para>
    /// <b>Nor could any of them, until #301, check whether a level survives being taken apart</b> — every
    /// check above reads the layout as authored, and all of them are true of a level that cannot be
    /// finished, because none knows what the remainder <i>weighs</i>. <see cref="SagProbe"/> is the answer
    /// and it hangs the level in the real simulation instead of reading it;
    /// <see cref="LevelGates.WorstAnchorLoad"/> is the cheap figure that came out of building it. Read
    /// <see cref="RunSagGate"/> for why the first is opt-in and <c>docs/formats-and-tools.md</c> for what it is
    /// not yet entitled to decide.
    /// </para>
    /// <para>
    /// <b>The tool is split by what each part does (#597), as #386 had already split the designs by block.</b>
    /// <c>Program</c> is the designs and the entry point: this file holds the figures every design is drawn
    /// against, each block's designs — with every helper only that block's designs use — are in
    /// <c>Designs/BlockNN_Name.cs</c>, a helper the designs of more than one block use is in
    /// <c>Designs/Shared.cs</c>, and <c>Cli.cs</c> holds <see cref="Main"/> (the command line and the
    /// campaign's play order, which is a list of the designs) and the probe runners. Everything else is a type
    /// of its own: <see cref="Design"/>, the <see cref="LevelEmitter"/> that turns one into a level file, the
    /// <see cref="LevelGates"/> every written level is read back through, and the <see cref="CampaignSet"/>
    /// that holds the block, music and ball-style tables and writes the set. So a new design goes into its
    /// block's file, and a new block is a new file there, a row in <see cref="CampaignSet"/>'s tables and an
    /// array in <see cref="Main"/>. <b>A static field's initialiser must not read a static field declared
    /// in another of these files</b>: the order in which a partial class's parts are initialised is unspecified.
    /// </para>
    /// <para>
    /// <c>dotnet run --project Tools\LevelGen\LevelGen.csproj [output directory] [--sag[=Name,Name]]</c>.
    /// Rewrites <c>Levels.json</c> too, One and Colossus included, so run it whole rather than for one level.
    /// </para>
    /// </summary>
    internal static partial class Program
    {
        // The field is 16 levels deep for every design: that is the deepest field the game hangs at its
        // standard height (FIELD_TOP_Y, 8/sqrt2) without raising it off the death line, so every level is
        // framed by the camera and the gun exactly the way One.json is. The layout hangs at the top and
        // the empty levels under it are the room shot balls attach into.
        internal const byte FIELD_LEVELS = 16;

        // The pictures' own, and it is a LEVER ON HANGING HEIGHT rather than on growth room (#203). A field
        // is hung at FIELD_TOP_Y unless it is deep enough that its bottom level would start past the death
        // line, in which case GameplayScreen.FitFieldToMap raises the WHOLE field until the floor clears the
        // line by FIELD_FLOOR_MARGIN — so past 16 levels, adding depth pushes the layout UP rather than
        // leaving room under it. That is the only lever a design has on how much air its lowest row starts
        // with, the layout always hanging at the top of its field.
        //
        // The pictures needed one. A wall is 14 rows in a field of 16, so its lowest ball started 1.96 above
        // the line by centre and 1.46 by surface, where every other level in the pack has at least 2.88 and
        // most have 7 to 8.5 (measured off the game's own [field] line, all five). Three things followed from
        // that, and the third is the one that says this was a fault rather than a tight margin.
        //
        // The floor alarm arms at CEILING_DEATH_Y + 3 steps of 0.6, i.e. -3.70, and these walls START at
        // -3.54 — 0.16 from lighting the net, on a cluster whose own comment budgets "a few tenths of a unit"
        // of bob for a shove. A stalk left dangling two lattice levels under the wall sat at -4.95, one shove
        // or one descent from the line. And an UNTOUCHED wall crossed the line on its FOURTH descent
        // (1.96 / 0.6 = 3.3) against a budget that buys SIX on every one of the five — 60 shots stepping
        // every 10, 55 every 9, 48 every 8 — so the ceiling could end these levels before their own budget
        // ran out. That is the shape of the bug: not a level that is hard, a level whose two clocks disagree.
        //
        // Measured after, on all five (the [field] line again): top Y 5.66 -> 7.02, and it now reports
        // "raised off the line" because the raise is what does the work; floor -4.95 -> -5.00; lowest ball
        // -3.54 -> -2.17, i.e. 1.96 -> 3.33 above the line by centre and 1.46 -> 2.83 by surface. An untouched
        // wall now survives five descents and crosses on the sixth, which is where the budget ends anyway.
        // The raise costs about 2.3 degrees of the gun's elevation budget — [aimcheck]'s steepest cell moves
        // from (0,0,14) at 67.3/69.9 deg to (0,0,16) at 69.6/72.0 of the same 80.2 limit — so all five still
        // PASS with roughly eight degrees spare. GameCameraFit re-solves a hair closer (30.8 -> 30.7 out on
        // the 15-wide pictures, 31.5 -> 31.4 on the 17-wide), which is not a visible change in ball size.
        //
        // EVERY FIGURE ABOVE IS AGAINST THE DEATH LINE AT -5.5, where it stood until it was lowered onto the
        // island (GameplayScreen.CEILING_DEATH_Y is ArenaIsland.TOP_Y + 1 now, after a report that a cluster
        // which merely SWUNG dipped under it a few shots into a level). Re-measured after that move: an
        // 18-level field is no longer raised at all - it is pinned at FIELD_TOP_Y again and hangs 1.36 lower
        // - and a picture's lowest ball starts 3.96 above the line instead of 3.33, which is 6.6 descents
        // against a budget that buys six. The depth still does what it was chosen for; two of the three units
        // of air now come from the line rather than from the raise, and the aimcheck cost the raise used to
        // charge is gone with it (the steepest cell needs 60.5 deg of the 80.2 limit, not 69.6).
        //
        // 18 is the figure and it is also the CEILING: GameplayScreen.FRAMED_LEVELS is 18 and its test is
        // "Levels > FRAMED_LEVELS", so 20 would quietly turn a picture into a tall level, fed by
        // FeedTallColumn and aim-clamped by TALL_AIM_HEADROOM. 18 - 14 = 4 is even, which is what keeps every
        // row's level parity — and therefore the drawing itself — exactly where it was; the emitter refuses
        // an odd offset rather than drawing it shifted. If the air this buys is ever not enough, the next
        // lever is fewer bitmap rows, which means redrawing the pictures.
        private const byte PICTURE_FIELD_LEVELS = 18;

        // THE CEILING BUDGET IS PART OF A DESIGN, NOT A KNOB TURNED AFTERWARDS (#288), and it is arithmetic no
        // gate can do for you — Validate reads the layout as authored and knows nothing about how long the
        // level lasts. Four shipped levels lost to "The cluster reached the line" rather than to their own
        // budget before anyone did the sum. It is two numbers:
        //
        //     clearance      = the [field] line's "above the line", printed by the game on every level load
        //     consumed       = floor(Shots / CeilingStep) * GameplayScreen.CEILING_DESCENT_PER_STEP (0.60)
        //     final headroom = clearance - consumed
        //
        // FINAL HEADROOM MUST CLEAR A SWING, and how deep a swing goes is measured rather than guessed: the
        // probe recorded beside GameplayScreen.CLUSTER_SWING_ALLOWANCE took 35 swings over 67 s on Chest, the
        // second-heaviest cluster in the pack, and the deepest was 0.82 units below the trend. So a level
        // ending with less than that has an ordinary swing UNDER the line however well it is played, and the
        // hold rule then decides it; the allowance itself, 1.00, is the figure to design to. Measured before
        // the fix: Pylon -1.04, Ghost +0.36, Orrery +0.37, Cube +1.18 — the first past the line with no swing
        // at all, on an untouched cluster, at shot 66 of 74.
        //
        // A LOW FIGURE IS NOT BY ITSELF THE FAULT, and this is the part that stops the sum being used as a
        // gate. Horn and Turbine both sit at -0.23 and neither has ever been complained about, because their
        // lowest ball is a TIP — Horn opens upwards from HORN_TIP, so the few balls at the bottom go in the
        // first shots and the true lowest point then jumps several units up. The sum is honest about the
        // cluster AS AUTHORED; what a competent run leaves hanging is the designer's own judgement, and a
        // shape whose lowest balls are structural (Pylon's legs, Ghost's foot) has to be read that way.
        //
        // THE LEVERS, in the order they are worth trying: CeilingStep (costs nothing but pace), then Shots,
        // then FieldLevels — but the third is spent the moment FitFieldToMap has raised a field as far as the
        // floor margin allows (Pylon, floor Y -7.00), and it is closed outright wherever a block has promised
        // its levels are framed whole (the Arcade's 18, GameplayScreen.FRAMED_LEVELS). Changing the layout is
        // the last resort, not the first.

        private const float HALF = 0.5f;
    }
}
