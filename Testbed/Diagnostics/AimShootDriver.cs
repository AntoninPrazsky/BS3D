using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Render;
using System;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// Both halves of the aim testing, which is the one part of the gameplay that is otherwise mouse-driven and
    /// so cannot be checked from a screenshot: <see cref="LogReachability"/> answers <c>aimcheck</c> — can the
    /// gun be pointed at every cell of this map at all — and the instance drives <c>aimshoot</c>, which then
    /// actually fires at them. They are one file because <c>aimshoot</c> <b>implies</b> <c>aimcheck</c>: the
    /// sweep's "want X, got Y" lines are only worth reading against the clamp the check reports.
    /// <para>
    /// The sweep walks the field's centre column bottom to top and finishes on its four top corners, which are
    /// the steepest <i>facing</i> shots there are and therefore the ones that bind
    /// (<see cref="AimReachability"/> holds why a facing shot is the one that has to fit). It stands the carriage
    /// to face each cell first (<see cref="Cannon.OrbitToFace"/>), so every shot is the clean one over open
    /// ground rather than one fired across the whole cluster.
    /// </para>
    /// <para>
    /// <b>Every <c>[aimcheck]</c> and <c>[aimshoot]</c> line here is a contract</b>, not a debug aid:
    /// <c>.claude/skills/verify</c> and <c>docs/testbed.md</c> both document the exact strings, so the wording
    /// moved out of <c>Testbed.cs</c> with #73 unchanged to the character.
    /// </para>
    /// </summary>
    public sealed class AimShootDriver
    {
        private const float RAD_TO_DEG = 180f / MathF.PI;

        private const int COLUMN_STEPS = 6;                 //samples up the centre column, bottom to top
        private const int STEPS = COLUMN_STEPS + 4;         //then the four top corners (the steepest facing shots)
        private const float INTERVAL_SECONDS = 0.6f;        //so shots do not pile up mid-flight

        private readonly Cannon _cannon;

        //The muzzle's distance ahead of the trunnions, which is what the precise-aim lens is placed off. A
        //constant of the built rig (CannonRig derives it from the magazine the bore is sized to), so it is taken
        //once rather than read through a renderer this harness would otherwise have to hold.
        private readonly float _pivotToFrontBall;

        //Captured once at construction rather than written at the call site, which would allocate a delegate per
        //frame (BestPractices.md §3)
        private readonly Action _shoot;

        /// <summary>
        /// The field being swept. Pushed by <c>InstallMap</c> beside the contact handler's own map, for the same
        /// reason: the Testbed swaps maps inside a live session, so a harness that captured one at construction
        /// would go on sweeping a field that is no longer loaded.
        /// </summary>
        public BallsMap Map;

        //-1 until the game-mode entry animation has finished, then 0..STEPS. The sweep cannot start before it:
        //a shot in the overview leaves the camera, not the gun.
        private int _index = -1;
        private float _elapsed;

        public AimShootDriver(Cannon cannon, float pivotToFrontBall, Action shoot)
        {
            _cannon = cannon;
            _pivotToFrontBall = pivotToFrontBall;
            _shoot = shoot;
        }

        /// <summary>
        /// Advances the sweep. One call from the caller's update.
        /// </summary>
        /// <param name="ready">Whether the gun is actually in game mode with no transition animation running —
        /// the caller's own state, and the gate the sweep waits on before its first shot.</param>
        public void Update(float elapsedSeconds, bool ready)
        {
            if (Map == null || !ready) return;

            //The entry animation has finished: arm the first shot for the next frame rather than making it wait
            //out a whole interval
            if (_index == -1) { _index = 0; _elapsed = INTERVAL_SECONDS; }

            if (_index >= STEPS) return;

            _elapsed += elapsedSeconds;
            if (_elapsed < INTERVAL_SECONDS) return;

            _elapsed = 0f;
            Step(_index);
            _index++;

            if (_index < STEPS) return;

            //Sweep done: hold the barrel aimed straight up (clamped to MaxElevation, ~80°) and leave ADS
            //engaged, so the steepest precise-aim view - the one that used to sink the lens under the island -
            //sits as a stable frame to inspect or screenshot.
            _cannon.AimAt(new Vector3(_cannon.OrbitCenter.X, 100f, _cannon.OrbitCenter.Z));
            Console.WriteLine($"[aimshoot] centre-column sweep complete; holding straight-up, ADS lens Y={LensPosition().Y:F1} (island top {ArenaIsland.TOP_Y:F1})");
        }

        /// <summary>
        /// One step of the scan. Steps 0..N-1 walk up the field's centre column; the last four are its top
        /// corners, the steepest facing shots. For each the carriage is orbited to face the cell and the cannon
        /// aimed at it, then a shot is fired through the normal game-mode path (so any attach is reported by the
        /// usual contact logging). Logs the elevation the aim asked for against what the clamp allowed, so a shot
        /// held short by the clamp is obvious.
        /// </summary>
        private void Step(int step)
        {
            byte topLevel = (byte)(Map.Levels - 1);
            XZLevel cell;
            string label;

            if (step < COLUMN_STEPS)
            {
                byte level = (byte)(topLevel * step / (COLUMN_STEPS - 1));
                cell = new XZLevel(Map.StageSizeX / 2, Map.StageSizeZ / 2, level);
                label = "centre";
            }
            else
            {
                byte lastX = (byte)(Map.StageSizeX - 1), lastZ = (byte)(Map.StageSizeZ - 1);
                cell = (step - COLUMN_STEPS) switch
                {
                    0 => new XZLevel(0, 0, topLevel),
                    1 => new XZLevel(lastX, 0, topLevel),
                    2 => new XZLevel(0, lastZ, topLevel),
                    _ => new XZLevel(lastX, lastZ, topLevel),
                };
                label = "top corner";
            }

            Vector3 target = Map.GetRealCenteredPosition(cell);

            _cannon.OrbitToFace(target); //stand facing the cell so the shot is the clean, steep facing one
            bool reachable = _cannon.CanAimAt(target, out float wantedElevation, out _);
            _cannon.AimAt(target);

            Vector3 dir = _cannon.AimTarget - _cannon.Position;
            float gotElevation = MathF.Atan2(dir.Y, MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z));

            //ADS lens Y against the island top (ArenaIsland.TOP_Y): confirms the precise-aim camera stays above the floor
            //even at the steep corner shots, where it used to sink through the stone disc.
            float adsLensY = LensPosition().Y;

            Console.WriteLine($"[aimshoot] {label} ({cell.X},{cell.Z},{cell.Level}) Y={target.Y:F1}: " +
                $"want {wantedElevation * RAD_TO_DEG:F1} deg, got {gotElevation * RAD_TO_DEG:F1} deg  ->  {(reachable ? "reachable" : "CLAMPED SHORT")}" +
                $"; ADS lens Y={adsLensY:F1} (island top {ArenaIsland.TOP_Y:F1})");

            _shoot();
        }

        /// <summary>
        /// Where the precise-aim lens sits this instant, for the two diagnostics that check it stays above the
        /// stone island at the steep corner shots, where it used to sink through the disc. The floor that holds
        /// it there is <see cref="PreciseAim.FLOOR_CLEARANCE"/> over the local stone
        /// (<see cref="ArenaIsland.FloorHeightAt"/>).
        /// </summary>
        private Vector3 LensPosition() =>
            PreciseAim.LensPosition(_cannon.MuzzlePosition(_pivotToFrontBall), _cannon.AimDirection);

        /// <summary>
        /// Diagnostic (<c>aimcheck</c>): reports whether the cannon can be aimed at every cell of the loaded map,
        /// which is what makes a level finishable. The clean shot at a cell is from the orbit angle that
        /// <i>faces</i> it (the cell on the near side): the ball rises from the gun straight to it over open
        /// ground. The opposite angle is geometrically shallower but fires across the whole hanging cluster, so
        /// it is obstructed for anything high — this facing angle is the one that actually has to fit the
        /// elevation clamp. It steepens with height and with distance out from the field's axis, so the top
        /// corners of a large map bind. Logs the steepest required facing elevation against the clamp and a
        /// PASS/FAIL.
        /// <para>
        /// Static, and separate from the sweep, because <c>aimcheck</c> runs without <c>aimshoot</c>: it is asked
        /// once per map load and keeps no state at all. The test itself is <see cref="AimReachability"/>'s since
        /// #76 — a pure function of the map and the gun's orbit, so the map editor can ask it before saving a
        /// level rather than a script having to read a console line. The three lines stay here on purpose: they
        /// are a CLI surface <c>.claude/skills/verify</c> documents, so their exact wording is a contract and
        /// belongs with the executable that publishes it.
        /// </para>
        /// </summary>
        public static void LogReachability(BallsMap map, float orbitRadius, float trunnionsY)
        {
            if (map == null) return;

            AimReachabilityResult reach = AimReachability.Check(map, orbitRadius, trunnionsY, Cannon.MaxElevation);

            string verdict = reach.Pass
                ? "PASS - every cell can be shot while facing it"
                : $"FAIL - {reach.UnreachableCells}/{reach.TotalCells} cells need more up-elevation than the clamp allows (unfinishable)";

            Console.WriteLine($"[aimcheck] Field {map.StageSizeX}x{map.StageSizeZ}x{map.Levels}: cannon orbit R={orbitRadius:F1}, trunnions Y={trunnionsY:F1}");
            Console.WriteLine($"[aimcheck]   elevation clamp [{Cannon.MinElevation * RAD_TO_DEG:F1}, {Cannon.MaxElevation * RAD_TO_DEG:F1}] deg, traverse +/-{Cannon.MaxTraverse * RAD_TO_DEG:F0} deg");
            Console.WriteLine($"[aimcheck]   steepest cell ({reach.WorstCell.X},{reach.WorstCell.Z},{reach.WorstCell.Level}) at Y={reach.WorstCellY:F1} needs {reach.WorstElevation * RAD_TO_DEG:F1} deg facing elevation  ->  {verdict}");
        }
    }
}
