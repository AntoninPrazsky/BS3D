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
    /// <see cref="Peek()"/> and <see cref="Slot"/> read (by value), <see cref="Advance"/> shifts,
    /// <see cref="Recolour"/> re-colours one slot, and there is no indexer to write through.
    /// </para>
    /// <para>
    /// <b>What loads next is injected, and neither policy lives here.</b> The Testbed draws uniformly from all
    /// thirteen types — its cluster is whatever map was dropped on it. The Game draws only among the colours
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
    /// this type's business. And <i>which</i> loaded colours have died and what replaces them, which is a rule
    /// about the Game's cluster — that caller decides and calls <see cref="Recolour"/>.
    /// </para>
    /// <para>
    /// <b>What a slot carries is one value, <see cref="MagazineSlot"/>, since #582</b>: its colour, its kind (a
    /// wildcard, #330), the colour it is dissolving out of and how far through that dissolve it is. Until then
    /// the colour lived here and the other three in three parallel arrays on <c>GameplayScreen</c>, kept in step
    /// by three callbacks this type fired on every shift, deal and swap — and the #392 swap is the record of those
    /// coming apart once, when a swap wired as two one-way copies left both slots holding the same state. A shift
    /// or a swap now moves one struct, so there is nothing left to keep in step; the kind is dealt through a
    /// second injected policy beside the colour, and the dissolve's clock runs in <see cref="Step"/>.
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

        /// <summary>
        /// How long a re-coloured ball takes to change colour, in seconds. Slow enough to be unmistakably seen —
        /// the whole point is that the player watches the game help them, and a snap would read as a bug — and
        /// short enough not to hold up a queue the player is aiming with. The Game's figure, and the Game is the
        /// only caller of <see cref="Recolour"/>; it moved here with the countdown it paces (#582).
        /// </summary>
        public const float TRANSMUTE_SECONDS = 0.75f;

        private readonly MagazineSlot[] _slots = new MagazineSlot[SIZE];

        private readonly Func<BallType> _nextType;
        private readonly Func<BallKind> _nextKind;

        /// <param name="nextType">What to load, asked once per dealt slot. Must answer a real
        /// <see cref="BallType"/> — see the class remarks on the invariant. Each executable's own policy; see the
        /// class remarks on why neither lives here.</param>
        /// <param name="nextKind">What kind the dealt ball is, asked once per dealt slot straight after
        /// <paramref name="nextType"/> — the Game's wildcard cadence (#330), which counts the balls dealt. Null
        /// deals every ball <see cref="BallKind.Normal"/>, which is the Testbed.</param>
        /// <remarks>
        /// Both policies are handed over <b>once</b>, here, and not per shot: a delegate built at the call site
        /// of every advance would allocate one per round fired. The constructor deals a full queue before it
        /// returns, so whatever state the policies read has to exist by then — the caller's own initialisation
        /// order to get right.
        /// </remarks>
        public Magazine(Func<BallType> nextType, Func<BallKind> nextKind = null)
        {
            _nextType = nextType ?? throw new ArgumentNullException(nameof(nextType));
            _nextKind = nextKind;

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
        public BallType Peek() => _slots[0].Type;

        /// <summary>
        /// The colour loaded in one slot, 0 at the muzzle to <see cref="SIZE"/> − 1 at the breech. For the draw
        /// loop and for the Game's transmute pass, which asks every slot whether its colour is still alive.
        /// </summary>
        public BallType Peek(int slot) => _slots[slot].Type;

        /// <summary>
        /// Everything one slot carries — colour, kind and the dissolve it may be part way through — as a value,
        /// so a caller can read it but never write through it.
        /// </summary>
        public MagazineSlot Slot(int slot) => _slots[slot];

        /// <summary>
        /// Deals a whole fresh queue and settles it. Run at the start of a session, and in the Game on every
        /// level load — its colours belong to a level, and the level whose cluster they were drawn from is gone.
        /// Every slot is dealt whole, so nothing starts part way through a dissolve left over from the session
        /// before it.
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
        /// <b>A slot moves whole.</b> The colour, the kind, the colour it is dissolving out of and how far through
        /// that dissolve it is are one <see cref="MagazineSlot"/>, so shifting the queue cannot leave a slot
        /// dissolving out of the ball behind it, nor a wildcard in the slot it was dealt into while its ball
        /// moves on (docs/game-session.md pins both). Until #582 the last three were the Game's parallel arrays,
        /// carried along this loop by a callback per <c>(destination, source)</c> pair.
        /// </para>
        /// <para>
        /// The fresh tail is dealt whole too: a ball drawn from what is alive right now has nothing to fade out
        /// of. And the countdown runs <b>1 → 0</b>, so the dissolve's progress is its <i>complement</i>
        /// (<see cref="MagazineSlot.TransmuteProgress"/>) — a caller feeding the countdown straight into
        /// <c>ModelInstance.Dissolve</c> runs the effect backwards, the new colour arriving complete on the frame
        /// of the swap with the old one never seen at all. That is exactly what the first build did and only a
        /// zoomed screenshot caught it.
        /// </para>
        /// </summary>
        public void Advance()
        {
            for (int slot = 0; slot < SIZE - 1; slot++) _slots[slot] = _slots[slot + 1];

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
        /// <b>It starts the cross-fade the caller draws</b>: the slot's countdown is set to 1 and runs down over
        /// <see cref="TRANSMUTE_SECONDS"/> in <see cref="Step"/>. The colour it fades <i>out of</i> is whatever is
        /// on screen now — which for a slot caught mid-transmute is the colour it was already fading out of, not
        /// the one it never finished becoming. Restarting from the visible colour is what keeps the animation
        /// continuous. The slot's kind is left alone.
        /// </para>
        /// </summary>
        /// <param name="slot">The slot to re-colour, 0 at the muzzle.</param>
        /// <param name="type">The replacement colour. Must be a real one — the class remarks say what a zero
        /// does, which is nothing, silently.</param>
        public void Recolour(int slot, BallType type)
        {
            if (type == default) throw new ArgumentOutOfRangeException(nameof(type),
                "A magazine slot cannot hold the unused zero ball type; the queue may never empty.");

            MagazineSlot current = _slots[slot];
            BallType fadingFrom = current.Transmute <= 0f ? current.Type : current.FadingFrom;

            _slots[slot] = new MagazineSlot(type, current.Kind, fadingFrom, 1f);
        }

        /// <summary>
        /// Exchanges two loaded slots' colours in place — a power-up's own operation (#392, <c>PowerupKind.Swap</c>
        /// being its first), and the one thing <see cref="Advance"/> and <see cref="Recolour"/> do not offer:
        /// neither reorders the queue. The "never empty" invariant is untouched, because a swap only ever
        /// exchanges two already-valid slots, and the slide is left alone — nothing here glides, both balls are
        /// already exactly where they sit in the bore.
        /// <para>
        /// The two slots are exchanged <b>whole</b> — the dissolve each is part way through and its kind travel
        /// with its colour. That is the #392 fault made unrepresentable: while those lived in the Game's parallel
        /// arrays, a swap wired as two one-way copies left both slots holding the same state rather than
        /// exchanged. A no-op on <c>a == b</c>: nothing has actually moved.
        /// </para>
        /// </summary>
        /// <param name="a">One slot, 0 at the muzzle.</param>
        /// <param name="b">The other.</param>
        public void SwapSlots(int a, int b)
        {
            if (a == b) return;

            (_slots[a], _slots[b]) = (_slots[b], _slots[a]);
        }

        //Loads one slot from the injected policies. The one place a slot's colour is written by anything but the
        //transmute, so it is the one place the invariant can be enforced: a policy that answered the unused zero
        //would put a ball in the barrel that draws as nothing at all, and it would do it without a word - see
        //the class remarks. One comparison per round dealt. The colour is asked before the kind, the order the
        //Game's hooks asked them in before #582, so the random draws behind both come in the same order.
        private void Deal(int slot)
        {
            BallType type = _nextType();

            if (type == default) throw new InvalidOperationException(
                "The magazine's next-colour policy answered the unused zero ball type; the queue may never empty.");

            BallKind kind = _nextKind?.Invoke() ?? BallKind.Normal;

            //A ball dealt from what is alive has nothing to fade out of
            _slots[slot] = new MagazineSlot(type, kind, type, 0f);
        }

        /// <summary>
        /// Eases the post-shot glide towards the resting slots and snaps the last thousandth so it settles
        /// exactly, and runs every re-coloured slot's dissolve down towards settled. Call it every frame; it is
        /// cheap, and a queue at rest costs a comparison per slot.
        /// <para>
        /// <b>Give it the wall clock, not the simulation's step.</b> The queue glides while the simulation is
        /// paused or slowed (the Testbed's F5/F9), because the balls sliding down a tube is the gun answering
        /// the shot and not something the physics is doing — and a ball changing colour in the bore is the gun
        /// saying what is loaded, for the same reason.
        /// </para>
        /// </summary>
        /// <param name="elapsedSeconds">The frame's own elapsed time. Both the ease and the dissolve are framed
        /// in seconds, so neither changes with the frame rate.</param>
        public void Step(float elapsedSeconds)
        {
            //Linear, so a dissolve genuinely finishes rather than leaving a slot for ever a few pixels short of its
            //new colour. First, because the glide below returns early once the queue is at rest.
            for (int slot = 0; slot < SIZE; slot++)
            {
                MagazineSlot s = _slots[slot];
                if (s.Transmute <= 0f) continue;

                _slots[slot] = new MagazineSlot(s.Type, s.Kind, s.FadingFrom,
                    MathF.Max(0f, s.Transmute - elapsedSeconds / TRANSMUTE_SECONDS));
            }

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
        public BorePose Pose(Cannon cannon, float pivotToFrontBall) =>
            //Three reads of the pose per frame rather than per ball, which is the whole reason this is taken
            //once and handed back as a value. The DRAWN muzzle, recoil and all (#115): the queue rides in the
            //bore that is drawn, and taking the pose off the gun's own state — rather than off a scalar every
            //caller had to remember to pass — is what makes the balls and the tube unable to disagree.
            new(cannon.BarrelOrientation(), cannon.DrawnMuzzlePosition(pivotToFrontBall),
                cannon.AimDirection, Slide);
    }

    /// <summary>
    /// One loaded round, as <see cref="Magazine"/> holds it (#582): what colour it is, what kind, and the
    /// cross-fade it may be part way through. A readonly value, so the magazine moves a slot by assigning one
    /// and a caller can only read it — sixteen bytes, and no allocation anywhere in its life.
    /// </summary>
    public readonly struct MagazineSlot
    {
        /// <summary>Builds one slot's state. <see cref="Magazine"/> is the only writer.</summary>
        /// <param name="type">The colour it fires as (a wildcard's is dealt and never seen, #330).</param>
        /// <param name="kind">What it is — <see cref="BallKind.Wildcard"/> or <see cref="BallKind.Normal"/>.</param>
        /// <param name="fadingFrom">The colour it is dissolving out of; its own colour when settled.</param>
        /// <param name="transmute">The dissolve's countdown, 1 just re-coloured to 0 settled.</param>
        public MagazineSlot(BallType type, BallKind kind, BallType fadingFrom, float transmute)
        {
            Type = type;
            Kind = kind;
            FadingFrom = fadingFrom;
            Transmute = transmute;
        }

        /// <summary>The colour loaded in this slot, and the one that fires — never the one it is fading out of.</summary>
        public BallType Type { get; }

        /// <summary>What this ball is (#330): a wildcard rides the queue as a kind, never as a colour.</summary>
        public BallKind Kind { get; }

        /// <summary>The colour a re-coloured ball is dissolving <i>out of</i>; equal to <see cref="Type"/> once
        /// settled, and meaningless while <see cref="Transmute"/> is 0.</summary>
        public BallType FadingFrom { get; }

        /// <summary>
        /// How far the dissolve still has to go: <b>1</b> on the frame of the re-colour, running down to <b>0</b>
        /// (settled, nothing to draw twice) over <see cref="Magazine.TRANSMUTE_SECONDS"/>. A <i>countdown</i> —
        /// see <see cref="TransmuteProgress"/> for the value the dissolve itself takes.
        /// </summary>
        public float Transmute { get; }

        /// <summary>The dissolve's own progress, the complement of <see cref="Transmute"/>: 0 just re-coloured,
        /// 1 settled. Feeding the countdown in its place runs the effect backwards.</summary>
        public float TransmuteProgress => 1f - Transmute;
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
