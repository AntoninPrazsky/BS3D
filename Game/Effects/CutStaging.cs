using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>Using the Cut is a moment</b> (#767): the session's state of its four beats, which <see cref="CutBeats"/> draws,
    /// <see cref="CutRumble"/> says in the hand and <c>GameplayScreen</c> wires to the gun, the lens and the world. Until #767
    /// the Cut was a chip that dimmed, a click, and at the strike one storey gone and an award: a cut that freed nothing and
    /// one that freed forty looked the same for the first half-second, and a player thought it "does nothing" (#692).
    /// <list type="number">
    /// <item><description><b>The press</b> (<see cref="Press"/>, wall clock): the round in the bore charges - its saw spins up
    /// from rest to the flight's speed and back (<see cref="SpinOffset"/>), its light surges (<see cref="Glow"/>), the collar
    /// and the rays flare with it, the charge sound rises to its "ting" and the pad's hold rises under both triggers.</description></item>
    /// <item><description><b>The flight</b> (<see cref="Fired"/>, wall clock): the lens pinches (<see cref="FovScale"/>), and
    /// what the round will cut off stays lit while it flies.</description></item>
    /// <item><description><b>The severing</b> (<see cref="Strike"/>, the world's clock): the blade's slash through the storey
    /// it struck, a white-hot flash and a grinder's sparks (<c>Blasts.Cut</c>), the slice's ring, a knock in the hand, and a
    /// hit-stop - the world almost held for a beat (<see cref="TimeScale"/>).</description></item>
    /// <item><description><b>The fall</b>: what the cut freed drops in slow motion that grows with how much it was, its cut
    /// face glowing white-hot and cooling to nothing as it falls, and the storey left hanging above the cut glowing with it;
    /// the pad's roll-off grows along the release's curve. A cut with nothing hanging on its storey is the severing alone, still an
    /// event; a bigger result adds beats on top.</description></item>
    /// </list>
    /// <b>Never in the way</b>: nothing here changes a rule or the score (the cut's landing is scored as it always was), the
    /// slow motion is the world's alone - the HUD, the lens and the gun run on - and a shot fired during it ends it
    /// (<see cref="Skip"/>) and leaves as it always would, so it eats no input. A pause stops every clock that feeds it.
    /// </summary>
    internal sealed class CutStaging
    {
        /// <summary>How brightly the freed part's cut face glows at its hottest, through the ripple's channel: well over the
        /// preview's (which it takes over from), under a landing wave's peak of 1, since a cut face can be a whole storey.</summary>
        public const float FALL_GLOW = 0.75f;

        /// <summary>How brightly the storey left hanging above the cut glows at its hottest: the other face of the cut.</summary>
        public const float FACE_GLOW = 0.45f;

        /// <summary>How much brighter than its accent the loaded round is at the top of its charge (<see cref="Glow"/>).</summary>
        public const float CHARGE_GLOW = 0.6f;

        //The beats' ages: the press and the flight on the wall clock, the slow motion's own timeline on the wall clock, the
        //cut faces' heat on the world's. Negative is "not running".
        private float _pressAge = -1f;
        private float _fireAge = -1f;
        private float _slowAge = -1f;
        private float _heatAge = -1f;

        //What the strike measured, for the slow motion
        private float _size;
        private bool _freed;

        //What glows: the freed balls with each one's share of the heat, and the storey left above the cut
        private readonly List<PhysicsBall> _freedBalls = new(256);
        private readonly List<float> _freedHeat = new(256);
        private readonly List<PhysicsBall> _faceBalls = new(128);

        /// <summary>How charged the round in the bore is now, 0 to 1 (<see cref="CutBeats.Charge"/>).</summary>
        public float Charge => CutBeats.Charge(_pressAge);

        /// <summary>
        /// The loaded cutter's light as a multiple of its accent: 1 at rest, up to 1 + <see cref="CHARGE_GLOW"/>. The round's
        /// own, wherever in the magazine it sits: a Swap that moves a charging cutter back a slot moves its glow with it, as its
        /// sound and its hold in the hand go on, so the press is never cut in half.
        /// </summary>
        public float Glow => 1f + CHARGE_GLOW * Charge;

        /// <summary>
        /// How far the loaded cutter's saw has turned past its idle turn since the press (<see cref="CutBeats.SpinOffset"/>),
        /// held once the press is over and until the round is fired, 0 when no press is behind it. The round's own, wherever
        /// it sits: drawn only at the muzzle, a cutter swapped back a slot and forward again leapt by the whole offset.
        /// </summary>
        public float SpinOffset => _pressAge < 0f ? 0f : CutBeats.SpinOffset(_pressAge);

        /// <summary>The lens's field of view as a multiple of what it would be: the flight's pinch.</summary>
        public float FovScale => CutBeats.FovScale(_fireAge);

        /// <summary>How fast the world runs now, 0 to 1: the strike's hit-stop and the fall's slow motion, 1 otherwise.</summary>
        public float TimeScale => _slowAge < 0f ? 1f : CutBeats.TimeScale(_slowAge, _size, _freed);

        /// <summary>Whether the strike's slow motion is running (what a shot fired now would end).</summary>
        public bool Slowed => _slowAge >= 0f;

        /// <summary>The press: the round in the bore has just become a cutter. Starts the charge.</summary>
        public void Press() => _pressAge = 0f;

        /// <summary>The flight: a cutter has just left the barrel. Starts the lens's pinch and ends the press, whose round is gone.</summary>
        public void Fired()
        {
            _fireAge = 0f;
            _pressAge = -1f;
        }

        /// <summary>
        /// Ends the strike's slow motion at once: a shot fired during it (#765: skippable, and never eating the input that
        /// skips it), the drop cinematic taking the lens with its own (the two never run together), the result page.
        /// </summary>
        public void Skip() => _slowAge = -1f;

        /// <summary>
        /// The severing: a cutter struck <paramref name="cell"/> at <paramref name="struck"/> (world space) and let go
        /// <paramref name="freedCount"/> balls, which are the last <paramref name="freedCount"/> entries of
        /// <paramref name="falling"/> - the storey it destroyed and what hung on it. Arms the cut faces' glow and the slow
        /// motion, and answers how far the slash should reach either side of the struck point and how big the result was.
        /// </summary>
        /// <param name="orphaned">What hung on the storey alone: the part of the result the fall's slow motion grows with.</param>
        /// <param name="slowMotion">False while the drop cinematic has the lens and its own slow motion (a big release): the
        /// cut adds none on top of it, so the cinematic runs exactly as it always has.</param>
        public void Strike(XZLevel cell, Vector3 struck, List<PhysicsBall> falling, int freedCount, int orphaned,
            PhysicsBall[,,] cluster, bool slowMotion, out float halfLength, out float size)
        {
            ClearGlows();

            //The slash reaches across the storey it went through: as far as the farthest of that storey's balls, level with
            //the strike, plus a ball's width so it runs out past the silhouette, and never shorter than a few balls
            float reach = 0f;
            int first = Math.Max(0, falling.Count - freedCount);

            for (int i = first; i < falling.Count; i++)
            {
                PhysicsBall ball = falling[i];
                if (ball.Kind == BallKind.Cutter) continue;

                Vector3 at = ball.BallReference.Pose.Position.ToXna();
                float depth = struck.Y - at.Y;

                //The cut's own storey, level with the strike: its spread is the slash's reach
                if (MathF.Abs(depth) < 0.35f)
                {
                    Vector2 across = new(at.X - struck.X, at.Z - struck.Z);
                    reach = MathF.Max(reach, across.Length());
                }

                _freedBalls.Add(ball);
                _freedHeat.Add(CutBeats.HeatAtDepth(depth));
            }

            halfLength = MathF.Max(MIN_HALF_LENGTH, reach + 1f);

            //The other face of the cut: every ball still hanging in the storey just above a ball the cut destroyed. The cells
            //are the destroyed balls' own (a released ball keeps its ArrayPosition), so nothing is guessed from positions.
            if (cluster != null)
            {
                XZLevel bounds = XZLevel.FromArray(cluster);

                for (int i = 0; i < _freedBalls.Count; i++)
                {
                    XZLevel at = _freedBalls[i].ArrayPosition;
                    if (at.Level != cell.Level) continue;

                    foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(at, bounds))
                    {
                        if (neighbour.Level != cell.Level + 1) continue;

                        PhysicsBall above = cluster[neighbour.X, neighbour.Z, neighbour.Level];
                        if (above != null && !_faceBalls.Contains(above)) _faceBalls.Add(above);
                    }
                }
            }

            _heatAge = 0f;

            size = CutBeats.Size(freedCount);
            _size = CutBeats.Size(orphaned);
            _freed = orphaned > 0;
            _slowAge = slowMotion ? 0f : -1f;

            StepWorld(0f);
        }

        //The shortest slash: a few balls either side, so a cut through a storey of one still reads as a cut
        private const float MIN_HALF_LENGTH = 2.5f;

        /// <summary>
        /// What the press asks of the pad this frame (#767): the rising hold on the body and trigger motors, all zero when no
        /// press is charging. A hold is asked every frame it lasts and is gone the first frame it is not
        /// (<c>GamepadRumble.Hold</c>, <c>HoldTriggers</c>), so the session hands this on every frame.
        /// </summary>
        public (float Left, float Right, float LeftTrigger, float RightTrigger) PadHold =>
            _pressAge < 0f ? default : CutRumble.Charge(_pressAge);

        /// <summary>Whether the charge topped out on the last <see cref="StepWall"/>: the session's cue for the click under both triggers.</summary>
        public bool ToppedOut { get; private set; }

        /// <summary>
        /// The beats that run on the wall clock, by <paramref name="elapsed"/> seconds of it: the press (and when it tops out,
        /// <see cref="ToppedOut"/>), the flight's pinch and the slow motion's own timeline.
        /// </summary>
        public void StepWall(float elapsed)
        {
            ToppedOut = false;

            if (_pressAge >= 0f)
            {
                float before = _pressAge;
                _pressAge = MathF.Min(_pressAge + elapsed, CutBeats.PRESS_SECONDS);

                //On the frame it crosses the top, whatever the frame's length
                ToppedOut = before < CutBeats.CHARGE_SECONDS && _pressAge >= CutBeats.CHARGE_SECONDS;
            }

            if (_fireAge >= 0f)
            {
                _fireAge += elapsed;
                if (_fireAge >= CutBeats.PINCH_IN_SECONDS + CutBeats.PINCH_OUT_SECONDS) _fireAge = -1f;
            }

            if (_slowAge >= 0f)
            {
                _slowAge += elapsed;
                if (_slowAge >= CutBeats.SlowSeconds(_size, _freed)) _slowAge = -1f;
            }
        }

        /// <summary>
        /// The beats that belong to the world, by <paramref name="elapsed"/> seconds of the simulation's time (slow motion
        /// included): the cut faces' heat, written into each ball's glow through the ripple's channel
        /// (<see cref="PhysicsBall.CutPreviewGlow"/>, which the preview hands over to at the strike).
        /// </summary>
        public void StepWorld(float elapsed)
        {
            if (_heatAge < 0f) return;

            _heatAge += elapsed;

            float heat = CutBeats.Heat(_heatAge);
            if (heat <= 0f)
            {
                ClearGlows();
                return;
            }

            for (int i = 0; i < _freedBalls.Count; i++) _freedBalls[i].CutPreviewGlow = FALL_GLOW * _freedHeat[i] * heat;
            for (int i = 0; i < _faceBalls.Count; i++) _faceBalls[i].CutPreviewGlow = FACE_GLOW * heat;
        }

        /// <summary>Everything at rest: a new level, a retry. The glows are put out on the balls that still carry them.</summary>
        public void Reset()
        {
            ClearGlows();
            _pressAge = _fireAge = _slowAge = -1f;
        }

        private void ClearGlows()
        {
            for (int i = 0; i < _freedBalls.Count; i++) _freedBalls[i].CutPreviewGlow = 0f;
            for (int i = 0; i < _faceBalls.Count; i++) _faceBalls[i].CutPreviewGlow = 0f;

            _freedBalls.Clear();
            _freedHeat.Clear();
            _faceBalls.Clear();
            _heatAge = -1f;
        }
    }
}
