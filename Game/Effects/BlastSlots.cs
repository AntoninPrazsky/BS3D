using Microsoft.Xna.Framework;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <see cref="Blasts"/>' state, without the GPU and without the sound (#767's review): which slots burn, how old each one
    /// is, a chain's stagger, a blade's hurry, when a slot is freed and the light they throw. <see cref="Blasts"/> draws what
    /// stands here and plays the reports it says are due. Kept apart so the tests can step the very ages the picture is drawn
    /// from, at any frame rate, through the session's own slow motion, instead of a copy of the arithmetic.
    /// <para>
    /// Everything advances on what <see cref="Step"/> is handed, which is the <b>world's</b> seconds (slow motion included,
    /// see <see cref="Blasts"/>' remarks); nothing counts frames.
    /// </para>
    /// </summary>
    internal sealed class BlastSlots
    {
        /// <summary>Concurrent blasts. Must match <c>MAX_BLASTS</c> in Blast.fx.</summary>
        public const int MAX_BLASTS = 8;

        /// <summary>
        /// How long a bomb's slot stays taken. Must cover Blast.fx's longest-lived part of a bomb — a spark at the top of
        /// its life jitter, <c>SPARK_SECONDS</c> × 1.45 = 0.87 s — or a part is cut off mid-fade. A blade's is
        /// <see cref="CutBeats.LIFE_SECONDS"/>, which covers its slash and its sparks the same way.
        /// </summary>
        public const float LIFE_SECONDS = 0.95f;

        /// <summary>
        /// How far behind the bomb that set it off each link of a chain goes off, in seconds. See <see cref="Blasts"/>'
        /// remarks for why it is this short; a chain of five, the longest one landing sets off in the campaign
        /// (Sill, Paroxysm), is over in under a third of a second.
        /// </summary>
        public const float CHAIN_STAGGER_SECONDS = 0.07f;

        /// <summary>
        /// Balls destroyed at which a blast counts as full size. A blast's radius holds a couple of dozen cells of
        /// a packed cluster, and a bomb on a cluster's underside takes about half that — measured over the shipped
        /// bomb levels, 13 a bomb on Vent and Paroxysm and 23 on Sill.
        /// </summary>
        private const float FULL_SIZE_DESTROYED = 24f;

        /// <summary>The smallest a blast is drawn and heard, so a bomb that took only itself still plainly goes off.</summary>
        private const float MIN_SIZE = 0.35f;

        //The light the blast throws (see SceneLights.SetFlash): how long it lasts, how far it reaches and what it
        //is at full strength. Linear radiance, far over 1 on purpose — a point light's term is attenuated by the
        //square of the distance through its range and by N·L, and at 1 per channel a ball two units away took a
        //faint warm tint rather than being lit up.
        //
        //It started at (5, 2.2, 0.7) for 0.38 s and the first capture could barely find it on the cluster: the
        //balls round a blast took a faint warm cast and nothing read as having been LIT.
        private const float LIGHT_SECONDS = 0.45f;
        private const float LIGHT_RANGE = 10f;
        private static readonly Vector3 LIGHT_COLOR = new(8.5f, 3.6f, 1.0f);

        //The most the summed light of a chain may reach, as a multiple of LIGHT_COLOR. Five links going off in a
        //third of a second overlap, and uncapped their sum floods the whole cluster white.
        private const float LIGHT_CEILING = 2.5f;

        //A blade's light (#767): the cutter's white-hot accent driven well over 1 like the blast's, lasting
        //CutBeats.LIGHT_SECONDS (more than twice a bomb's LIGHT_SECONDS, with the slash and the sparks it lights) and
        //reaching as far. Linear radiance.
        private static readonly Vector3 BLADE_LIGHT_COLOR = new(7.5f, 7.0f, 6.2f);

        /// <summary>One slot.</summary>
        public struct Blast
        {
            public Vector3 Position;
            public float Age;            //negative while its link waits its turn, 0 at the moment it goes off
            public float Size;           //0..1
            public int Link;             //how far down its chain, 0 for a bomb the landing set off itself
            public bool Active;
            public bool Reported;        //has its report been played and its jolt handed out
            public bool Blade;           //a blade's impact (#767) rather than a bomb's: see Cut
            public bool Hurried;         //a shot was fired while it burned: it plays out CutBeats.HURRY_RATE times as fast
            public CutSlash Slash;       //a blade's slash: its line, where its middle is and how far it reaches
        }

        private readonly Blast[] _blasts = new Blast[MAX_BLASTS];

        /// <summary>The slot <paramref name="slot"/> as it stands.</summary>
        public ref readonly Blast this[int slot] => ref _blasts[slot];

        /// <summary>
        /// Sets off every blast of a landing, each link of the chain queued behind the bomb that reached it.
        /// <para>
        /// <b>Copies what it needs out of <paramref name="detonations"/> here and now</b>: the list is the contact
        /// handler's own and the next landing refills it (see <c>BallLanding.Detonations</c>), while a chain plays
        /// out over several frames after this call returns.
        /// </para>
        /// <para>
        /// A ninth blast takes the <b>oldest</b> slot rather than being dropped: the oldest is the one furthest
        /// through its fade, so taking it loses the least that is still being looked at.
        /// </para>
        /// </summary>
        public void SetOff(IReadOnlyList<Detonation> detonations)
        {
            for (int i = 0; i < detonations.Count; i++)
            {
                Detonation detonation = detonations[i];

                int slot = FreeOrOldestSlot();

                _blasts[slot] = new Blast
                {
                    Position = detonation.World,
                    Age = -detonation.Link * CHAIN_STAGGER_SECONDS,
                    Link = detonation.Link,
                    Size = MathHelper.Lerp(MIN_SIZE, 1f,
                        MathHelper.Clamp(detonation.Destroyed / FULL_SIZE_DESTROYED, 0f, 1f)),
                    Active = true,
                    Reported = false
                };
            }
        }

        /// <summary>
        /// A blade's impact (#767), at <paramref name="struck"/>, its slash along <paramref name="slash"/>, at
        /// <paramref name="size"/> (0 to 1): see <see cref="Blasts.Cut"/>. Reported already: a cut answers with its own sound
        /// and knock.
        /// </summary>
        public void Cut(Vector3 struck, CutSlash slash, float size)
        {
            int slot = FreeOrOldestSlot();

            _blasts[slot] = new Blast
            {
                Position = struck,
                Age = 0f,
                Link = 0,
                Size = MathHelper.Clamp(size, 0f, 1f),
                Active = true,
                Reported = true,
                Blade = true,
                Slash = slash
            };
        }

        /// <summary>
        /// A shot was fired: every blade still burning plays the rest of its picture <see cref="CutBeats.HURRY_RATE"/> times as
        /// fast (#765: skippable, #767's review: eased, not stepped), the slash thinning and cooling to amber, the sparks
        /// running out and the light fading through every age they would have passed, so the field clears in a few frames
        /// and nothing vanishes in one. Returns whether a blade was hurried now.
        /// </summary>
        public bool HurryBlades()
        {
            bool any = false;

            for (int i = 0; i < _blasts.Length; i++)
            {
                if (!_blasts[i].Active || !_blasts[i].Blade || _blasts[i].Hurried) continue;

                _blasts[i].Hurried = true;
                any = true;
            }

            return any;
        }

        private int FreeOrOldestSlot()
        {
            int oldest = 0;

            for (int i = 0; i < _blasts.Length; i++)
            {
                if (!_blasts[i].Active) return i;
                if (_blasts[i].Age > _blasts[oldest].Age) oldest = i;
            }

            return oldest;
        }

        /// <summary>
        /// Advances every blast by <paramref name="elapsed"/> seconds of the <b>world's</b> time (a hurried blade by
        /// <see cref="CutBeats.HURRY_RATE"/> times that) and frees the slots whose picture is over.
        /// </summary>
        /// <returns>
        /// The slots whose moment came this step, one bit a slot (bit <c>i</c> for slot <c>i</c>): a bomb whose age has just
        /// crossed zero, whose report, jolt and flash arrive together however far down its chain it was. Zero on every
        /// other step; a blade is never due (it is reported at <see cref="Cut"/>).
        /// </returns>
        public int Step(float elapsed)
        {
            int due = 0;

            for (int i = 0; i < _blasts.Length; i++)
            {
                if (!_blasts[i].Active) continue;

                _blasts[i].Age += _blasts[i].Hurried ? elapsed * CutBeats.HURRY_RATE : elapsed;

                //On the step the age crosses zero, not when the chain was queued
                if (!_blasts[i].Reported && _blasts[i].Age >= 0f)
                {
                    _blasts[i].Reported = true;
                    due |= 1 << i;
                }

                if (_blasts[i].Age > (_blasts[i].Blade ? CutBeats.LIFE_SECONDS : LIFE_SECONDS)) _blasts[i].Active = false;
            }

            return due;
        }

        /// <summary>
        /// The light the blasts throw now, as one light: a chain's links go off inside a third of a second a few units apart,
        /// and one light at their weighted centre, as strong as their sum, lights the cluster the way they would together —
        /// for one slot where five would crowd the scene's own lamps out.
        /// </summary>
        public bool TryGetLight(out Vector3 position, out Vector3 color, out float range)
        {
            float total = 0f, largest = 0f;
            Vector3 weighted = Vector3.Zero, tinted = Vector3.Zero;

            for (int i = 0; i < _blasts.Length; i++)
            {
                Blast blast = _blasts[i];
                if (!blast.Active || blast.Age < 0f) continue;

                //Squared, the ceiling flash's rule: unmistakable on the frame it goes off, thinned well before its end. A
                //blade's is its own (#767): never under half strength, however little the cut freed - the strike itself is the
                //event - and lasting with its slash and sparks (CutBeats.LightStrength), hurried with them by a shot.
                float strength;
                if (blast.Blade) strength = CutBeats.LightStrength(blast.Age, blast.Size);
                else
                {
                    if (blast.Age >= LIGHT_SECONDS) continue;
                    float left = 1f - blast.Age / LIGHT_SECONDS;
                    strength = blast.Size * left * left;
                }

                if (strength <= 0f) continue;

                total += strength;
                weighted += blast.Position * strength;
                tinted += (blast.Blade ? BLADE_LIGHT_COLOR : LIGHT_COLOR) * strength;
                largest = MathF.Max(largest, blast.Size);
            }

            if (total <= 0f)
            {
                position = color = Vector3.Zero;
                range = 0f;
                return false;
            }

            position = weighted / total;

            //Each blast's own colour, weighted as its place is, at the summed strength under the ceiling
            color = tinted / total * MathF.Min(total, LIGHT_CEILING);
            range = LIGHT_RANGE * (0.7f + 0.3f * largest);
            return true;
        }

        /// <summary>Whether any slot holds a blade still burning: only then does the draw take the blade's extra sparks.</summary>
        public bool AnyBlade()
        {
            for (int i = 0; i < _blasts.Length; i++)
                if (_blasts[i].Active && _blasts[i].Blade) return true;

            return false;
        }

        /// <summary>Nothing left burning — a fresh level starts clean.</summary>
        public void Reset()
        {
            for (int i = 0; i < _blasts.Length; i++) _blasts[i].Active = false;
        }
    }
}
