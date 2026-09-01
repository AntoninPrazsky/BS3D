using BS3D.Effects;
using BepuPhysics;
using Microsoft.Xna.Framework;
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
    /// <b>The level's rules and its end</b> — what a landing does, when a level is cleared or lost, the ball
    /// census the magazine is loaded from, and the result the player is shown.
    /// </summary>
    /// <remarks>
    /// The order these are asked in is the whole of their correctness, and every piece of it was earned:
    /// <see cref="CheckLevelCleared"/> is tested only on a landing; the drop cinematic is triggered before it;
    /// <see cref="CheckLevelLost"/>'s budget test waits until no shot is undecided, or the last ball of a
    /// budget loses the level it was about to win; and <see cref="FinishLevel"/> runs last in
    /// <c>Update</c> because it may tear the session down under everything else. Split out of
    /// <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region The level's rules and its end

        /// <summary>
        /// A shot has landed in the lattice, having cut <paramref name="released"/> loose. Zero of both means
        /// it stuck without completing a group, which the scorer treats as a spent shot.
        /// </summary>
        private void OnBallLanded(BallLanding landing)
        {
            //Not once the page is up. The simulation goes on running under it (#241), so a shot that was
            //still in the air when the level ended reaches this method for real — and everything below it is
            //the level talking to a player who has stopped playing: a thunk under the fanfare, an award flown
            //into a HUD that is no longer drawn, a keeper moved after LevelResult was taken off it, and on a
            //cleared field a CheckLevelCleared that would start the whole celebration a second time (the beat
            //ends by zeroing its own countdown, so LevelDecided reads this as undecided without LevelOver).
            //The ball still sticks — the handler attached it before saying so, and a ball vanishing in front
            //of the player is the fault RemoveFallenBalls exists to avoid — it simply sticks in silence.
            if (LevelOver) return;

            //The landing's own sound, before anything is scored: it depends only on the colour that hit, what it
            //is MADE of (#314) and where — not on what came loose. Spoken from the cell it stuck to — the same
            //solved position the award is born on below — so a hit on the left of the field is heard on the
            //left. The camera is no longer passed: the listener is the host's, posed once a frame from the one
            //camera there is. The style is this session's rather than the landing's, the material being a
            //property of the level and not of the ball that just arrived.
            Game.Audio.PlayLanded(landing.Type, _ballStyle, landing.World);

            //What came loose answers separately (#46): the lattice's snap and the freed group popping away,
            //scaled by how much of it there is. A plain attach stays just the thunk above. It sounds from the
            //cell that broke and stays there rather than following the group down — see PlayRelease.
            int released = landing.Released.Matched + landing.Released.Orphaned;
            if (released > 0) Game.Audio.PlayRelease(landing.World, released);

            ScoreAward award = _score.Landed(landing.Released.Matched, landing.Released.Orphaned);

            //What the shot was worth, born on the cell it landed in and flown into the corner from there. The
            //type is the colour of the group it completed — a match is by definition three of one colour
            //touching — and is what the number is tinted with on the way; see PlayHud.
            if (award.Scored) _hud.AddAward(landing.World, award, landing.Type);

            //The light runs out through the cluster from where the ball hit. Started AFTER the release above,
            //so the wave walks the cluster that is left rather than the one that was: it goes around the hole
            //a matched group has just left, which is most of what makes it read as travelling through the
            //balls rather than as a sphere expanding through space.
            StartRipple(landing.Cell);

            //The cluster just changed — a ball joined it, and a group may have left. Recount before anything
            //asks what may be loaded, and re-colour whatever is already in the barrel and has just gone dead.
            //
            //Not once the level is decided (#176). The census is only ever read to answer what may be loaded
            //next, and nothing loads any more, so all that would still run is the transmute's deliberately
            //visible dissolve — the queue re-colouring itself in front of a player who has already won. A
            //landing can still arrive here that late even with the gun shut (#177): a shot fired before the
            //field emptied may still be in the air when it does, and an empty field's own ceiling will take it.
            if (!LevelDecided)
            {
                RecountBallTypes();

                //And nothing to transmute TO is the winning landing itself, which LevelDecided cannot see: the
                //release has already emptied the map, but CheckLevelCleared only arms the countdown at the foot
                //of this method. Measured on a one-colour test level, every clear re-coloured three to five of
                //the five loaded slots to a default the instant the field went — the case #176 is reached
                //through on EVERY clear, not only on the late landing its report describes. Not a special case for
                //the ending either: a queue cannot be re-coloured towards a live colour when none is left, and
                //RandomBallType's own empty-cluster branch is the one that would answer here.
                if (AnyBallTypeAlive()) Transmute();
            }

            //Before the clear test, because a shot that empties the field is the one most worth watching and
            //CheckLevelCleared starts the countdown that ends the level
            TryBeginDropCinematic(landing.Released);

            //And the cluster has just changed, which is the only thing that can put a tall level's underside
            //out of reach
            FeedTallColumn();

            CheckLevelCleared();
        }

        /// <summary>
        /// Hands the camera to <see cref="DropCinematic"/> if this shot cut enough loose to be worth
        /// watching. The subject is the balls that were just released: <c>ReleaseSameTypeCluster</c> appends
        /// them to <see cref="_fallingBalls"/>, so they are that list's last
        /// <c>Matched + Orphaned</c> entries at this instant and nothing else has run in between.
        /// <para>
        /// They are held by <b>body handle</b> rather than by index, because the kill plane removes them from
        /// the list one by one as they go. Bepu recycles a handle once its body is gone, which would let a
        /// later ball inherit a dead subject's identity — harmless here and only here, because nothing new is
        /// added to the simulation while a cinematic runs: the gun's controls are locked, so there is no shot
        /// to land and therefore no further release.
        /// </para>
        /// </summary>
        private void TryBeginDropCinematic(BallsReleased released)
        {
            int total = released.Matched + released.Orphaned;

            //Big enough to be a spectacle at all, and bigger than anything this level has already shown the
            //player — see DropCinematic.MustBeatBestBy for why the second half cannot be a fixed count.
            bool worthWatching = total >= DropCinematic.MIN_BALLS
                                 && total >= _biggestDrop * DropCinematic.MustBeatBestBy;

            //Raised by every release, including the ones refused below: a collapse the player watched happen
            //has moved what "big" means here, whether or not the camera went with it.
            int previousBest = _biggestDrop;
            if (total > _biggestDrop) _biggestDrop = total;

            if (!worthWatching) return;

            //Never over a camera takeover already running, and never over the end of a level: the result
            //screen is about to cover this one, and a camera move under it is a move nobody sees.
            if (CameraTakeoverEngaged || LevelDecided) return;

            int first = _fallingBalls.Count - total;
            if (first < 0) return;

            _cinematicSubject.Clear();

            Vector3 centre = Vector3.Zero;

            for (int i = first; i < _fallingBalls.Count; i++)
            {
                BodyReference body = _fallingBalls[i].BallReference;

                _cinematicSubject.Add(body.Handle.Value);

                centre += body.Pose.Position.ToXna();
            }

            centre /= total;

            _cinematic.Begin(Game.Scene, centre, Camera.Position, total, RANDOM, Game.SeaLevelY);

            //One line per cinematic, in the manner of the [level] and [score] lines: it is a rare event, not a
            //per-frame one, and the shot is rolled — so when one frames badly this is the only record of what
            //it actually chose. It carries the bar it beat as well, because "why did that one fire and not the
            //last one" is now a question about the level's history rather than about a constant.
            Console.WriteLine($"[cinematic] {total} balls ({released.Matched} matched, {released.Orphaned} orphaned)"
                + $" beat a best of {previousBest}, from y={centre.Y:F1}, {_cinematic.Describe()}");
        }

        /// <summary>
        /// Hands the camera to <see cref="ChapterIntro"/> when the level just built is the first index of a
        /// <b>new</b> block this run of the program has not already toured (#267). Called from
        /// <see cref="BuildLevel"/>, after the field, the cannon and the game camera are all fit to it — the
        /// intro orbits the very point the ordinary camera is about to look at, so it needs that solve to
        /// have already run.
        /// <para>
        /// A set with no blocks at all (<see cref="LevelSet.HasBlocks"/> false) never reaches here:
        /// <see cref="LevelSet.BlockRange"/>'s "an entry naming no block is its own run of one" fallback would
        /// otherwise call every single level the first index of its own block and open an intro on every one
        /// of them — the same trap the block-complete milestone (#184) is gated against for the same reason.
        /// </para>
        /// </summary>
        private void TryBeginChapterIntro()
        {
            LevelSet set = Game.LevelSet;
            if (set == null || !set.HasBlocks) return;

            set.BlockRange(_levelIndex, out int first, out _);
            if (first != _levelIndex) return;

            //Add answers false when the key is already in the set — one call is both the test and the record,
            //so there is no window between asking and marking where a re-entrant BuildLevel could see stale
            //state. Never removed: a fresh launch of the program is the only thing that shows a chapter's
            //opening twice, and that is the point (see the field's own remarks).
            if (!_chapterIntroShown.Add(first)) return;

            Vector3 centre = new(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z);

            //The ordinary gameplay pose, verbatim: the tour's last key is it, so the flight lands where the
            //player is handed the camera and the blend-out is a nudge between identical poses.
            _chapterIntro.Begin(centre, _gameCameraDistance, GAME_FOV,
                GameCameraPositionAt(_gameCameraDistance), centre, RANDOM);

            //One line per intro, in the manner of [cinematic]: a rare event — nine times over the whole
            //campaign — and the shot is rolled, so this is the only record of what it actually chose.
            Console.WriteLine($"[intro] block '{set.BlockName(_levelIndex)}' ({set.BlockNumber(_levelIndex)}/{set.BlockCount}), "
                + _chapterIntro.Describe());
        }

        /// <summary>
        /// Where the released group is now, averaged over the ones the kill plane has not taken yet. False
        /// once the last of them is gone, which is what ends the cinematic.
        /// </summary>
        private bool TryGetDropCentre(out Vector3 centre)
        {
            centre = Vector3.Zero;

            if (_cinematicSubject.Count == 0) return false;

            int found = 0;

            for (int i = 0; i < _fallingBalls.Count; i++)
            {
                PhysicsBall ball = _fallingBalls[i];
                if (!_cinematicSubject.Contains(ball.BallReference.Handle.Value)) continue;

                //The pose the frame DRAWS, not the raw body: under slow motion the bodies advance only every
                //few frames, and a camera fed the raw staircase inherits it — the balls' half of #293 fixed,
                //the lens would still judder off this very read.
                ball.InterpolatedPose(_renderAlpha, out System.Numerics.Vector3 position, out _);

                centre += position.ToXna();
                found++;
            }

            if (found == 0) return false;

            centre /= found;
            return true;
        }

        /// <summary>
        /// A shot is over without having landed. The streak breaks — unless the level is already over, in
        /// which case there is no streak to break: the figures the result page shows were taken off the
        /// keeper when the level ended, and the simulation running under that page (#241) can still spend a
        /// shot against the stone or drop one past the kill plane. See <see cref="LevelOver"/>.
        /// </summary>
        private void OnShotSpent()
        {
            if (LevelOver) return;

            _score.Missed();
        }

        /// <summary>
        /// Has the field just been emptied? That is the goal of a level: release every ball so it falls.
        /// <para>
        /// Tested only here, on a landing, which is the one thing that can empty the field — and testing it
        /// anywhere else would be worse than redundant. Polling it per frame would declare a level authored
        /// with no balls won before the player had fired a shot, and would keep declaring it.
        /// </para>
        /// <para>
        /// The completion bonus is awarded <b>now</b> rather than when the level actually ends, so nothing
        /// happening during the pause below can eat the balls the player finished with. Since #177 no shot can
        /// be fired into that pause at all — <see cref="Shoot"/> refuses the moment the level is decided — so
        /// the two agree rather than this one carrying the whole weight. A shot still in flight at this moment
        /// is harmless to the count either way: it was spent when it left the barrel.
        /// </para>
        /// </summary>
        private void CheckLevelCleared()
        {
            if (LevelDecided || _map.GetBallsCount() > 0) return;

            int bonus = _score.AwardCompletionBonus();
            _clearedCountdown = LEVEL_CLEARED_BEAT;

            //WHETHER THIS CLEAR FINISHES A BLOCK, decided ONCE and here (#184). Here because the celebration
            //starts here and the result page arrives LEVEL_CLEARED_BEAT later, so a decision taken on the page
            //would reach the fireworks and the fanfare too late to change either. Once because all three have to
            //agree: a page that says a chapter is finished over an ordinary barrage and an ordinary fanfare is
            //three components disagreeing about what just happened.
            //
            //Asked before the record is written (that happens in ShowResultScreen), so it is "would this clear
            //complete it" rather than "is it complete" — and it is false on a replay of a block already finished,
            //which is an ordinary clear because that is what it is.
            _blockCompleted = Game.WouldCompleteBlock(_levelIndex);

            //AND WHETHER IT FINISHES THE CAMPAIGN (#215), here for the identical reason and expressed once so
            //that the page cannot answer a different question from the confetti. "Complete" only when there
            //actually was a campaign — a set of more than one level cleared to its end — because a single-level
            //set is just a level cleared, and calling that the end of a campaign overstates it.
            //
            //Cheap and knowable this early: unlike the block, which has to look at every level of a run, this
            //is the last entry of the set, and this clear is the clear.
            _campaignCompleted = Game.LevelSet != null && Game.LevelSet.Count > 1
                                 && _levelIndex + 1 >= Game.LevelSet.Count;

            //The party. Started here rather than when the result screen appears, so the first shells are
            //already climbing while the last of the cluster is still falling — the celebration overlaps the
            //moment it is celebrating instead of following it. It runs on the host, so it carries on over the
            //result screen and the released camera swings through it (see Fireworks).
            //
            //A finished block takes a LONGER OPENING BARRAGE rather than a longer display: the density is already
            //at its maximum in the opening, and that is the part the player is watching — by the time it eases
            //off they are reading their score. See Fireworks.Celebrate's own remarks.
            Game.Fireworks?.Celebrate(CELEBRATION_SECONDS, CELEBRATION_DELAY,
                _blockCompleted ? BLOCK_CELEBRATION_OPENING : 0f);

            //And the confetti, but only for the campaign's own ending (#215). This is the one beat that is not
            //a dial on the display: #184 had already established that a celebration is made to read as BIGGER
            //by lengthening its opening, and by the time a block completes that dial is at eight seconds of an
            //already-maximum density — there is nothing above it. So the last ending gets a different KIND of
            //thing rather than more of the same, and it runs alongside the fireworks rather than instead of
            //them. No delay of its own: the fanfare's opening statement is already protected by the display's,
            //and paper starting to fall is not a sound.
            if (_campaignCompleted) Game.Confetti?.Celebrate(CONFETTI_SECONDS);

            //And the theme stops dead, so the reports land in silence. A bang competing with a four-to-the-
            //floor kick is a bang nobody hears, and the sudden quiet is itself part of winning.
            Game.Music?.Stop();

            //Into that silence, the fanfare — scaled by the score, so how big a win it was is audible before
            //the result screen has said a word. It is a separate instance from the theme, so stopping the one
            //above does not cut this off. A finished block takes it at full intensity whatever the last level
            //scored, because the milestone is the chapter and not that level.
            Game.Music?.PlayVictory(_score.Score, grand: _blockCompleted);

            //The bonus has no popup to fly in and land on the readout, so it would otherwise be the one award
            //the score takes without being hit — it counts up out of nowhere while the collapse plays
            if (bonus > 0) _hud.FlashScore();

            Console.WriteLine($"[level] Cleared '{LevelName(_levelIndex)}' with {_score.Score}"
                + $" (+{bonus} for {_score.ShotsRemaining?.ToString() ?? "unlimited"} unused)"
                + $", {StarRating.Rate(_score.Score, _initialBallCount)} star(s)"
                //The milestone, on the line that already reports the clear. It is the only way a play-through
                //says whether the block fired, since the decision is invisible until the page arrives — and it
                //names the block either way, so a milestone that did NOT fire says which chapter is still open.
                + (Game.CampaignHasBlocks
                    ? $" [block {Game.LevelBlockNumber(_levelIndex)}/{Game.BlockCount}"
                      + $" '{Game.LevelBlockName(_levelIndex)}'{(_blockCompleted ? " COMPLETE" : string.Empty)}]"
                    : string.Empty));
        }

        /// <summary>
        /// Has the level been lost? The two pressures that lose it — a spent budget with the field uncleared, and
        /// the ceiling reaching the death line — are decided here, after the physics step, once every shot in
        /// flight has resolved. Either one alone loses; both are checked because either can be the one a last shot
        /// earned.
        /// </summary>
        /// <remarks>
        /// The spent budget is tested last — after a possible clear this same frame has run. The last ball of a
        /// budget may be the one that empties the field, and a loss called before <see cref="OnBallLanded"/> had
        /// its say would steal that win. So the budget only loses when nothing is in flight and the field is still
        /// standing. The ceiling, by contrast, does not wait on a landing at all — a descent can push a ball past
        /// the line between landings — but it is no longer decided on a single frame's pose either: see
        /// <c>CLUSTER_SWING_ALLOWANCE</c> and <c>CLUSTER_BELOW_LINE_GRACE</c> (#239), which is what stops a
        /// cluster that merely swings from ending a level it is nowhere near losing.
        /// </remarks>
        /// <param name="elapsed">
        /// REAL seconds this frame, the same figure the quality probe is fed — not the simulation's scaled step.
        /// A swing takes the time it takes whatever the world is doing, so the grace it is measured against has
        /// to be wall time.
        /// </param>
        /// <param name="mayLose">
        /// False while a drop cinematic runs: the walk and the floor alarm still evaluate on this frame's
        /// poses — the warning is the cluster's own geometry, not part of the spectacle the cinematic holds —
        /// but neither ending may be declared until the collapse the player earned has been seen.
        /// </param>
        //The grace's own state - ClusterLineWatch's since #301/#302, so the level generator's sag gate decides
        //a simulated run by running THIS rule rather than a second copy of it that could drift lenient. It
        //needs no reset when a level starts, exactly as the bare float it replaces did not: every level begins
        //with its cluster far above the line, so the first frame zeroes it.
        private ClusterLineWatch _lineWatch;

        private void CheckLevelLost(float elapsed, bool mayLose)
        {
            //Already ending — a cleared countdown or a loss in flight. Testing further would re-trigger a loss
            //on top of a clear or a teardown already underway.
            if (LevelDecided) return;

            //The ceiling reaching the death line. Live poses are in _physicsBalls (the lattice in _map holds
            //cells, not bodies); the loop mirrors the draw's own walk over the structure (ClusterCollector),
            //including the null check for cells a release has emptied. It tracks the minimum rather than
            //stopping at the first offender, because the floor alarm below wants the cluster's true lowest
            //point on every frame, not only the losing one — same walk, no second scan.
            XZLevel size = XZLevel.FromArray(_physicsBalls);
            float lowestBallY = float.MaxValue;

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        float y = ball.BallReference.Pose.Position.Y;
                        if (y < lowestBallY) lowestBallY = y;
                    }

            //Before the loss test, so the frame that loses also lights the net the loss was promised on —
            //the result screen then schedules its linger-and-fade.
            UpdateLaserWarning(lowestBallY);

            //A cinematic defers the endings, never the warning above: the walk already ran on this frame's
            //poses, and both losses will be re-asked the moment the cinematic lets go.
            if (!mayLose) return;

            //The line's verdict, and the message is built only inside the branch that lost - the walk above
            //runs every frame and formatting one there would allocate on the gameplay path.
            switch (_lineWatch.Update(lowestBallY, elapsed))
            {
                //Deeper than any swing measured on the heaviest cluster in the pack reaches, so there is
                //nothing to wait for: the descent has genuinely put a ball under, and holding the verdict for
                //a second would only be a second of watching a lost level.
                case ClusterLineVerdict.PastAllowance:
                    LoseLevel(LevelFailure.ClusterReachedLine,
                        $"a ball at {lowestBallY:F2} <= {CEILING_DEATH_Y - CLUSTER_SWING_ALLOWANCE:F2}"
                        + $" ({CLUSTER_SWING_ALLOWANCE:F2} past the line, deeper than a swing goes)");
                    return;

                //Otherwise the line had to be HELD rather than merely touched.
                case ClusterLineVerdict.HeldTooLong:
                    LoseLevel(LevelFailure.ClusterReachedLine,
                        $"a ball at {lowestBallY:F2} <= {CEILING_DEATH_Y:F2} held for"
                        + $" {_lineWatch.BelowLineSeconds:F2} s (grace {CLUSTER_BELOW_LINE_GRACE:F2} s)");
                    return;
            }

            //The budget spent with the field uncleared — but only once every shot has RESOLVED, so the last ball
            //fired has had its chance to clear. A ball still in flight could be that chance, and a loss called
            //beneath it would steal the win. "Resolved" is the load-bearing word: see AnyShotUndecided.
            if (_score.OutOfShots && !AnyShotUndecided() && _map.GetBallsCount() > 0)
                LoseLevel(LevelFailure.OutOfBalls,
                    $"budget {LevelShotBudget(_levelIndex)?.ToString() ?? "unlimited"}, fired {_score.ShotsFired}"
                    + $", {_shotBalls.Count} spent ball(s) not yet culled");
        }

        /// <summary>
        /// Arms or stands down the floor alarm from the cluster's lowest live ball: on when
        /// <see cref="LASER_WARN_STEPS"/> more ceiling steps would push it past the death line — the very
        /// comparison <see cref="CheckLevelLost"/> loses on, two descents early — and off with a little
        /// hysteresis on the way back up. An empty field's <c>MaxValue</c> stands it down for free. The
        /// <c>lasers</c> command-line flag pins it on, so the net can be screenshotted without playing a
        /// level to the brink — the <c>celebrate</c> reasoning, for a session-owned effect.
        /// <para>
        /// Evaluated on every frame of a built session, drop cinematic included — the cinematic holds the
        /// level's <i>endings</i>, not this: the release that engages one is exactly the release that
        /// rescues a low cluster, and a warning frozen lit would outstay the danger by the whole shot.
        /// </para>
        /// </summary>
        private void UpdateLaserWarning(float lowestBallY)
        {
            float threshold = CEILING_DEATH_Y + LASER_WARN_STEPS * CEILING_DESCENT_PER_STEP;
            if (_laserGrid.Visible) threshold += LASER_WARN_HYSTERESIS;

            _laserGrid.SetVisible(Game.ForceLaserWarning || lowestBallY <= threshold, WallClock);
        }

        /// <summary>
        /// Whether any shot is still <b>undecided</b> — in flight, and so still able to reach the cluster and
        /// clear the field.
        /// <para>
        /// Deliberately not <c>_shotBalls.Count == 0</c>, which was the bug behind #66. That list holds shot
        /// <i>bodies</i>, and a ball is taken out of it only when it attaches or is culled — so a ball that
        /// comes to rest on the island's stone ring stays in it for the rest of the session, since the game does
        /// not sleep-cull (see <see cref="RemoveFallenBalls"/> for why not). One such ball parked there made the
        /// out-of-balls loss unreachable for ever: the HUD read 0 balls left and no result screen ever came up.
        /// It bit on the authored 30-shot level and not on a 3-shot test set, because the smaller the budget the
        /// less chance there is of a shot having settled on the stone by the time it runs out.
        /// </para>
        /// <para>
        /// The quantity wanted is already maintained. A ball stops being a contact listener at exactly the
        /// moment its shot resolves — on attaching, on touching anything static or kinematic, or on being culled
        /// — and <see cref="Shoot"/> is the only place anything is ever registered, so every listener is a shot
        /// still in the air. A counter of its own would be a fourth place to keep in step with those three.
        /// </para>
        /// </summary>
        private bool AnyShotUndecided()
        {
            //An indexed walk over at most a handful of balls, like the handler's own FindShotBall, and IsListener
            //is a set lookup — this runs per frame, so neither LINQ nor an allocation belongs here.
            for (int i = 0; i < _shotBalls.Count; i++)
            {
                BodyReference body = _shotBalls[i].BallReference;

                //Awake as well as listening, which closes the one case the listener flag alone does not: a ball
                //that comes to rest supported by another LOOSE shot ball has touched nothing static or kinematic
                //and never attached, so nothing ever unregistered it (the handler returns without unregistering
                //when the other body is neither structure nor ceiling) — yet a sleeping ball is plainly not
                //going to reach the cluster. A ball in flight is always awake, so this can never hide a live shot.
                if (_world.Events.IsListener(body.CollidableReference) && body.Awake) return true;
            }

            return false;
        }

        /// <summary>
        /// Ends the level as a loss for the stated reason. It does <b>not</b> tear the session down here — a loss
        /// can be reached from the middle of <see cref="Update"/> (a shot that spends the budget, a frame that
        /// slides the ceiling past the line), and rebuilding mid-frame would leave the rest of the frame running
        /// against a simulation that no longer exists. Instead it sets the outcome and hands the player the result
        /// screen, whose Retry button does the real reload — the same screen a cleared level lands on.
        /// </summary>
        /// <param name="diagnostic">
        /// The figures behind the loss. <b>Logged and never shown</b>: what a player needs is which limit ran
        /// out, and a world-space Y against a death line tells them nothing they can act on.
        /// </param>
        private void LoseLevel(LevelFailure failure, string diagnostic)
        {
            //Once only: a descent and a budget can reach their lines on the same frame, and a loss in flight
            //must not stack a second screen onto the first.
            if (_levelLost) return;
            _levelLost = true;

            Console.WriteLine($"[level] Lost '{LevelName(_levelIndex)}': {failure} ({diagnostic}), score {_score.Score}");

            //The music goes with the level, win or lose. A dance track carrying on cheerfully over a result
            //screen that says the player ran out of balls is the wrong feeling entirely.
            Game.Music?.Stop();

            //The same scaling from the other end: a good score that still lost gets a fuller, more dignified
            //piece and a poor one gets three thin notes that do not resolve. Losing narrowly and losing badly
            //should not sound the same.
            Game.Music?.PlayDefeat(_score.Score);

            _pendingOutcome = LevelOutcome.Failed;
            _pendingFailure = failure;
            ShowResultScreen();
        }

        /// <summary>
        /// What the player is told about a loss. The two limits carry no figures at all — a world-space Y
        /// or a budget they already watched run down tells them nothing they can act on.
        /// </summary>
        private static string FailureText(LevelFailure failure) => failure switch
        {
            LevelFailure.OutOfBalls => "You ran out of balls.",
            LevelFailure.ClusterReachedLine => "The cluster reached the line.",
            _ => string.Empty,
        };

        /// <summary>
        /// The level is over and the collapse has played out. It does <b>not</b> act on the outcome — it decides
        /// which one it was and hands the player the result screen to choose what to do about it. The build,
        /// the teardown and the advance live behind the result screen's buttons
        /// (<see cref="BS3DGame.RetryLevel"/>, <see cref="BS3DGame.AdvanceLevel"/>), which is what a player
        /// actually presses.
        /// <para>
        /// A cleared field is the only thing that reaches here today; a lost one reaches the same screen through
        /// <see cref="LoseLevel"/>. Both set the outcome and call <see cref="ShowResultScreen"/>, and the screen
        /// does the rest.
        /// </para>
        /// </summary>
        private void FinishLevel()
        {
            //Clearing the field IS passing the level, without a further test: the per-level score gate this
            //used to weigh (ShortOfGate) is retired with #111 — a clear always rates at least one star, and
            //whether the NEXT level opens is the star total's question, answered on the result screen rather
            //than by failing a level the player just watched themselves win.
            _pendingOutcome = LevelOutcome.Cleared;
            _pendingFailure = LevelFailure.None;

            ShowResultScreen();
        }

        /// <summary>
        /// Puts the result screen over the level that has just ended. The screen is a push over this one,
        /// exactly as a pause is — but where a pause stops what is underneath, this page leaves its
        /// <c>UpdatesUnderlying</c> true and the arena goes on living behind the numbers (#241, and
        /// <see cref="UpdateUnderResult"/> for what "living" is allowed to mean). Called when a level ends —
        /// see <see cref="FinishLevel"/> and <see cref="LoseLevel"/>.
        /// </summary>
        private void ShowResultScreen()
        {
            //The floor alarm has said its piece: from the moment the ending is actually put in front of the
            //player, a standing net keeps pulsing under the page for a moment and then goes out. Stamped here,
            //the one funnel both endings come through, and not back where the level logically ended — a clear's
            //page arrives LEVEL_CLEARED_BEAT after the field empties, plus a whole cinematic when one is
            //running (the countdown freezes for it), and a linger stamped at the clear was spent before anyone
            //saw it. Wall-clock stamped, because nothing steps the net once the page is up: the frame that
            //runs under it is the world's, and the alarm is a rule of the level (see LaserGrid).
            _laserGrid.NoticeLevelEnded(WallClock);

            //Smears age in Update and draw in Draw, and the frame under the page ages neither — so one still
            //mid-fade would hang at a fixed alpha for as long as the result page is up, while the page's own
            //camera turns around it. Dropped rather than converted to wall clock: a level that has ended has
            //no shot worth still showing.
            _smears.Clear();

            //The figures are handed over as a SNAPSHOT taken now, not read by the screen when it draws. The
            //level does not stop the instant it is cleared — the collapse is held for a beat and a player who
            //keeps firing moves the balls remaining — so a screen that re-read the keeper printed a row that
            //did not add up to the total above it. See LevelResult.
            bool cleared = _pendingOutcome == LevelOutcome.Cleared;
            bool lastEntry = Game.LevelSet == null || _levelIndex + 1 >= Game.LevelSet.Count;

            //The rating and the record, at the one funnel both endings come through. Recorded BEFORE the
            //unlock below is read, so the stars this clear just earned already count towards the next
            //level's gate — a clear that pushes the total over it unlocks Next Level on this very screen.
            int stars = cleared ? StarRating.Rate(_score.Score, _initialBallCount) : 0;
            bool newBest = cleared && Game.RecordLevelResult(_levelIndex, _score.Score, stars);

            Game.PresentResult(new LevelResult(
                cleared: cleared,
                failureText: cleared ? null : FailureText(_pendingFailure),
                stars: stars,
                newBest: newBest,

                //Which level this was (#313). LevelName is the same helper the [level] line below prints
                //through, so the screen and the log cannot disagree; the number is the entry's own 1-based
                //place, which is what the picker's tiles and the window title both show. Zero off a set,
                //where there is no entry to number and LevelResult prints the name alone.
                levelName: LevelName(_levelIndex),
                levelNumber: Game.LevelSet != null ? _levelIndex + 1 : 0,

                hasNextLevel: !lastEntry,
                nextLevelUnlocked: !lastEntry && Game.IsLevelUnlocked(_levelIndex + 1),
                nextLevelMinStars: lastEntry ? 0 : Game.LevelMinStars(_levelIndex + 1),
                totalStars: Game.TotalStars,

                //And which one is next, for the button that offers it. Filled whenever there IS a next entry,
                //gate or no gate — see LevelResult: a locked next level still has a name worth wanting.
                nextLevelName: lastEntry ? null : LevelName(_levelIndex + 1),

                //"Campaign complete", read off the decision CheckLevelCleared already took rather than derived
                //again (#215) — the confetti has been falling on it for a beat by now, and the rule that only a
                //set of more than one level cleared to its end counts as a campaign lives there, once. Still
                //gated on `cleared` here because that decision is only ever taken on a clear: a LOSS on the last
                //level reaches this line with the flag from no clear at all, and it must not say the campaign is
                //over. (Which is also why it is cleared with the rest of a level's state in BuildLevel.)
                campaignComplete: cleared && _campaignCompleted,

                //The block milestone (#184), read off the decision CheckLevelCleared already took rather than
                //asked again: the record above has been written by now, so re-asking would answer a different
                //question — and the fireworks and the fanfare have been running on that decision for a beat
                //already. LevelResult itself suppresses it when the campaign completes.
                blockComplete: _blockCompleted,
                blockName: Game.LevelBlockName(_levelIndex),
                blockNumber: Game.LevelBlockNumber(_levelIndex),
                blockCount: Game.BlockCount,

                score: _score.Score,
                matchedBalls: _score.MatchedBalls,
                orphanedBalls: _score.OrphanedBalls,
                streakBonus: _score.StreakBonus,
                hadBudget: _score.ShotsRemaining.HasValue,
                unusedShotsAwarded: _score.UnusedShotsAwarded,
                completionBonusAwarded: _score.CompletionBonusAwarded));

            Console.WriteLine($"[level] Result for '{LevelName(_levelIndex)}': {_pendingOutcome}" + (_pendingOutcome == LevelOutcome.Failed ? $" ({_pendingFailure})" : "")
                + $", score {_score.Score}"
                + (cleared ? $", {stars} star(s){(newBest ? ", new best" : "")}, {Game.TotalStars} total" : ""));
        }

        /// <summary>What to call the level at <paramref name="index"/>, set or no set.</summary>
        private string LevelName(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.DisplayName(index) : "the built-in level";

        /// <summary>
        /// The ball budget the entry at <paramref name="index"/> grants, or null for unlimited — which is what
        /// an absent <c>shots</c> rule, an index outside the set and a missing set all mean. This is the read
        /// site the nullable rule is documented against: the set records only that a rule is absent, and this
        /// is where the game says what absent means.
        /// </summary>
        private int? LevelShotBudget(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.Levels[index].Shots : null;

        /// <summary>
        /// Shots between two descents of the glass ceiling, or null for a ceiling that holds still — which is what
        /// an absent <c>ceilingStep</c> rule, an index outside the set and a missing set all mean. Mirrors
        /// <see cref="LevelShotBudget"/>: the nullable rule is read at one site, and this is where absent is given
        /// its meaning.
        /// </summary>
        private int? LevelCeilingStep(int index) =>
            Game.LevelSet != null && index >= 0 && index < Game.LevelSet.Count ? Game.LevelSet.Levels[index].CeilingStep : null;

        /// <summary>
        /// Recounts how many balls of each colour are still hanging. The magazine may only load a colour whose
        /// count is above zero: a ball of a colour that exists nowhere in the cluster can never match anything,
        /// so it can only be parked somewhere — which grows the very cluster the player is shrinking, wastes a
        /// budgeted shot, and in the limit makes a level unwinnable.
        /// <para>
        /// A colour with fewer than <see cref="BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE"/> left is arguably
        /// already dead weight and could be dropped from the queue early. It is deliberately <b>not</b>: that
        /// changes which levels are solvable at all, which makes it a difficulty decision rather than this fix.
        /// </para>
        /// </summary>
        private void RecountBallTypes()
        {
            for (int i = 0; i < _ballsOfType.Length; i++) _ballsOfType[i] = 0;

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = XZLevel.FromArray(balls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int index = (int)ball.Type - 1;
                        if (index >= 0 && index < _ballsOfType.Length) _ballsOfType[index]++;
                    }
        }

        /// <summary>
        /// Re-colours every loaded ball whose colour has just been eliminated from the cluster, and starts the
        /// dissolve that shows it happening.
        /// <para>
        /// The alternative — letting a stale queue play out — costs the player up to <see cref="Magazine.SIZE"/>
        /// shots on colours that cannot match anything, through no fault of their own. This is a game and not a
        /// simulation, so a ball that is already loaded may simply be re-coloured; the player will notice, and
        /// noticing is the point, because what they see is the game helping rather than the game cheating them.
        /// </para>
        /// <para>
        /// The colour changes <b>immediately</b> and the dissolve is cosmetic: firing mid-transition must give
        /// the new colour, never the dead one it is still fading out of. The replacement is drawn at random
        /// from what survives — picking whichever colour would help most would quietly make the game easier,
        /// and that is a difficulty decision, not a fix.
        /// </para>
        /// <para>
        /// <see cref="Magazine.Recolour"/> deliberately does <b>not</b> fire the loaded hook the constructor
        /// wired, which is what lets the old colour below stand: a re-coloured ball is precisely the one whose
        /// previous colour the cross-fade has to keep.
        /// </para>
        /// </summary>
        private void Transmute()
        {
            for (int slot = 0; slot < Magazine.SIZE; slot++)
            {
                BallType loaded = _magazine.Peek(slot);

                int index = (int)loaded - 1;
                if (index >= 0 && index < _ballsOfType.Length && _ballsOfType[index] > 0) continue;

                BallType replacement = RandomBallType();
                if (replacement == loaded) continue; //nothing survives to swap to; leave it alone

                //The ball it is fading OUT of is whatever is on screen now — which for a slot caught
                //mid-transmute is the colour it was already fading out of, not the one it never finished
                //becoming. Restarting from the visible colour is what keeps the animation continuous.
                if (_magazineTransmute[slot] <= 0f) _magazineFrom[slot] = loaded;

                Console.WriteLine($"[transmute] slot {slot}: {_magazineFrom[slot]} is gone from the cluster -> {replacement}");

                _magazine.Recolour(slot, replacement);
                _magazineTransmute[slot] = 1f;
            }
        }

        /// <summary>
        /// Whether anything at all is still hanging, off the census <see cref="RecountBallTypes"/> has just
        /// taken — so it costs a walk over the type counters rather than a second walk over the field.
        /// </summary>
        private bool AnyBallTypeAlive()
        {
            for (int i = 0; i < _ballsOfType.Length; i++) if (_ballsOfType[i] > 0) return true;

            return false;
        }

        /// <summary>
        /// What the magazine loads next: one of the colours <b>still hanging</b> (see
        /// <see cref="RecountBallTypes"/>), drawn evenly among them off the unseeded run-to-run generator.
        /// Not a static method by accident — the live set changes with every shot that lands, so it cannot
        /// be static the way it was when the cluster was a fixed pyramid.
        /// </summary>
        private BallType RandomBallType()
        {
            int live = 0;
            for (int i = 0; i < _ballsOfType.Length; i++) if (_ballsOfType[i] > 0) live++;

            //An empty cluster — a level authored with no balls, or one the player has just cleared. There is
            //nothing left to match, so what is loaded cannot matter; the default four keep the barrel full.
            if (live == 0) return DEFAULT_BALL_TYPES[RANDOM.Next(DEFAULT_BALL_TYPES.Length)];

            int pick = RANDOM.Next(live);

            for (int i = 0; i < _ballsOfType.Length; i++)
            {
                if (_ballsOfType[i] <= 0) continue;
                if (pick == 0) return (BallType)(i + 1);

                pick--;
            }

            return DEFAULT_BALL_TYPES[0]; //unreachable: pick < live, and live is the count of the loop's hits
        }

        #endregion
    }
}
