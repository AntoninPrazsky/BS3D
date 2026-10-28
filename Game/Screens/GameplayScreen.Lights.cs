using BS3D.Lighting;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
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
            int shown = _run.Score.ShotsRemaining is int shotsLeft ? Math.Min(Magazine.SIZE, shotsLeft) : Magazine.SIZE;

            input.HasLoaded = shown > 0;
            input.Loaded = shown > 0 ? LoadedTint(0) : Vector3.Zero;

            int next = Math.Clamp(shown - 1, 0, queue.Length);
            for (int i = 0; i < next; i++) queue[i] = LoadedTint(i + 1);
            input.QueueCount = next;

            input.TutorialKeys = KeyLightModel.KeysFor(_tutorial.AskingLesson, _tutorial.Step);
        }

        /// <summary>
        /// How the level ended, for the keyboard's ending (#987): cleared and its stars, or false while it has not ended. Read off the
        /// record the result page was built from, so the keyboard and the page cannot disagree.
        /// </summary>
        internal bool KeyLightEnding(out bool cleared, out int stars)
        {
            cleared = _endingRecord?.Cleared == true;
            stars = _endingRecord?.Stars ?? 0;

            return _endingRecord != null;
        }
    }
}
