using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Precise aim — the lens leaning in over the gun's barrel while the player holds it (the right mouse button
    /// or the gamepad's left trigger). It exists because how much angle onto the field an overview can give is
    /// fixed by where the camera stands, while a lens looking <b>along</b> the aim sees whatever the barrel
    /// points at, head-on. It stood in both executables, every dial value-identical, until #76.
    /// <para>
    /// The whole piece is <b>pure</b>: it computes a pose and hands it back as a value, and it never touches a
    /// camera. That is not tidiness, it is the one thing the two callers genuinely do differently — the Testbed
    /// drives a <c>BasicCamera3D</c> through ordered setters whose order is mandatory (the <c>Target</c> setter
    /// rebuilds the view last, with world up), while the Game writes a base pose onto a <c>RecoilCamera</c> and
    /// lets the shake compose on top of it, and additionally goes on lerping the drop cinematic over the pose
    /// this returns. Neither could be served by a component that assigned to a camera itself.
    /// </para>
    /// <para>
    /// One reversible scalar, <see cref="Blend"/>, eases 0 → 1 and the pose is
    /// <c>Lerp(overview, leaned, Blend)</c> — so at 0 the overview pose comes back bit for bit and an
    /// interrupted hold never snaps. There is no state machine and nothing to latch.
    /// </para>
    /// </summary>
    public sealed class PreciseAim
    {
        /// <summary>How far behind the muzzle the lens sits, along <c>-aim</c>.</summary>
        public const float BACK = 6f;

        /// <summary>How far above the bore it is lifted, along <see cref="LensUp"/>.</summary>
        public const float RISE = 2f;

        //Nearest convergence depth, which keeps the look-at point off the barrel itself, and the farthest, which
        //stays well inside the far plane.
        public const float CONVERGE_MIN = 6f;
        public const float CONVERGE_MAX = 90f;

        /// <summary>Ease time constant in seconds (~90 % in ~0.18 s) — the magazine slide's own idiom.</summary>
        public const float BLEND_TAU = 0.08f;

        /// <summary>
        /// The convergence depth's own ease, and <b>deliberately not <see cref="BLEND_TAU"/></b> (#382): it is a
        /// different quantity answering a different event. The lean is a button the player pressed and wants to
        /// see happen; the depth follows a first-hit distance that <i>jumps</i> whenever the crosshair crosses a
        /// silhouette, with nobody having asked for anything. At the lean's 0.08 s such a jump reads as a flick,
        /// so this is slower — ~90 % in ~0.7 s, slow enough that crossing an edge is a drift the eye does not
        /// catch and still far faster than a player can re-aim across a field.
        /// </summary>
        public const float CONVERGE_TAU = 0.3f;

        /// <summary>Gamepad left-trigger pull that counts as held.</summary>
        public const float TRIGGER_THRESHOLD = 0.5f;

        /// <summary>
        /// A modest 1.19× lean-in on the game camera's own field of view — enough to read as leaning in, not
        /// enough to read as a scope.
        /// </summary>
        public static readonly float FOV = MathF.PI / 5f;

        /// <summary>
        /// Clearance the lens keeps over the stone directly below it. Aiming steeply up, <c>-aim</c> points
        /// downwards and the set-back would drop the lens through the stone island and show it from
        /// underneath; it is floored this far over the stone that is actually there
        /// (<see cref="ArenaIsland.FloorHeightAt"/> at the lens's own footprint — the island never moves, so
        /// a world position's XZ length is its radius on it), from where the bottom of the frame still looks
        /// upwards and the stone stays out of it. It used to be one fixed floor a unit over the island's
        /// arris plane, and since the gun stands on the island's <b>dish</b> no single height serves: that
        /// floor sat <i>above</i> the trunnions of a gun standing near the drain (the lean would have pinned
        /// against it and stopped following the barrel), while one cut low enough for the drain would let a
        /// lens near the arris sink into the higher stone there.
        /// </summary>
        public const float FLOOR_CLEARANCE = 1f;

        /// <summary>
        /// 0 is the exact overview pose, 1 is fully leaned in. Each executable's crosshair reads it — the Game
        /// draws its four bars only above 0.01 and fades them up with it, because the overview's screen centre
        /// points at nothing in particular.
        /// </summary>
        public float Blend { get; private set; }

        /// <summary>
        /// Eases <see cref="Blend"/> towards held or not-held and snaps the last thousandth at either end so it
        /// settles exactly.
        /// <para>
        /// <b>Call it every frame, held or not.</b> An unheld frame is how the lean eases back out, which is what
        /// makes losing focus a fade rather than a drop — and losing focus must clear the held flag, because
        /// XInput reports a trigger to an unfocused window and an alt-tabbed game would otherwise stay leaned in.
        /// Whether the window is focused is the caller's to decide: the Testbed rebuilds its held flag with an
        /// <c>IsActive</c> term every frame, the Game simply does not run its aim update while inactive and
        /// clears the flag in that branch. Same rule, two routes.
        /// </para>
        /// </summary>
        /// <param name="held">Whether precise aim is being asked for this frame, after every gate the caller
        /// wants on it (focus, a running cinematic, a loaded field, a mode animation).</param>
        /// <param name="elapsedSeconds">The frame's own elapsed time. Framed in seconds, so the ease does not
        /// change with the frame rate.</param>
        /// <param name="targetDepth">How far along the aim the lens should converge <i>this</i> frame, before
        /// easing and before clamping — see <see cref="ConvergeDepth"/> for what it is and why it is eased.</param>
        public void Step(bool held, float elapsedSeconds, float targetDepth)
        {
            float target = held ? 1f : 0f;

            Blend = target + (Blend - target) * MathF.Exp(-elapsedSeconds / BLEND_TAU);

            if (target == 0f && Blend < 0.002f) Blend = 0f;
            if (target == 1f && Blend > 0.998f) Blend = 1f;

            //Followed exactly while the lens is out, eased only while it is in. Both halves are load-bearing.
            //Out, the depth changes nothing that is drawn (the pose is Lerp(overview, leaned, 0)), so easing
            //there would only mean ARRIVING wrong: a hold begun after the aim had swept elsewhere would open
            //converged on where the player used to be pointing and slide, which is the one place the lean is
            //supposed to be exact. In, easing is the whole point — see ConvergeDepth.
            ConvergeDepth = Blend <= 0f
                ? targetDepth
                : targetDepth + (ConvergeDepth - targetDepth) * MathF.Exp(-elapsedSeconds / CONVERGE_TAU);
        }

        /// <summary>
        /// How far along the aim the lens converges, eased by <see cref="CONVERGE_TAU"/> and clamped where it is
        /// used. <b>The caller says what it should be; this only smooths it.</b>
        /// <para>
        /// It exists because the over-the-barrel lens sits back and above the bore, so the screen-centre
        /// crosshair is pixel-exact at exactly one depth and reads slightly off either side of it — and until
        /// #382 that depth was the whole level's <i>average</i>: the cluster centre projected onto the aim. Worst
        /// ~1.6° on the biggest map, and consistently slightly <b>low</b>, balls attaching at the near face. The
        /// Game now hands it the distance to what the shot preview actually found, which is a number it already
        /// computes in the same frame five lines earlier, so the error goes to zero <i>at the thing being aimed
        /// at</i> — the only place it was ever visible.
        /// </para>
        /// <para>
        /// <b>The ease is not polish, it is the whole risk of feeding it a real one.</b> A projected centre is
        /// smooth by construction: it slides continuously as the aim sweeps. A first-hit distance does not — cross
        /// a silhouette edge and it jumps from a near face to the far side of the field, or to nothing at all.
        /// Converging on a jumping depth swings the look-at, which is a camera flick on every edge the crosshair
        /// crosses, and that is worse than the 1.6° it fixes. Hence its own <see cref="CONVERGE_TAU"/>, and hence
        /// the caller's duty to hand back today's projection — never the clamp's ceiling — when the sweep finds
        /// nothing at all: an aim pointed at open sky has no impact to converge on.
        /// </para>
        /// </summary>
        public float ConvergeDepth { get; private set; } = CONVERGE_MAX;

        /// <summary>
        /// The depth a caller with only a cluster centre has: that centre projected onto the aim. It is what
        /// this component converged on for every frame before #382, and it stays the honest answer for two
        /// cases — the Testbed, which has no shot preview at all and derives the centre from the loaded map,
        /// and either caller on a frame whose sweep found nothing.
        /// </summary>
        public static float DepthToClusterCentre(Vector3 muzzle, Vector3 aim, Vector3 clusterCentre)
            => Vector3.Dot(clusterCentre - muzzle, aim);

        /// <summary>Back to the overview with no ease — for a torn-down session or a camera-mode exit.</summary>
        public void Reset()
        {
            Blend = 0f;

            //Back to the far end rather than to whatever the torn-down session was looking at: the next Step
            //while the lens is out will overwrite it with that caller's own target anyway, so this only has to
            //be a value no arithmetic can trip over.
            ConvergeDepth = CONVERGE_MAX;
        }

        /// <summary>
        /// Whether precise aim is being held: the right mouse button, or the gamepad's left trigger past
        /// <see cref="TRIGGER_THRESHOLD"/>. It is handed the frame's snapshots and never polls a device itself —
        /// one snapshot per device per frame is a repo-wide rule, and two reads in one frame can disagree.
        /// </summary>
        public static bool ButtonHeld(in MouseState mouse, in GamePadState pad) =>
            mouse.RightButton == ButtonState.Pressed || pad.Triggers.Left > TRIGGER_THRESHOLD;

        /// <summary>
        /// What this frame's cursor aim rate should be multiplied by, so that a hand movement sweeps the
        /// crosshair the same distance across the <b>screen</b> whether the lens is leaned in or not (#384).
        /// Without it the mode whose entire purpose is placing a shot precisely was the mode the cursor moved
        /// <i>fastest</i> in: <see cref="FOV"/> is a lean-in, so the same angle covers more of the picture.
        /// <para>
        /// <b>The ratio is of half-angle tangents rather than of the two fields of view</b>, because that is
        /// what the projection actually does — a pixel at the centre of the frame subtends an angle
        /// proportional to <c>tan(fov/2)</c>, not to <c>fov</c>. On this game's own pair the two readings are
        /// 0.828 and 0.840, so the plain ratio would be 1.4 % off; that is small, and it is also free to be
        /// right, and the tangent form stays right if either field of view is ever retuned.
        /// </para>
        /// <para>
        /// It rides <see cref="Blend"/> like everything else about the lean, so the rate is continuous through
        /// an interrupted hold and is <b>exactly 1</b> at a blend of zero — a caller that never leans in is
        /// multiplying by one, bit for bit, and aims exactly as it did before this existed.
        /// </para>
        /// <para>
        /// <b>Its one honest limit, measured rather than reasoned:</b> a rate scale multiplies the <i>angle</i>,
        /// while what lands on the screen is that angle's tangent, so the screen distances match exactly only in
        /// the limit and carry a third-order residual that grows with how far the hand moved. Measured against
        /// the real gun: <b>0.004 % out at a 10 px nudge, 0.15 % at a 60 px correction</b>, against the
        /// <b>20.8 %</b> that stood there before this existed and stood there at every distance. Remapping the
        /// cursor's position instead of scaling its rate would close that last 0.15 %, and would cost the
        /// property that makes this safe — being exactly 1, and therefore exactly today's aim, at rest.
        /// </para>
        /// </summary>
        /// <param name="overviewFov">The field of view the lean is leaning in <i>from</i> — the caller's own
        /// game-mode FOV, the same value it hands <see cref="BlendedPose"/>.</param>
        public float CursorRateScale(float overviewFov) =>
            MathHelper.Lerp(1f, LENS_HALF_TANGENT / MathF.Tan(overviewFov * Constants.HALF), Blend);

        //The leaned half-angle's tangent, which never changes; the overview's cannot be cached beside it,
        //because it is the caller's to state and the two executables are free to differ.
        private static readonly float LENS_HALF_TANGENT = MathF.Tan(FOV * Constants.HALF);

        /// <summary>
        /// The "up" the lens is lifted along: world up made perpendicular to the bore, so the lift is always
        /// straight over the barrel whatever the aim. This is the <b>lift</b> up only — the <b>view</b> up is
        /// plain world up, which the Testbed gets for free by setting its camera's target last.
        /// <para>
        /// Well conditioned across the gun's elevation clamp: at its ~80° ceiling the squared length is still
        /// ~0.03, far above the fallback threshold, which only trips within ~0.6° of vertical — so the
        /// horizontal-perpendicular fallback stays dead code unless that clamp is pushed almost to straight up.
        /// </para>
        /// </summary>
        public static Vector3 LensUp(Vector3 aim)
        {
            Vector3 up = Vector3.Up - aim * Vector3.Dot(Vector3.Up, aim);

            return up.LengthSquared() < 1e-4f ? Vector3.Normalize(new Vector3(aim.Z, 0f, -aim.X)) : Vector3.Normalize(up);
        }

        /// <summary>
        /// The leaned lens: back from the muzzle along the aim and lifted over the bore, with its Y floored
        /// <see cref="FLOOR_CLEARANCE"/> over the stone under it.
        /// </summary>
        public static Vector3 LensPosition(Vector3 muzzle, Vector3 aim)
        {
            Vector3 lens = muzzle - aim * BACK + LensUp(aim) * RISE;

            lens.Y = MathF.Max(lens.Y,
                ArenaIsland.FloorHeightAt(MathF.Sqrt(lens.X * lens.X + lens.Z * lens.Z)) + FLOOR_CLEARANCE);

            return lens;
        }

        /// <summary>
        /// Where the leaned lens looks: a point <b>on the shot ray</b>, so the screen-centre crosshair marks
        /// where the shot is actually directed. The depth is the cluster centre projected onto the aim and
        /// clamped, which centres the small over-the-barrel parallax over the region the impact face sweeps
        /// during a game.
        /// </summary>
        /// <param name="depth">How far along the aim to converge — <see cref="ConvergeDepth"/>, already eased.
        /// Clamped here rather than by the caller, so that every route in gets the same floor and ceiling: the
        /// floor keeps the look-at off the barrel itself, and a depth is a number any caller can get wrong.</param>
        public static Vector3 LensTarget(Vector3 muzzle, Vector3 aim, float depth)
            => muzzle + aim * MathHelper.Clamp(depth, CONVERGE_MIN, CONVERGE_MAX);

        /// <summary>
        /// This frame's pose: the overview and the leaned pose interpolated by <see cref="Blend"/>, position,
        /// look-at and field of view together. Returns a value and touches no camera — see the class remarks for
        /// why that is load-bearing rather than tidy. Allocates nothing: the result is a readonly struct and
        /// every step of it is stack arithmetic.
        /// </summary>
        /// <param name="muzzle">The muzzle this frame, taken <b>after</b> the gun has been updated — reading the
        /// pose before the gun moves makes the camera lag a frame, which reads as jitter.</param>
        public AimPose BlendedPose(Vector3 overviewPosition, Vector3 overviewTarget, float overviewFov,
            Vector3 muzzle, Vector3 aim)
        {
            //At a blend of exactly zero these are Lerp(a, b, 0) == a, bit for bit, so the overview pose comes
            //back untouched rather than approximately — which is what lets an interrupted hold not snap
            return new AimPose(
                Vector3.Lerp(overviewPosition, LensPosition(muzzle, aim), Blend),
                Vector3.Lerp(overviewTarget, LensTarget(muzzle, aim, ConvergeDepth), Blend),
                MathHelper.Lerp(overviewFov, FOV, Blend));
        }
    }

    /// <summary>
    /// The pose precise aim asks for this frame: a lens, a point to look at and a vertical field of view. A
    /// readonly struct, so handing one back per frame allocates nothing — and a value rather than a camera, so
    /// each caller applies it the way its own camera type requires and can go on composing over it.
    /// </summary>
    public readonly struct AimPose
    {
        public readonly Vector3 Position;
        public readonly Vector3 Target;
        public readonly float FieldOfView;

        public AimPose(Vector3 position, Vector3 target, float fieldOfView)
        {
            Position = position;
            Target = target;
            FieldOfView = fieldOfView;
        }
    }
}
