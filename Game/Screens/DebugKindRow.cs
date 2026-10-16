using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The pause page's debug row</b> (#876): one button per kind of ball a player can fire, each loading that kind into
    /// the gun's bore, so a tester can try a wildcard or a cutter in the level at hand without waiting for a deal or
    /// spending a power-up's charge. The rules of the row, held apart from the widgets so they can be tested without a
    /// window: what it is made of, whether a build has it at all, and what a press does to the magazine.
    /// <para>
    /// <b>A dev build only, and the build's own word decides</b>: <see cref="Available"/> asks whether the build carries a
    /// release tag (<c>BuildVersion.Release</c>, #546), exactly as the Unlock all row does (#854) and for the same reason, not
    /// the Debug/Release configuration, because the owner plays Release-configuration builds of main and those keep the
    /// row. Every door takes the tag as an argument rather than reading it, which is what lets a test stand in a release:
    /// the page asks <see cref="Kinds"/> and draws nothing for an empty answer, and <see cref="Load"/> refuses on its own,
    /// so a caller that bypasses the page still loads nothing.
    /// </para>
    /// <para>
    /// <b>The kinds are read, not listed</b>: <see cref="BallKinds.ShootableKinds"/>, the kinds that exist only on the gun's
    /// side. A kind a level places is not among them, and not by omission: a shot lands by colour alone, so a bomb loaded into
    /// the bore would not keep its kind through the landing.
    /// </para>
    /// </summary>
    internal static class DebugKindRow
    {
        /// <summary>The caption over the row: plain about what it is and that it is not part of the game.</summary>
        internal const string CAPTION = "DEBUG: LOAD THE NEXT ROUND";

        /// <summary>
        /// The most buttons in one line of the row. The row is cut to the pause column's width, so each button gets a share of
        /// it, and a third kind starts a second line instead of narrowing every label until the longest name no longer fits - and
        /// before the slab is too narrow to carry the focus prompt (<c>BS3DGame.DrawFocusPrompt</c> draws none on a slab under a
        /// share of the column). Each line is as tall as a real entry, so the page holds only a few of them: at 1280x720 one
        /// line leaves about 100 px clear above PAUSED and under the strip, and each further line takes about 24 px of that
        /// from each side (the column is centred).
        /// </summary>
        internal const int MAX_PER_LINE = 2;

        /// <summary>Whether a build has the row at all: only one that carries no release tag.</summary>
        internal static bool Available(string release) => release is null;

        /// <summary>The kinds the row has a button for in this build: every shootable kind in a dev build, none in a release.</summary>
        internal static IReadOnlyList<BallKind> Kinds(string release) =>
            Available(release) ? BallKinds.ShootableKinds : Array.Empty<BallKind>();

        /// <summary>A button's words: the kind's own name, so a kind added to the enum labels itself.</summary>
        internal static string Name(BallKind kind) => kind.ToString();

        /// <summary>What the bore holds, for the line under the row: an ordinary round is "ordinary", any other the kind's name.</summary>
        internal static string Describe(BallKind kind) => kind == BallKind.Normal ? "ordinary" : Name(kind);

        /// <summary>How many buttons stand in each line of <paramref name="count"/> kinds.</summary>
        internal static int PerLine(int count) => Math.Clamp(count, 1, MAX_PER_LINE);

        /// <summary>
        /// A press: turns the round in the bore (the next one to fire) into a <paramref name="kind"/>, keeping its colour, its
        /// place and any dissolve it is part way through (<see cref="Magazine.SetKind"/>). Refused, and the magazine left as
        /// it was, in a release and for a kind a shot cannot carry. It touches nothing but the magazine: no charge is spent, no
        /// shot, no score and nothing in the save.
        /// </summary>
        /// <returns>Whether the round was changed.</returns>
        internal static bool Load(string release, BallKind kind, Magazine magazine)
        {
            if (!Available(release) || !BallKinds.Shootable(kind)) return false;

            magazine.SetKind(0, kind);
            return true;
        }
    }
}
