using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// The gun's magazine: the queue of loaded ball colours, the glide that carries it forward after a shot, and
    /// where each loaded ball sits in the bore. It stood in both executables — the Testbed's <c>_magazine</c> and
    /// the Game's — with the size, the spacing and the ease constant value-identical, until #76.
    /// <para>
    /// It is what makes a shot aimable at all. The barrel is a tube with a slit along the top
    /// (<see cref="CannonRig.SLOT_HALF_ANGLE"/>) for no other reason than to show this queue: the balls nest
    /// inside the bore and only a strip of each reads, and you cannot aim a shot whose colour you cannot see.
    /// Slot 0 is the one at the muzzle — the round that fires, and where a shot spawns — and the rest recede
    /// towards the breech.
    /// </para>
    /// <para>
    /// <b>The invariant that was only a comment in both copies is the type's now: the queue never empties.</b>
    /// Every slot holds a real colour from the moment the magazine exists — the constructor deals a full queue,
    /// and <see cref="Advance"/> deals the tail before it returns — so there is no instant at which a slot holds
    /// <c>default(BallType)</c>. That zero is not a colour (<see cref="BallType"/> starts at 1; an empty cell in
    /// a map is a null ball and not a type 0), and it would not fail loudly: every collector in this project
    /// rejects an out-of-range type index and quietly draws nothing, so a queue that lost a ball would look like
    /// a queue with a hole in it and log not a word. Hence the check in <see cref="Recolour"/> and on the drawn
    /// colour, both of which cost one comparison per <i>shot</i>. The array never leaves this class either:
    /// <see cref="Peek()"/> reads, <see cref="Advance"/> shifts, <see cref="Recolour"/> re-colours one slot, and
    /// there is no indexer to write through.
    /// </para>
    /// <para>
    /// <b>What loads next is injected, and neither policy lives here.</b> The Testbed draws uniformly from all
    /// eight types — its cluster is whatever map was dropped on it. The Game draws only among the colours
    /// <i>still hanging</i>, because a ball of a colour that exists nowhere in the cluster can never match
    /// anything: it can only be parked, which grows the very cluster the player is shrinking, wastes a budgeted
    /// shot and in the limit makes a level unwinnable (docs/game-session.md). Answering that needs the live count
    /// of every colour, which is a question about a map, a level and its rules — three things this type would
    /// have to learn in order to ask it. So it is one <see cref="Func{TResult}"/> handed in at construction, and
    /// the whole of the difference between the two executables' magazines is that delegate.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The <i>drawing</i>: the loaded balls go through each
    /// executable's own instance collector, which buckets by type and LOD and carries occlusion the Testbed and
    /// the Game do not spell the same way, so the placement is handed back as plain values
    /// (<see cref="BorePose"/>) and this type never touches a renderer. The <i>recoil</i>: a scalar parameter,
    /// since one executable animates a stroke and the other does not, and neither the shape nor the decay is
    /// this type's business. And the Game's <i>transmute cross-fade</i> — which loaded colours have died, what
    /// each slot is dissolving out of and how far through it is — which is a rule about the cluster; only the
    /// <b>shift</b> of that state is owned here, for the reason <see cref="Advance"/> gives at length.
    /// </para>
    /// <para>
    /// The barrel's own figures are <see cref="CannonRig"/>'s and its pose is <see cref="Cannon"/>'s: this type
    /// is told how far ahead of the trunnions the head of the queue sits rather than deriving the bore's frame
    /// again, so the tube that was built, the muzzle that is fired from and the balls drawn in it cannot
    /// disagree.
    /// </para>
    /// </summary>
    public sealed class Magazine
    {
        /// <summary>
        /// How many balls the gun is loaded with: the one at the muzzle and four queued behind it. Enough that
        /// the player can plan a few shots ahead, which is the whole reason the queue is visible, and not so many
        /// that the barrel becomes a train — the tube's length is derived from this figure
        /// (<see cref="CannonRig"/>), so it is also how long the gun is.
        /// </summary>
        public const int SIZE = 5;

        /// <summary>
        /// How far apart the loaded balls sit along the bore, in world units: a ball diameter, so the queue is a
        /// line of balls touching one another with no gaps to read as missing rounds. The barrel's muzzle lip
        /// and breech face sit a ball radius beyond the end balls' centres, so the queue is exactly enclosed
        /// — change this and the tube's ends follow it through <see cref="CannonRig"/>.
        /// </summary>
        public const float SPACING = 1f;

        //Ease-out time constant for the post-shot glide, in seconds: quick off the mark and settling gently, ~0.2
        //s to arrive. The idiom precise aim borrows for its lean (PreciseAim.BLEND_TAU) - this is where it came
        //from.
        private const float SLIDE_TAU = 0.07f;

        //Where the glide is declared over and the slide snapped to exactly zero, so it ends rather than
        //approaching zero for ever. It is a fraction of a SLOT, so a thousandth is a thousandth of a ball
        //diameter - the two copies disagreed here (0.01 against 0.001) and neither figure is visible at the size
        //the gun is drawn; the tighter one is kept so nothing about the queue's resting place depends on it.
        private const float SLIDE_SETTLED = 0.001f;

        private readonly BallType[] _queue = new BallType[SIZE];

        private readonly Func<BallType> _nextType;
        private readonly Action<int, int> _slotCarried;
        private readonly Action<int, BallType> _slotLoaded;

        /// <param name="nextType">What to load, asked once per dealt slot. Must answer a real
        /// <see cref="BallType"/> — see the class remarks on the invariant. Each executable's own policy; see the
        /// class remarks on why neither lives here.</param>
        /// <param name="slotCarried">Called as the queue shifts, for each <c>(destination, source)</c> pair in
        /// turn, so a caller with per-slot state of its own can carry it forward in step — see
        /// <see cref="Advance"/>. Null for a caller with no such state (the Testbed).</param>
        /// <param name="slotLoaded">Called with a slot and the colour just dealt into it, so a caller can clear
        /// whatever per-slot state a fresh ball has none of. Fires from <see cref="Refill"/> and for
        /// <see cref="Advance"/>'s tail, never from <see cref="Recolour"/> — a re-coloured ball is precisely the
        /// one whose old colour must be kept.</param>
        /// <remarks>
        /// Both hooks are handed over <b>once</b>, here, and not per shot: a delegate built at the call site of
        /// every advance would allocate one per round fired. Neither reads anything back out of the magazine —
        /// the loaded hook is <i>given</i> the colour — so a caller can wire them from a constructor that has not
        /// yet assigned the field it is building, which it will: the constructor deals a full queue and fires
        /// <paramref name="slotLoaded"/> <see cref="SIZE"/> times before it returns. Whatever state those hooks
        /// write to has to exist by then, though, which is the caller's own initialisation order to get right.
        /// </remarks>
        public Magazine(Func<BallType> nextType, Action<int, int> slotCarried = null,
            Action<int, BallType> slotLoaded = null)
        {
            _nextType = nextType ?? throw new ArgumentNullException(nameof(nextType));
            _slotCarried = slotCarried;
            _slotLoaded = slotLoaded;

            //A full queue from the first frame, so the player has something to read and nothing has to cope with
            //an empty slot that cannot legally exist
            Refill();
        }

        /// <summary>
        /// How far the queue is still displaced <i>backwards</i> from its resting slots, in slots: 1 the instant
        /// a ball fires — every ball drawn one slot back, so the muzzle slot is empty — easing to 0 as the balls
        /// glide into place. (Every ball but the freshest: its share is clamped to the breech chamber it waits
        /// in — see <see cref="BorePose.SlotPosition"/>.) It is what keeps the advance from snapping, and it is
        /// the only reason <see cref="Step"/> exists. Nothing outside needs it: the placement below already
        /// applies it.
        /// </summary>
        public float Slide { get; private set; }

        /// <summary>The colour that will fire next: the ball at the muzzle, in slot 0.</summary>
        public BallType Peek() => _queue[0];

        /// <summary>
        /// The colour loaded in one slot, 0 at the muzzle to <see cref="SIZE"/> − 1 at the breech. For the draw
        /// loop and for the Game's transmute pass, which asks every slot whether its colour is still alive.
        /// </summary>
        public BallType Peek(int slot) => _queue[slot];

        /// <summary>
        /// Deals a whole fresh queue and settles it. Run at the start of a session, and in the Game on every
        /// level load — its colours belong to a level, and the level whose cluster they were drawn from is gone.
        /// Fires the loaded hook for every slot, so a caller's per-slot state starts clean rather than inheriting
        /// half-finished animations from the session before it.
        /// </summary>
        public void Refill()
        {
            for (int slot = 0; slot < SIZE; slot++) Deal(slot);

            Settle();
        }

        /// <summary>
        /// Shifts the queue forward one slot and deals a fresh colour into the back, so it never empties, then
        /// arms the slide at 1 so the balls are drawn one slot back and glide forward into the muzzle slot the
        /// shot just vacated rather than snapping into it.
        /// <para>
        /// <b>The shift is owned here, and that is the point of the carried hook.</b> The Game draws its queue
        /// from three arrays in step — the colour, the colour it is dissolving out of, and how far through that
        /// dissolve it is — and shifting only the colours would leave a slot dissolving out of the ball behind
        /// it (docs/game-session.md pins this). The state itself is the Game's, because <i>which</i> colours have
        /// died is a question about its cluster; but there must be exactly one place that knows a queue has
        /// moved, or the two copies of that loop are free to drift. So this walks the shift and reports every
        /// <c>(destination, source)</c> pair as it goes, and the caller carries its own arrays along the same
        /// path in the same order.
        /// </para>
        /// <para>
        /// Note what the fresh tail means for that state: a ball drawn from what is alive right now has nothing
        /// to fade out of, which is why the loaded hook fires for it. And the Game's countdown runs <b>1 → 0</b>,
        /// so the dissolve's progress is its <i>complement</i> — a caller feeding the carried countdown straight
        /// into <c>ModelInstance.Dissolve</c> runs the effect backwards, the new colour arriving complete on the
        /// frame of the swap with the old one never seen at all. That is exactly what the first build did and
        /// only a zoomed screenshot caught it.
        /// </para>
        /// </summary>
        public void Advance()
        {
            for (int slot = 0; slot < SIZE - 1; slot++)
            {
                _queue[slot] = _queue[slot + 1];
                _slotCarried?.Invoke(slot, slot + 1);
            }

            Deal(SIZE - 1);

            Slide = 1f;
        }

        /// <summary>
        /// Re-colours the ball in one slot where it sits, leaving the queue's order and the slide alone. The
        /// Game's transmute uses it: a loaded ball whose colour has just been eliminated from the cluster is
        /// re-coloured rather than left to be fired at nothing. The colour changes <b>immediately</b> and
        /// whatever the caller animates over it is cosmetic — firing mid-transition must give the new colour,
        /// never the dead one it is still fading out of.
        /// <para>
        /// Deliberately does <b>not</b> fire the loaded hook: that hook clears the per-slot state of a ball that
        /// has nothing to fade out of, and this is the one case where the old colour is exactly what must be
        /// kept.
        /// </para>
        /// </summary>
        /// <param name="type">The replacement colour. Must be a real one — the class remarks say what a zero
        /// does, which is nothing, silently.</param>
        public void Recolour(int slot, BallType type)
        {
            if (type == default) throw new ArgumentOutOfRangeException(nameof(type),
                "A magazine slot cannot hold the unused zero ball type; the queue may never empty.");

            _queue[slot] = type;
        }

        //Loads one slot from the injected policy and tells the caller about it. The one place a slot's colour is
        //written by anything but the transmute, so it is the one place the invariant can be enforced: a policy
        //that answered the unused zero would put a ball in the barrel that draws as nothing at all, and it would
        //do it without a word - see the class remarks. One comparison per round dealt.
        private void Deal(int slot)
        {
            BallType type = _nextType();

            if (type == default) throw new InvalidOperationException(
                "The magazine's next-colour policy answered the unused zero ball type; the queue may never empty.");

            _queue[slot] = type;
            _slotLoaded?.Invoke(slot, type);
        }

        /// <summary>
        /// Eases the post-shot glide towards the resting slots and snaps the last thousandth so it settles
        /// exactly. Call it every frame; it is cheap and returns at once with the queue at rest.
        /// <para>
        /// <b>Give it the wall clock, not the simulation's step.</b> The queue glides while the simulation is
        /// paused or slowed (the Testbed's F5/F9), because the balls sliding down a tube is the gun answering
        /// the shot and not something the physics is doing.
        /// </para>
        /// </summary>
        /// <param name="elapsedSeconds">The frame's own elapsed time. The ease is framed in seconds, so it does
        /// not change with the frame rate.</param>
        public void Step(float elapsedSeconds)
        {
            if (Slide <= 0f) return;

            Slide *= MathF.Exp(-elapsedSeconds / SLIDE_TAU);

            if (Slide < SLIDE_SETTLED) Slide = 0f;
        }

        /// <summary>The glide dropped with no ease — for a torn-down session, where there is nothing on screen
        /// for the queue to glide across.</summary>
        public void Settle() => Slide = 0f;

        /// <summary>
        /// Where the loaded queue sits this frame, as a value: the barrel's basis, the head-of-queue point on the
        /// bore and the direction back down it, taken once so the per-slot calls are arithmetic only.
        /// <para>
        /// Read <b>after</b> the gun has been updated — a pose taken before the barrel moves makes the queue lag
        /// a frame behind the tube it is supposed to be inside, which reads as jitter. All of it comes off
        /// <see cref="Cannon"/>'s own pose rather than being derived again here, so the balls cannot end up in a
        /// different bore from the one that is drawn.
        /// </para>
        /// </summary>
        /// <param name="cannon">The gun, already updated this frame.</param>
        /// <param name="pivotToFrontBall">How far ahead of the trunnions the head-of-queue ball sits —
        /// <see cref="CannonRig.PivotToFrontBall"/>, which the rig derives from the very
        /// <see cref="SIZE"/>/<see cref="SPACING"/> above.</param>
        /// <param name="recoilBack">The barrel's recoil stroke this instant, in world units back along the bore,
        /// 0 at rest. The queue rides <i>in</i> the bore, so pass the same value the barrel is drawn with or the
        /// balls float out of it. Drawing only: a shot leaves along the true aim on the frame it is fired, before
        /// any of this.</param>
        public BorePose Pose(Cannon cannon, float pivotToFrontBall, float recoilBack = 0f) =>
            //Three reads of the pose per frame rather than per ball, which is the whole reason this is taken once
            //and handed back as a value
            new(cannon.BarrelOrientation(), cannon.MuzzlePosition(pivotToFrontBall, recoilBack),
                cannon.AimDirection, Slide);
    }

    /// <summary>
    /// Where the loaded queue is this frame: one basis and one point on the bore, from which every slot's place
    /// is a multiply and an add. A readonly struct, so taking one per frame allocates nothing, and a value
    /// rather than a draw call, so each executable feeds its own instance collector with it.
    /// </summary>
    public readonly struct BorePose
    {
        //The barrel's own orientation, and it carries NO translation of its own (Matrix.CreateWorld about
        //Vector3.Zero) - which is what lets SlotWorld write a translation row instead of multiplying one in.
        private readonly Matrix _orientation;

        private readonly Vector3 _front;
        private readonly Vector3 _alongBore;
        private readonly float _slide;

        internal BorePose(Matrix orientation, Vector3 front, Vector3 alongBore, float slide)
        {
            _orientation = orientation;
            _front = front;
            _alongBore = alongBore;
            _slide = slide;
        }

        /// <summary>
        /// Where one loaded ball sits: <paramref name="slot"/> slots back from the muzzle along the bore, plus
        /// whatever of the post-shot glide is left — during the slide each ball is drawn <c>(slot + slide)</c>
        /// slots back, so it eases forward by one slot into the place the fired ball vacated.
        /// <para>
        /// All but the freshest, that is. The full displacement would deal the tail round a whole slot behind
        /// the tube — which the once-open breech used to absorb, and the dome that closed it would have the
        /// ball materialise through steel — so the last slot's share of the slide is clamped to the chamber
        /// the dome hides (<see cref="CannonRig.CHAMBER_DEPTH"/>): the round waits parked half a ball back,
        /// its back hemisphere nested in the cavity and its front hidden under the hood the slot stops short
        /// by, and pulls out into view once the ball ahead has glided clear of its slot. While both are under
        /// the hood the two interpenetrate, invisibly — the handoff at the clamp is seamless because the park
        /// depth, the hood's length and the cavity's depth are the same figure.
        /// </para>
        /// </summary>
        public Vector3 SlotPosition(int slot)
        {
            float slide = slot == Magazine.SIZE - 1
                ? MathF.Min(_slide, CannonRig.CHAMBER_DEPTH / Magazine.SPACING)
                : _slide;

            return _front - _alongBore * ((slot + slide) * Magazine.SPACING);
        }

        /// <summary>
        /// The matrix one loaded ball is drawn with, and its position, which the caller needs anyway to pick a
        /// LOD by distance.
        /// <para>
        /// The balls take the <b>barrel's own basis</b>, which is what stops them skewing: drawn unrotated they
        /// would hold a fixed world orientation while the barrel tilted around them, and the eye reads that
        /// mismatch as each ball twisting in its slot. The same basis the barrel itself is drawn with, so the two
        /// cannot drift apart.
        /// </para>
        /// <para>
        /// The translation is written straight into the fourth row rather than multiplied in. The orientation
        /// carries no translation of its own, so <c>orientation × CreateTranslation(p)</c> is <i>exactly</i> the
        /// orientation with that row set — bit-exact, not an approximation, and it saves a 4×4 multiply per ball
        /// per frame (BestPractices.md §6). What licenses it is that the orientation arrives from
        /// <see cref="Cannon.BarrelOrientation"/>, which builds it about <see cref="Vector3.Zero"/> and so leaves
        /// its fourth row <c>(0,0,0,1)</c>. The substitution would break silently — and only here, not in the
        /// barrel — if that method were ever given a translation of its own.
        /// </para>
        /// </summary>
        public Matrix SlotWorld(int slot, out Vector3 position)
        {
            position = SlotPosition(slot);

            Matrix world = _orientation;

            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;

            return world;
        }
    }
}
