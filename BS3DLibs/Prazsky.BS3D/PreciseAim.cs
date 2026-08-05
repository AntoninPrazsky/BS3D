using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Render;
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
        public void Step(bool held, float elapsedSeconds)
        {
            float target = held ? 1f : 0f;

            Blend = target + (Blend - target) * MathF.Exp(-elapsedSeconds / BLEND_TAU);

            if (target == 0f && Blend < 0.002f) Blend = 0f;
            if (target == 1f && Blend > 0.998f) Blend = 1f;
        }

        /// <summary>Back to the overview with no ease — for a torn-down session or a camera-mode exit.</summary>
        public void Reset() => Blend = 0f;

        /// <summary>
        /// Whether precise aim is being held: the right mouse button, or the gamepad's left trigger past
        /// <see cref="TRIGGER_THRESHOLD"/>. It is handed the frame's snapshots and never polls a device itself —
        /// one snapshot per device per frame is a repo-wide rule, and two reads in one frame can disagree.
        /// </summary>
        public static bool ButtonHeld(in MouseState mouse, in GamePadState pad) =>
            mouse.RightButton == ButtonState.Pressed || pad.Triggers.Left > TRIGGER_THRESHOLD;

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
        /// <param name="clusterCentre">Where the hanging cluster's middle is. The caller's to supply and the
        /// two do it differently — the Testbed derives it from the loaded map, the Game reads a figure it
        /// solved once for the level — so this deliberately does not learn what a map is.</param>
        public static Vector3 LensTarget(Vector3 muzzle, Vector3 aim, Vector3 clusterCentre)
        {
            float depth = MathHelper.Clamp(Vector3.Dot(clusterCentre - muzzle, aim), CONVERGE_MIN, CONVERGE_MAX);

            return muzzle + aim * depth;
        }

        /// <summary>
        /// This frame's pose: the overview and the leaned pose interpolated by <see cref="Blend"/>, position,
        /// look-at and field of view together. Returns a value and touches no camera — see the class remarks for
        /// why that is load-bearing rather than tidy. Allocates nothing: the result is a readonly struct and
        /// every step of it is stack arithmetic.
        /// </summary>
        /// <param name="muzzle">The muzzle this frame, taken <b>after</b> the gun has been updated — reading the
        /// pose before the gun moves makes the camera lag a frame, which reads as jitter.</param>
        public AimPose BlendedPose(Vector3 overviewPosition, Vector3 overviewTarget, float overviewFov,
            Vector3 muzzle, Vector3 aim, Vector3 clusterCentre)
        {
            //At a blend of exactly zero these are Lerp(a, b, 0) == a, bit for bit, so the overview pose comes
            //back untouched rather than approximately — which is what lets an interrupted hold not snap
            return new AimPose(
                Vector3.Lerp(overviewPosition, LensPosition(muzzle, aim), Blend),
                Vector3.Lerp(overviewTarget, LensTarget(muzzle, aim, clusterCentre), Blend),
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
