using Prazsky.BS3D.Levels;
using System;
using System.Text.Json.Nodes;

namespace BS3D.Screens
{
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// What a note sent from this level says about it (#813): which level and which version of its file, the
        /// phase it stood in, and the shots, the score and the seconds so far — what the author needs to find the
        /// moment again, since the words alone rarely say which level they are about.
        /// </summary>
        internal void DescribeForNote(JsonObject context)
        {
            LevelSet set = Game.LevelSet;
            LevelSetEntry entry = set != null && _run.Index >= 0 && _run.Index < set.Levels.Count ? set.Levels[_run.Index] : null;

            context["level"] = entry?.File;
            context["levelName"] = entry?.Name;
            context["levelNumber"] = _run.Index + 1;
            context["chapter"] = entry?.Block;
            context["levelHash"] = _run.Identity?.Hash;
            context["phase"] = _phase.ToString();
            context["shots"] = _run.Score.ShotsFired;
            context["score"] = _run.Score.Score;
            context["seconds"] = MathF.Round(_run.Seconds, 1);
        }
    }
}
