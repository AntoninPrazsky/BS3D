using BS3D.Lighting;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.Levels;
using System;

namespace BS3D.Screens
{
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// What the backlit keyboard shows while this level is played (#987): the round in the bore, the magazine's next balls in
        /// <paramref name="queue"/> (next first), and the keys the tutorial's card is asking for. The tints are the ones the muzzle
        /// collar and the aim beam take (<see cref="LoadedTint"/>), so a wildcard's keys cycle with it and a cutter's are the Cut's,
        /// and the queue stops where the strip on screen stops, at the shots the budget has left.
        /// </summary>
        internal void FillKeyLights(ref KeyLightInput input, Vector3[] queue)
        {
            int shown = MagazineBallsShown;

            input.HasLoaded = shown > 0;
            input.Loaded = shown > 0 ? LoadedTint(0) : Vector3.Zero;

            int next = Math.Clamp(shown - 1, 0, queue.Length);
            for (int i = 0; i < next; i++) queue[i] = LoadedTint(i + 1);
            input.QueueCount = next;

            input.TutorialKeys = KeyLightModel.KeysFor(_tutorial.AskingLesson, _tutorial.Step);
        }

        /// <summary>
        /// How the level ended, for the keyboard's ending (#987): cleared and its stars, or false while it is still being played.
        /// <para>
        /// <b>From the moment the level is decided</b> (#1258), not from its record: the field emptied (<see cref="LevelPhase.ClearedBeat"/>)
        /// or the cluster crossed the line (<see cref="LevelPhase.LossHold"/>). Before #1258 the keyboard stayed in play through the
        /// cleared beat and flashed the round in the bore as the spare volley fired the magazine off, the owner's note.
        /// </para>
        /// <para>
        /// The stars come off the record the result page is built from, so the keyboard and the page cannot disagree, and
        /// <paramref name="final"/> says whether it has been made: a clear's stars are not known before, since the volley still adds its
        /// balls to the score.
        /// </para>
        /// </summary>
        internal bool KeyLightEnding(out bool cleared, out int stars, out bool final)
        {
            if (_endingRecord is EndingRecord record)
            {
                cleared = record.Cleared;
                stars = record.Stars;
                final = true;
                return true;
            }

            cleared = _phase == LevelPhase.ClearedBeat;
            stars = 0;
            final = false;
            return _phase is LevelPhase.ClearedBeat or LevelPhase.LossHold;
        }
    }
}
