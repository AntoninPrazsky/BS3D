using BS3D.Audio;
using BepuPhysics;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D.Scoring;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;


namespace BS3D.Screens
{
    /// <summary>
    /// <b>A session's lifetime</b> — what "Play" and a level's end actually are. Building one installs the
    /// level, derives everything the field's size and depth decide, and stands up the cluster; tearing one
    /// down returns the simulation to nothing. <see cref="IsBuilt"/> is the question the main menu asks.
    /// </summary>
    /// <remarks>
    /// The one instance is held for the life of the game, so build and tear-down have to be exactly inverse —
    /// a session left half-standing is a level that plays wrong rather than one that crashes, which is worse.
    /// Split out of <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region Building and tearing down a session

        /// <summary>
        /// Tears down whatever session is standing and builds the level at <paramref name="index"/> in its
        /// place — the path a first "Play", a "New Game", a retry and an advance all take.
        /// </summary>
        internal void BuildLevel(int index)
        {
            if (IsBuilt) TearDown();

            //Whatever the last level's victory left in the air goes with it. The display lives on the host and
            //would otherwise still be bursting over the opening seconds of the next level — which reads as the
            //game celebrating a level the player has not played yet.
            Game.Fireworks?.Stop();

            //The previous result's fanfare goes now, or a win's brass would still be ringing over the opening
            //of the next attempt. The theme itself comes up further down — after the install, which is what
            //picks the piece.
            Game.Music?.StopFanfare();

            //The map first: the ceiling's height and footprint come off the field, and the ceiling body has
            //to exist before the cluster, whose top level is constrained to it.
            _levelIndex = index;
            InstallLevel(_levelIndex);

            //And the theme comes up — from the top, since TearDown above has already stopped it — but only
            //now, because InstallLevel is where SetTheme says which of the two pieces this level plays.
            //Started before it, Play would put a pass of the OUTGOING piece on and SetTheme would stop it a
            //moment later: a level set that alternates would open every level with a fragment of the previous
            //level's music, and spend the pass it had in hand doing it.
            Game.Music?.Play();

            BuildPhysicsWorld();
            BuildCluster();

            //FitCeilingToMap made a new renderer, which starts without the sky palette
            Game.ApplySkyLighting();

            IsBuilt = true;
            _clearedCountdown = 0f;

            //The HUD carries state of its own across nothing: a new level starts at zero without counting down
            //to it, and a popup from the level just finished must not fly into the score of the one just built.
            //Seeded from the fresh scorer, so a new budget is not read as a ball just spent.
            _hud.Reset(_score);
            _levelLost = false;

            //The outcome has to be cleared here now that it is read for something other than building the
            //result screen: it gates the HUD, so a level entered with the last one's Failed still standing
            //would play with no readout at all. It was harmless while ShowResultScreen was its only reader —
            //that is set and consumed on the same line — which is exactly how a field like this goes stale.
            _pendingOutcome = LevelOutcome.None;
            _pendingFailure = LevelFailure.None;

            //The drop cinematic's bar is the biggest release of the level being played, so it starts over
            //with the level — otherwise the last one's best would follow the player into this one
            _biggestDrop = 0;

            //The glass is a fresh plate at the top of a fresh field, so nothing about the last level's last
            //descent should still be glowing on it — nor should a step it queued and never got to take come
            //down on the new one.
            _ceilingFlash = 0f;
            _ceilingStepsPending = 0;
            _ceilingStepHold = 0f;
            _ceilingStepWaited = 0f;

            //Where this level hangs its underside is what a tall one is fed back down to, so it is read off
            //the installed map rather than being a constant — see FeedTallColumn
            _feedFloorLevel = _map.GetLowestOccupiedLevel();
            _feedStepsQueued = 0;
            _ceilingFeedStepsQueued = 0;
            _ceilingFlashColor = CEILING_FLASH_COLOR;
            _ceilingFlashIsFeed = false;

            //And no floor alarm either: whatever the last level's ending left lingering over the drain is
            //not this level's danger.
            _laserGrid.Reset();
        }

        /// <summary>
        /// Tears the session down so a new one can be built. The world is disposed outright rather than emptied
        /// ball by ball: the constraints, the bodies, the statics and the per-worker contact queues all go with
        /// it, and rebuilding is a few milliseconds. The <i>order</i> those four go in is
        /// <see cref="PhysicsWorld.Dispose"/>'s, which is where the reason for it is written down as well.
        /// </summary>
        internal void TearDown()
        {
            //There is no level any more, so there is no level theme, and no celebration either. BuildLevel
            //starts the music again; a return to the main menu leaves both stopped. The display outlasts the
            //result screen by design, so without this a win followed by "Main Menu" would go on bursting over
            //the front end for the best part of a minute.
            Game.Music?.Stop();
            Game.Fireworks?.Stop();

            //Idempotent, which is what this needs: DisposeResources runs it again on the way out of the program
            _world?.Dispose();

            _world = null;
            _eventHandler = null;
            _ceiling = null;
            _map = null;
            _physicsBalls = null;

            //Cleared, never reassigned: the contact handler holds these very instances
            _shotBalls.Clear();
            _fallingBalls.Clear();
            _smears.Clear();

            _physicsAccumulator = 0f;
            _magazine.Settle();
            _preciseAim.Reset();

            //A cinematic caught mid-shot by a level ending under it would otherwise hold the camera and the
            //controls into the next level, and its subject handles belong to a simulation that is now gone
            _cinematic.Reset();
            _cinematicSubject.Clear();

            //The magazine is not refilled here: its colours belong to a level, and InstallLevel loads the
            //next one's before the queue means anything again

            //The gun goes back to its resting orbit angle and aim, so a new game starts pointed where the
            //first one did rather than wherever the last shot left the barrel
            _cannon.Restart();

            IsBuilt = false;
        }

        /// <summary>The session's own graphics resources, on the way out of the program.</summary>
        internal void DisposeResources()
        {
            TearDown();

            //The smears' and the beam's billboard quads and the crosshair's one texel — not the trail effect,
            //which the content manager owns and which those two share
            _smears.Dispose();
            _aimBeam.Dispose();
            _crosshair.Dispose();
            _laserGrid.Dispose();
        }

        /// <summary>
        /// Installs the map for one entry of the level set, and everything the field's size and depth decide:
        /// where the lattice meets the world, how high the glass hangs, how big it is, and which colours the
        /// magazine may load. Nothing here touches the simulation — it runs before
        /// <see cref="BuildPhysicsWorld"/>, which needs the ceiling's height and footprint to place its body.
        /// <para>
        /// The file is parsed <b>before</b> anything is installed, so a broken level leaves the previous state
        /// alone and simply falls back to the built-in one, exactly as the Testbed's loader does.
        /// </para>
        /// </summary>
        private void InstallLevel(int index)
        {
            LevelSet levelSet = Game.LevelSet;
            BallsMap map = null;

            //Which piece this level plays (#120). Set before anything else is installed and left alone if the
            //file names none, so the set's own rotation decides — and set even when the load fails below, since
            //the fallback map is still level `index` and should sound like it.
            string namedTheme = null;

            if (levelSet != null && index >= 0 && index < levelSet.Count)
            {
                string path = levelSet.ResolvePath(index);

                try
                {
                    //Both a full level file (marked "bs3d-level", carrying a scene and a sky as well) and a
                    //plain map file are .json, so the loader probes exactly as the Testbed's does. A level's
                    //scene is honoured; the player's own pick from the menu stands when the level has none.
                    if (Level.IsLevelFile(path))
                    {
                        Level level = Level.Load(path);
                        map = new BallsMap(level.Map);

                        if (level.Scene != null) Game.SetScene(level.Scene.Kind);
                        Game.SetSkyDome(Math.Clamp(level.SkyDome, (byte)1, BS3DGame.SKY_DOME_COUNT));
                        namedTheme = level.Music;
                    }
                    else map = new BallsMap(path);

                    Console.WriteLine($"[levels] Loaded {index + 1}/{levelSet.Count} '{levelSet.DisplayName(index)}' "
                        + $"({levelSet.DescribeRules(index)}) from '{path}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[levels] Failed to load '{path}': {e.Message}");
                    map = null;
                }
            }

            Game.Music?.SetTheme(ProceduralMusic.ThemeFor(namedTheme, index));

            _map = map ?? BuildFallbackMap();
            _map.Center();

            //The star rating's yardstick, taken while the level is still whole — clearing it is emptying the
            //map, so this is the last moment the count exists to be read
            _initialBallCount = _map.GetBallsCount();

            FitFieldToMap();
            FitCeilingToMap();

            //The gun and the lens both move with the field's size, and each is placed off the other
            FitCannonAndGameCameraToLevel();

            RecountBallTypes();

            //A whole fresh queue for the new level: its colours belong to a level, and the level the standing
            //queue was drawn from is gone. Refill deals every slot through the loaded hook, which is what
            //clears any half-finished dissolve the last session left in one.
            _magazine.Refill();

            //A fresh scorer per level, holding that entry's rules. Built even when the level fell back to the
            //built-in map, which then has no rules at all and so an unlimited budget and a still ceiling — the
            //same thing an entry that authors no "shots" or "ceilingStep" means.
            _score = new ScoreKeeper(LevelShotBudget(index), LevelCeilingStep(index), _initialBallCount);
        }

        /// <summary>
        /// The procedural pyramid the game shipped with, used only when no level file could be read: a
        /// <see cref="BallsMap"/> carved to a stepped square pyramid, apex down, hanging at the top of a
        /// taller field so the empty levels underneath are room for shot balls to attach into — the same
        /// arrangement a map file carries.
        /// </summary>
        private static BallsMap BuildFallbackMap()
        {
            BallsMap map = new(FALLBACK_X, FALLBACK_Z, FALLBACK_FIELD_LEVELS);

            //Its own generator off a fixed seed, so the pile is reproducible however many shots the
            //magazine's unseeded one has drawn by the time this runs
            Random layout = new(FALLBACK_SEED);

            //The pyramid is built about the centre of the field's topmost level, because that is the level
            //BallsMap.Center() puts on the origin. Odd levels are shifted by +0.5 in X and Z, so which
            //centre that is depends on its parity.
            float topShift = LevelShift(FALLBACK_FIELD_LEVELS - 1);
            float axisX = (FALLBACK_X - 1) * Constants.HALF + topShift;
            float axisZ = (FALLBACK_Z - 1) * Constants.HALF + topShift;

            for (byte level = 0; level < FALLBACK_LEVELS; level++)
            {
                byte fieldLevel = (byte)(level + FALLBACK_EXTRA_LEVELS);

                //Half the pyramid's width here: nothing but the apex cell at the bottom, growing half a unit
                //per level up to the full base at the top. Both this and the cell positions are whole
                //multiples of a half, hence exact in binary — which is why the test below needs no tolerance.
                float half = level * Constants.HALF;
                float shift = LevelShift(fieldLevel);

                for (byte x = 0; x < FALLBACK_X; x++)
                    for (byte z = 0; z < FALLBACK_Z; z++)
                    {
                        //Measured against where the cell actually sits, not its raw index, so a level's
                        //own half-unit offset cannot throw the flank out of line
                        if (MathF.Abs(x + shift - axisX) > half) continue;
                        if (MathF.Abs(z + shift - axisZ) > half) continue;

                        map.PutBallAt(x, z, fieldLevel, layout.Next(DEFAULT_BALL_TYPES.Length) switch
                        {
                            0 => DEFAULT_BALL_TYPES[0],
                            1 => DEFAULT_BALL_TYPES[1],
                            2 => DEFAULT_BALL_TYPES[2],
                            _ => DEFAULT_BALL_TYPES[3],
                        });
                    }
            }

            return map;
        }

        /// <summary>
        /// The half-unit offset a level's cells carry in X and Z: odd levels of the lattice are shifted, which
        /// is what nests each layer into the pockets of the one below. Mirrors
        /// <see cref="BallsMap.GetRealPosition"/>, which is where the shift actually happens.
        /// </summary>
        private static float LevelShift(byte level) => (level % 2) > 0 ? Constants.HALF : 0f;

        //There is no MapToWorld/WorldToMap pair here. The lattice-to-world offset used to be applied wherever
        //a grid position was drawn; now the bodies ARE the drawn positions, so the offset is applied exactly
        //twice — once to place them (BuildBallsStructure's worldOffset) and once inside
        //BallContactEventHandler, which takes a world contact down into the grid frame to ask the map about it.
        //Keeping a general-purpose converter around invited a third, uncounted crossing.

        /// <summary>
        /// Derives everything the loaded field's size and depth decide. See <see cref="_clusterWorldOffset"/>
        /// for why the offset has an X and a Z as well as a Y.
        /// </summary>
        private void FitFieldToMap()
        {
            XZLevel size = _map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);

            //The residual the centring leaves: the midpoint of the top level's own cells, measured through
            //the map's public centred-position accessor rather than re-deriving its arithmetic here
            Vector3 nearCorner = _map.GetRealCenteredPosition(new XZLevel(0, 0, topLevel));
            Vector3 farCorner = _map.GetRealCenteredPosition(new XZLevel(size.X - 1, size.Z - 1, topLevel));

            //Where the field's topmost level hangs: FIELD_TOP_Y, the frame every field is hung in — unless
            //the field is deep enough that its bottom level would start past the death line, in which case
            //the whole field is raised just enough that it does not (see FIELD_FLOOR_MARGIN, which is also
            //where the one map this moves is accounted for). The depth that matters is the FIELD's, not the
            //layout's: every cell has to be reachable without ending the level, or the empty levels an
            //author left as growth room are a trap instead of a clearance.
            float fieldTopY = MathF.Max(FIELD_TOP_Y,
                CEILING_DEATH_Y + FIELD_FLOOR_MARGIN + topLevel / Constants.SQRT_TWO);

            _clusterWorldOffset = new Vector3(
                -(nearCorner.X + farCorner.X) * Constants.HALF,
                fieldTopY - topLevel / Constants.SQRT_TWO,
                -(nearCorner.Z + farCorner.Z) * Constants.HALF);

            _ceilingY = CeilingPlate.CentreYAbove(fieldTopY);
            //Kept, because _ceilingY is about to start descending and the HUD's profile has to go on framing
            //the whole fall against where the glass STARTED — raise included.
            _ceilingRestY = _ceilingY;
            //At rest to start: target equals current, so nothing slides until a step is taken.
            _ceilingTargetY = _ceilingY;
            _ceilingDescending = false;
            //Where precise aim converges its crosshair: the middle of the play space in world Y. On a field
            //taller than the camera frames that is the middle of the FRAMED window and not of the field —
            //the lens converging halfway up a forty-level column would be aiming at balls the player cannot
            //see, let alone reach.
            int centredLevels = Math.Min((int)topLevel, FRAMED_LEVELS - 1);
            _clusterCentreY = centredLevels * Constants.HALF / Constants.SQRT_TWO + _clusterWorldOffset.Y;

            //The floor alarm's net, rebuilt at this field's footprint — asked of CeilingPlate.FootprintFor, so
            //it is the very margin the glass over the field covers the balls with rather than a second +1
            //written out here. It hovers where a ball's SURFACE would touch at the moment of loss: the death
            //line is compared against ball centres, which sit a radius higher.
            _laserGrid.Fit(
                CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF,
                CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF,
                CEILING_DEATH_Y - Constants.HALF);

            //One line per level load, in the manner of [camera]: where the field ended up against the death
            //line, and how much air the layout's lowest ball starts with — the figure an author sizing a
            //deep map actually wants, and the record of whether the raise above fired.
            float lowestBallY = _map.GetLowestOccupiedLevel() / Constants.SQRT_TWO + _clusterWorldOffset.Y;
            Console.WriteLine($"[field] {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}: top Y {fieldTopY:F2}"
                + (fieldTopY > FIELD_TOP_Y ? " (raised off the line)" : "") + $", floor Y {_clusterWorldOffset.Y:F2}"
                + $", lowest ball Y {lowestBallY:F2} ({lowestBallY - CEILING_DEATH_Y:F2} above the line)");
        }

        /// <summary>
        /// Rebuilds the drawn glass plate at the loaded field's footprint. The mesh and renderer are the
        /// host's — the glass is lit with the rest of the scene — so the host recreates them, and
        /// <see cref="BS3DGame.ApplySkyLighting"/> has to run after this, exactly as it does after the
        /// Testbed's <c>FitCeilingToMap</c>.
        /// </summary>
        private void FitCeilingToMap() => Game.RebuildCeilingRenderer(_map.StageSizeX, _map.StageSizeZ);

        /// <summary>
        /// Mirrors the loaded lattice into Bepu bodies, which is what the frame actually draws, and wires up
        /// the contact handler that catches a shot landing on it.
        /// </summary>
        private void BuildCluster()
        {
            //The lattice mirrored into bodies, one per occupied cell, constrained to its neighbours and — on
            //the top level — to the ceiling. The offset is what creates them where the cluster is drawn: a
            //BallsMap reckons in its own grid frame, whose level 0 sits at y = 0 however deep the field is,
            //and this game hangs that frame where FitFieldToMap decided (and, for an odd field, half a cell
            //across — see _clusterWorldOffset). The bodies have to be in world coordinates because everything
            //else the simulation
            //touches is — the floor, the ceiling, the muzzle a shot leaves from, the kill plane. It is
            //applied to the body positions and to nothing else: the constraint anchors are differences of two
            //grid positions, so the offset cancels out of them.
            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(
                _map.GetStaticBallsArray(), _world.Simulation, _ceiling.BodyReference,
                _clusterWorldOffset.ToNumerics());

            //The profile's backing array: one slot per cell, so the worst case (every cell occupied) is covered
            //without a resize. Grown only if the field grew, so a level of the same size reuses the same array.
            //
            //Plus headroom for the balls in the AIR, which are not in the field's cells: a released group has
            //left the cluster array by the time it is falling, so it is only the shots that can push past the
            //cell count, and the kill plane culls those a beat after they miss. The headroom is far past what
            //the fire rate can hold in flight at once, and BuildClusterProfile guards the write regardless —
            //this is what keeps the guard from ever firing, not what makes overrunning safe.
            int cellCount = _map.StageSizeX * _map.StageSizeZ * _map.Levels + PROFILE_FLIGHT_HEADROOM;
            if (_profileBalls == null || _profileBalls.Length < cellCount) _profileBalls = new PlayHud.BallMarker[cellCount];

            //What happens on a hit lives in the handler: the snap into the lattice, the constraints, and the
            //match rule. It gets the very list instances the frame draws from, and the same offset, so it can
            //take a world contact down into the grid frame to ask the map about it and bring the answer back up.
            _eventHandler = new BallContactEventHandler(_world.Simulation, _world.Events, _ceiling, _map,
                _physicsBalls, _shotBalls, _fallingBalls, _clusterWorldOffset);

            //The handler reports what a shot did; what it is worth is the scorer's business. Subscribed on the
            //handler the level just built, and the handler is rebuilt with it, so there is nothing to unhook.
            //Both go through a method that reads _score rather than binding the instance the field happens to
            //hold now: the scorer is replaced per level, and a handler holding a stale one would score into a
            //keeper nothing reads.
            _eventHandler.BallLanded += OnBallLanded;
            _eventHandler.ShotSpent += OnShotSpent;

            Console.WriteLine($"[game] {_map.GetBallsCount()} balls in the cluster, "
                + $"{_world.Simulation.Solver.CountConstraints()} constraints");

            //The frame has just become as expensive as it gets, so whatever the probe settled on against the
            //front end is a verdict about a different picture — see BS3DGame.ReopenQualityProbe. Per level and
            //not per session: the set runs from 225 balls to 959, so the cheap ones may hold a tier the heavy
            //ones cannot, and the answer is only ever "this level, this machine, this back buffer".
            Game.ReopenQualityProbe();
        }

        #endregion
    }
}
