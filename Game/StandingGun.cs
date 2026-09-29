using Microsoft.Xna.Framework;
using Prazsky.BS3D;

namespace BS3D
{
    /// <summary>
    /// The gun a live session has standing on the island (#650): the one registration that says a gun exists,
    /// so that what <b>casts its shadow</b> and what <b>draws it</b> cannot disagree about whether there is one.
    /// <para>
    /// They were two: the host held a shadow hook (#470) that the session set when it was built and cleared when
    /// it was torn down, and the session's own <c>Draw</c> put the gun on the screen. That held while the session
    /// was the screen being drawn — and broke on the one path where it is not. The pause page's Main Menu keeps the
    /// session for Continue (#607) and puts the front end over the backdrop, whose <c>Draw</c> knew nothing of a
    /// gun: the hook still stood, so the island wore the shadow of a gun that was not there. Now the host asks
    /// this one object for all of it — the shadow pass (<see cref="CastShadow"/>) and the front end's frame
    /// (<see cref="CollectBalls"/>, <see cref="Draw"/>, <see cref="DrawGlass"/>) — so a session that stands
    /// stands drawn, wherever the lens is.
    /// </para>
    /// <para>
    /// The <b>host</b> owns the rig and the island; the <b>session</b> owns where the gun stands, which is why
    /// this is an interface the session implements and not a reach into the screen stack: the shadow pass runs
    /// at the top of <c>BeginSceneDraw</c>, before any screen has drawn. Set by the session's <c>BuildLevel</c>,
    /// cleared by its <c>TearDown</c>; no session, no gun, no shadow.
    /// </para>
    /// </summary>
    internal interface IStandingGun
    {
        /// <summary>The gun into the sun's shadow map (#470), from wherever it stands and however far its stroke has run.</summary>
        void CastShadow(Matrix shadowViewProjection);

        /// <summary>
        /// The rounds loaded in the bore, into the frame's ball collection — before the frame's
        /// <c>BeginSceneDraw</c>, like every other ball, because that call's shadow pass reads the buckets.
        /// </summary>
        void CollectBalls(in BallDrawFrame frame);

        /// <summary>The gun's opaque parts — tube, muzzle collar, carriage — in the setting's states, before the balls draw.</summary>
        void Draw();

        /// <summary>The glazing of the gun's loading window, which is translucent and so goes after everything it is seen against.</summary>
        void DrawGlass();
    }
}
