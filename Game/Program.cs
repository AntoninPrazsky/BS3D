using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BS3D
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool fullscreen = false;
            bool uncappedFps = false;
            float exposure = 0f;

            //Left null when absent, so the game keeps doing what it normally does: a random one of the twelve
            //scenes, and whatever dome that scene wants. Pinning both is what makes a frame-cost measurement
            //repeatable (see BS3DGame.LogFrameRate) — without it every run measures a different backdrop.
            SceneKind? scene = null;
            byte? skyDome = null;
            bool logFrameRate = false;

            //Null means "nobody said", so the adaptive path is free to measure this machine and step the tier
            //down. Naming one settles it, exactly as naming ssaa= does.
            QualityLevel? quality = null;

            //Left null when "ssaa=" is absent, which is how the game tells "the player wants two" from "nobody
            //said" — only the latter may be lowered for a machine that cannot afford the default
            int? supersampleFactor = null;

            //Testing only: start the victory display on the front end, which is otherwise reachable only by
            //clearing a level.
            bool celebrate = false;

            //Testing only: pin the floor alarm's laser net on. Reaching it honestly means playing a level to
            //within two ceiling steps of losing it, which can no more be scripted than clearing one can.
            bool lasers = false;

            //Testing only: start with the master volume at zero. A scripted screenshot or benchmark run has
            //no business making noise, and nothing is persisted, so there is no settings file to pre-set.
            bool mute = false;

            //Testing only: drop straight into the first level, skipping the title card and the menu. The
            //session's placement and physics write their figures to stdout only once a level is built, and
            //building one honestly takes a mouse on a Myra button — which a scripted run does not have.
            bool play = false;

            //Testing only: which level "play" should open, as a 1-based place in the set or as a name. Null
            //means the argument was absent, and then "play" opens the first level as it always has.
            string level = null;

            //Testing only: put a cleared level's result screen up at startup. A level's ending — the released
            //camera, the stars landing, the arena going out of focus — is otherwise only reachable by winning
            //or losing one, which cannot be scripted any more than clearing one can.
            bool result = false;

            //Testing only: make that result page a BLOCK milestone rather than an ordinary clear (#184). A block
            //is finished only when every level of a five-level chapter is cleared, so where a single clear merely
            //cannot be scripted, this cannot be reached at all without playing the campaign.
            bool blockDone = false;

            //Testing only: start the campaign's closing confetti (#215). Reaching it honestly means clearing the
            //last level of the whole set, which is further out of a script's reach than a block milestone is.
            bool confetti = false;

            //Testing only: the rating the "result" page reports, and so which of the four trophy cups it
            //presents (#183). Null leaves the authored three — the only one a test could otherwise reach.
            int? resultStars = null;

            //Testing only: wall-clock seconds at which the game saves a PNG of its own frame. Null means the
            //argument was absent, which is every run but a scripted one. F12 does the same thing by hand — but
            //only this trigger survives a LOCKED desktop, which takes no keystrokes at all (#191).
            float[] shotSeconds = null;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "fullscreen", StringComparison.OrdinalIgnoreCase)) fullscreen = true;
                //"nocap" disables vsync so real rendering headroom can be measured
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                //"ssaa=<n>" trades sharpness against fill rate; "exposure=<f>" is the renderer's shutter speed
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
                //"logfps" writes one frame-rate line a second to stdout; "scene="/"sky=" pin what is being
                //measured. The scene names are the Testbed's, so one benchmark script drives either executable.
                else if (string.Equals(arg, "logfps", StringComparison.OrdinalIgnoreCase)) logFrameRate = true;
                else if (arg.StartsWith("scene=", StringComparison.OrdinalIgnoreCase) && SceneRenderer.TryParseScene(arg.Substring("scene=".Length), out SceneKind parsedScene)) scene = parsedScene;
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky) && parsedSky >= 1 && parsedSky <= BS3DGame.SKY_DOME_COUNT) skyDome = parsedSky;
                //"quality=" pins the whole detail tier; "ssaa=" then overrides just its supersample entry.
                else if (arg.StartsWith("quality=", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(arg.Substring("quality=".Length), ignoreCase: true, out QualityLevel parsedQuality)) quality = parsedQuality;
                //"celebrate" fires the victory display at startup. Clearing a level is the only thing that
                //normally starts it, and clearing one cannot be scripted, so without this the fireworks can be
                //neither screenshotted nor measured — the same reason autoshoot and aimshoot exist.
                else if (string.Equals(arg, "celebrate", StringComparison.OrdinalIgnoreCase)) celebrate = true;
                //"confetti" starts the campaign's closing fall (#215) — one step further along that reasoning
                //than "blockdone": a block milestone needs five levels played, this needs the whole campaign.
                else if (string.Equals(arg, "confetti", StringComparison.OrdinalIgnoreCase)) confetti = true;
                //"stars=<0..4>" rates that result page, which is the only way to see the other three trophy
                //cups (#183): reaching a four-star clear honestly means being good at the game, not scripting it.
                else if (arg.StartsWith("stars=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("stars=".Length), out int parsedStars)) resultStars = parsedStars;
                //"lasers" pins the floor alarm's laser net on while a level is played, for the same reason.
                else if (string.Equals(arg, "lasers", StringComparison.OrdinalIgnoreCase)) lasers = true;
                //"mute" starts silent, for the harnesses; the settings rows can still raise it.
                else if (string.Equals(arg, "mute", StringComparison.OrdinalIgnoreCase)) mute = true;
                //"play" skips the front end into the first level, so a session's figures can be measured at all.
                else if (string.Equals(arg, "play", StringComparison.OrdinalIgnoreCase)) play = true;
                //"level=<n|name>" does the same for any entry of the set — its 1-based place, as the title bar
                //numbers it, or its name. "play" reaches the first level only, which is the lightest one there
                //is, so a frame with a real cluster in it could not be measured at all: #167 asked for a level
                //"near the shipped set's 959-ball end" and there was no way to ask for one.
                else if (arg.StartsWith("level=", StringComparison.OrdinalIgnoreCase)) level = arg.Substring("level=".Length);
                //"result" puts a cleared level's result screen up; with "celebrate" that is the whole
                //end-of-level moment, fireworks and all, over an arena that goes out of focus behind it.
                else if (string.Equals(arg, "result", StringComparison.OrdinalIgnoreCase)) result = true;
                //"blockdone" makes that page a BLOCK milestone rather than an ordinary clear (#184). One step
                //further along the same reasoning: finishing a block needs every level of a five-level chapter
                //cleared, so unlike a single clear it cannot be reached in a scripted run at all.
                else if (string.Equals(arg, "blockdone", StringComparison.OrdinalIgnoreCase)) blockDone = true;
                //"shot=<t1,t2,…>" saves a PNG of the frame at those wall-clock seconds. Parsed leniently on
                //purpose — a malformed entry is dropped and the rest stand, the way an unknown scene name
                //falls back rather than throwing: this is a diagnostic, and it must never be the reason a
                //scripted run fails to start.
                else if (arg.StartsWith("shot=", StringComparison.OrdinalIgnoreCase))
                    shotSeconds = ParseSeconds(arg.Substring("shot=".Length));
            }

            using var game = new BS3DGame(fullscreen: fullscreen, supersampleFactor: supersampleFactor, exposure: exposure,
                uncappedFps: uncappedFps, scene: scene, skyDome: skyDome, logFrameRate: logFrameRate, quality: quality,
                celebrate: celebrate, confetti: confetti, lasers: lasers, mute: mute, play: play, result: result, blockDone: blockDone, resultStars: resultStars,
                shotSeconds: shotSeconds, level: level);
            game.Run();
        }

        /// <summary>
        /// The <c>shot=</c> list: comma-separated seconds, invariant culture like every other numeric argument
        /// here. Kept in ASKED order rather than sorted — a caller who writes them out of order is telling the
        /// run something, and the schedule is walked forward — but an entry that will not parse is dropped
        /// rather than throwing, and an empty result comes back as null so the game sees "no schedule" instead
        /// of an empty one to test every frame.
        /// </summary>
        private static float[] ParseSeconds(string list)
        {
            string[] parts = list.Split(',');
            var seconds = new List<float>(parts.Length);

            foreach (string part in parts)
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= 0f)
                    seconds.Add(value);

            return seconds.Count > 0 ? seconds.ToArray() : null;
        }

        //The spellings scene= takes are SceneRenderer.TryParseScene's since #75 — the Testbed grew an if/else
        //chain, this a switch, and the two had to be kept in step by hand so that one benchmark or screenshot
        //script drives either executable unchanged. That is exactly the agreement one shared parse cannot break.
    }
}
