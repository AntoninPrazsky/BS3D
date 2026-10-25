using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace BS3D.Effects
{
    /// <summary>
    /// The unused balls fired off and counted (#881). The owner, of the completion bonus: a figure in the corner said nothing,
    /// and it should be "a skippable animation where the gun very quickly fires all the remaining balls and the bonus counts up
    /// in the same rhythm", with the camera moved to an angle beside the gun "so the player understands without a doubt that
    /// he is not the one firing" - and it teaches that saving shots pays.
    /// <para>
    /// <b>The schedule</b> is pure, so <c>SpareVolleyTests</c> holds it: <see cref="Balls"/> shots at <see cref="Interval"/>,
    /// which is <see cref="MAX_INTERVAL"/> for a few and shrinks so a whole budget is fired within <see cref="MAX_SECONDS"/>,
    /// the first after <see cref="LEAD_IN"/> (the lens arriving), the camera let go <see cref="TAIL"/> after the last. The
    /// count after each shot is its share of the bonus (<see cref="CountAt"/>), the last one exactly the bonus, and a skip
    /// lands on the bonus too: the figure is the same however it is reached.
    /// </para>
    /// <para>
    /// <b>The camera</b> is a pose and a blend, as <see cref="LineLossCinematic"/> is (the host composes it with the others and
    /// <c>CameraTakeoverEngaged</c> counts it, so the gun does not answer and the cleared beat's countdown waits for it): to
    /// one side of the gun and a little behind and above it, looking along the barrel at the field the balls fly into.
    /// </para>
    /// </summary>
    internal sealed class SpareVolley
    {
        //The rhythm: a few balls at a steady cadence, a whole budget inside MAX_SECONDS (the longest budget, 90, at the floor;
        //a frame that owes several spreads them along their flight, GameplayScreen.StepSpareVolley)
        internal const float MAX_INTERVAL = 0.11f, MIN_INTERVAL = 0.025f;
        internal const float MAX_SECONDS = 2.2f;

        //The lens arriving before the first shot, and the last balls in the air after the last one before it goes back
        internal const float LEAD_IN = 0.45f;
        internal const float TAIL = 0.55f;

        //A skip is refused this long after the volley begins, as the drop cinematic's is: the click or Space that was meant as
        //one more shot as the field emptied would otherwise skip it before a ball left
        internal const float SKIP_LOCKOUT = 0.3f;

        //Where the lens stands, from the gun's pivot: to its side, behind it and above it, in world units; how far along the
        //barrel it looks and how far under that line (the barrel climbs to the cluster, and a lens looking along it framed the
        //glass with the gun half out at the bottom); and a field of view wider than the player's so the gun and the flight fit
        private const float SIDE = 7f, BACK = 3.2f, RISE = 2.4f, LOOK_AHEAD = 3f, LOOK_DOWN = 1.6f;
        private const float FIELD_OF_VIEW_SCALE = 1.15f;
        private const float BLEND_IN = 0.35f, BLEND_OUT = 0.45f;

        private bool _running;
        private float _elapsed, _blend, _playerFov;
        private bool _skipped;
        private Vector3 _from, _fromTarget, _to, _toTarget;

        /// <summary>How many balls are fired and what they are worth together.</summary>
        public int Balls { get; private set; }
        public int Bonus { get; private set; }

        /// <summary>How many have been counted, and how many of them really left the barrel (a skip counts the rest unfired).</summary>
        public int Fired { get; private set; }
        public int Launched { get; private set; }
        public int Counted => CountAt(Fired, Balls, Bonus);

        /// <summary>Whether the lens is held or still on its way back.</summary>
        public bool Engaged => _running || _blend > 0f;

        /// <summary>Whether the volley is under way: the lens held and the count still to finish.</summary>
        public bool Running => _running;

        /// <summary>How much of this pose is mixed into the player's, 0 to 1.</summary>
        public float Blend => _blend;

        public Vector3 Position { get; private set; }
        public Vector3 Target { get; private set; }
        public float FieldOfView { get; private set; }

        /// <summary>The time between two shots for a volley of <paramref name="balls"/>.</summary>
        public static float Interval(int balls) =>
            balls <= 1 ? MAX_INTERVAL : Math.Clamp(MAX_SECONDS / balls, MIN_INTERVAL, MAX_INTERVAL);

        /// <summary>How long a volley of <paramref name="balls"/> holds the lens, from the start to the last shot's tail.</summary>
        public static float Duration(int balls) => LEAD_IN + Math.Max(balls - 1, 0) * Interval(balls) + TAIL;

        /// <summary>The count once <paramref name="fired"/> of <paramref name="balls"/> have left: their share of the bonus, rounded down, the last one the whole of it.</summary>
        public static int CountAt(int fired, int balls, int bonus)
        {
            if (balls <= 0 || fired >= balls) return bonus;
            if (fired <= 0) return 0;
            return (int)((long)bonus * fired / balls);
        }

        /// <summary>
        /// Takes the lens and arms the volley: <paramref name="balls"/> worth <paramref name="bonus"/>, fired from the gun at
        /// <paramref name="pivot"/> along <paramref name="fireDirection"/> out of <paramref name="muzzle"/>.
        /// </summary>
        public void Begin(int balls, int bonus, Vector3 pivot, Vector3 muzzle, Vector3 fireDirection, Vector3 playerEye, Vector3 playerTarget,
            float playerFov)
        {
            Balls = Math.Max(balls, 0);
            Bonus = Math.Max(bonus, 0);
            Fired = Launched = 0;
            _running = Balls > 0;
            _skipped = false;
            _elapsed = 0f;
            _blend = 0f;
            _playerFov = playerFov;
            _from = playerEye;
            _fromTarget = playerTarget;

            //Beside the gun: the barrel's bearing flattened, the lens to its right, behind and above the pivot, looking along
            //it past the muzzle at the field. A barrel pointed straight up has no bearing: the lens's own then stands in
            Vector3 flat = new(fireDirection.X, 0f, fireDirection.Z);
            if (flat.LengthSquared() < 1e-4f) flat = new Vector3(playerTarget.X - playerEye.X, 0f, playerTarget.Z - playerEye.Z);
            if (flat.LengthSquared() < 1e-4f) flat = Vector3.Forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(flat, Vector3.Up);
            _to = pivot + right * SIDE - flat * BACK + Vector3.Up * RISE;
            _toTarget = muzzle + fireDirection * LOOK_AHEAD - Vector3.Up * LOOK_DOWN;
            Position = _from;
            Target = _fromTarget;
            FieldOfView = playerFov;
        }

        /// <summary>
        /// Advances by <paramref name="elapsed"/> and answers how many balls are due to leave this frame (none once skipped).
        /// The lens lets go <see cref="TAIL"/> after the last.
        /// </summary>
        public int Update(float elapsed)
        {
            int due = 0;
            if (_running)
            {
                _elapsed += elapsed;
                if (!_skipped)
                {
                    float interval = Interval(Balls);
                    int shouldHave = _elapsed < LEAD_IN ? 0 : Math.Min(Balls, 1 + (int)((_elapsed - LEAD_IN) / interval));
                    due = shouldHave - Fired;
                    Fired = Launched = shouldHave;
                }
                if (_elapsed >= Duration(Balls)) _running = false;

                float t = MathHelper.Clamp(_elapsed / LEAD_IN, 0f, 1f);
                float eased = MathHelper.SmoothStep(0f, 1f, t);
                Position = Vector3.Lerp(_from, _to, eased);
                Target = Vector3.Lerp(_fromTarget, _toTarget, eased);
                FieldOfView = MathHelper.Lerp(_playerFov, _playerFov * FIELD_OF_VIEW_SCALE, eased);
                _blend = MathF.Min(1f, _blend + elapsed / BLEND_IN);
            }
            else if (_blend > 0f)
            {
                _blend = MathF.Max(0f, _blend - elapsed / BLEND_OUT);
            }
            return due;
        }

        /// <summary>The rest counted at once and no more fired: the same figure, sooner. False when there is nothing to skip.</summary>
        public bool TrySkip()
        {
            if (!_running || _skipped || _elapsed < SKIP_LOCKOUT) return false;
            _skipped = true;
            _running = false;
            Fired = Balls;
            return true;
        }

        /// <summary>Back to nothing: a fresh level has no volley behind it.</summary>
        public void Reset()
        {
            _running = false;
            _skipped = false;
            _blend = 0f;
            _elapsed = 0f;
            Fired = Launched = Balls = Bonus = 0;
        }

        /// <summary>The time between two shots of this volley.</summary>
        public float CurrentInterval => Interval(Balls);

        public string Describe() => string.Create(CultureInfo.InvariantCulture,
            $"{Balls} balls worth {Bonus} at {Interval(Balls):0.000} s, {Duration(Balls):0.00} s in all");
    }
}
