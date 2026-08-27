using Prazsky.BS3D.GameStructure;
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

            //Testing only: "fpscap=N" is "nocap" with a ceiling — it presents immediately, so nothing
            //quantizes the reading, and idles out the rest of each frame's period, so a cheap frame does not
            //leave the card flat out. The Testbed has had it since #250; the Game needed it for #270, where a
            //vsync-capped level could only ever say "dearer than one refresh". Zero means no cap.
            int fpsCap = 0;
            float exposure = 0f;

            //Left null when absent, so the game keeps doing what it normally does: a random one of the fifteen
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

            //Testing only: which level the FRONT END should hang over the island instead of rolling one at
            //random (#254). Null means the argument was absent, and then the backdrop rolls as a player's does.
            string preview = null;

            //Testing only: open the level picker at boot, on a named chapter or on the one it chooses itself
            //(#273). Null means the argument was absent. Empty string means "pick" without a chapter, which is
            //why this is a string and not an int? — the two are different requests.
            string pick = null;

            //Testing only: draw every ball in one style whatever the level files say (#258). Null means the
            //argument was absent, and then each map is drawn in what it is authored in, as a player sees it.
            //It exists because the two styles can otherwise only be compared across two DIFFERENT levels —
            //and a look is judged by putting the same cluster, at the same stand-off, under the same dome,
            //in one material and then the other.
            BallStyle? ballStyle = null;

            //Testing only: put a cleared level's result screen up at startup. A level's ending — the released
            //camera, the stars landing, the arena going out of focus — is otherwise only reachable by winning
            //or losing one, which cannot be scripted any more than clearing one can.
            bool result = false;

            //Testing only: make that result page a BLOCK milestone rather than an ordinary clear (#184). A block
            //is finished only when every level of a five-level chapter is cleared, so where a single clear merely
            //cannot be scripted, this cannot be reached at all without playing the campaign.
            bool blockDone = false;
            bool lost = false;

            //Testing only: start the campaign's closing confetti (#215). Reaching it honestly means clearing the
            //last level of the whole set, which is further out of a script's reach than a block milestone is.
            bool confetti = false;

            //Testing only: the rating the "result" page reports, and so which of the four trophy cups it
            //presents (#183). Null leaves the authored three — the only one a test could otherwise reach.
            int? resultStars = null;

            //Testing only: what the HUD's multiplier readout shows (#180). The capped state takes five
            //consecutive scoring shots to reach honestly, so it is otherwise unphotographable.
            int? streak = null;

            //Testing only: wall-clock seconds at which the game saves a PNG of its own frame. Null means the
            //argument was absent, which is every run but a scripted one. F12 does the same thing by hand — but
            //only this trigger survives a LOCKED desktop, which takes no keystrokes at all (#191).
            float[] shotSeconds = null;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "fullscreen", StringComparison.OrdinalIgnoreCase)) fullscreen = true;
                //"nocap" lifts the frame limiter's ceiling so real rendering headroom can be measured. It meant
                //"disables vsync" until #270 — the game presents immediately in EVERY mode now, so all this
                //picks is FrameLimiter's target.
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) uncappedFps = true;
                //"fpscap=N" is that same ceiling set by hand, and it wins over "nocap" outright rather than
                //being reconciled with it (BS3DGame.FrameLimitHz), so the two cannot be given inconsistently.
                else if (arg.StartsWith("fpscap=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("fpscap=".Length), out int parsedCap) && parsedCap > 0) fpsCap = parsedCap;
                //"ssaa=<n>" trades sharpness against fill rate; "exposure=<f>" is the renderer's shutter speed
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) supersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) exposure = parsedExposure;
                //"logfps" writes one frame-rate line a second to stdout; "scene="/"sky=" pick the backdrop the
                //FRONT END hangs. They do NOT survive a level: a Level file names its own scene, dome and
                //weather and GameplayScreen applies all three over these, so "play level=X scene=meadow" draws
                //whatever X names. #270 read five runs as a scene comparison that were one scene measured
                //twice because of it. The [fps] line prints the LIVE scene for exactly this reason - it is the
                //authority, not this argument. The scene names are the Testbed's, so one benchmark script
                //drives either executable.
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
                //"streak=<n>" pins what the HUD's multiplier shows (#180) — the display only, never the
                //scoring, so the lever cannot alter the thing it is there to look at.
                else if (arg.StartsWith("streak=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("streak=".Length), out int parsedStreak)) streak = parsedStreak;
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
                //"lost" makes that page a FAILED level rather than a clear (#238). Without it the fail state was
                //unreachable from a test at all: "result" hardcoded a clear, so "stars=0" gave a starless CLEARED
                //page and nobody had ever looked at the reason line the player is told they lost by.
                else if (string.Equals(arg, "lost", StringComparison.OrdinalIgnoreCase)) lost = true;
                //"shot=<t1,t2,…>" saves a PNG of the frame at those wall-clock seconds. Parsed leniently on
                //purpose — a malformed entry is dropped and the rest stand, the way an unknown scene name
                //falls back rather than throwing: this is a diagnostic, and it must never be the reason a
                //scripted run fails to start.
                else if (arg.StartsWith("shot=", StringComparison.OrdinalIgnoreCase))
                    shotSeconds = ParseSeconds(arg.Substring("shot=".Length));
                //"pick" puts the LEVEL PICKER up at boot, and "pick=<n>" puts it up on that chapter (#273). The
                //page itself is two keypresses away for anyone sitting at the machine and unreachable on a
                //locked desktop, which takes no keystrokes — and since #273 it is a pager, so its other eight
                //chapters are several presses in and "a shot of the picker" means nothing without saying which.
                else if (string.Equals(arg, "pick", StringComparison.OrdinalIgnoreCase)) pick = string.Empty;
                else if (arg.StartsWith("pick=", StringComparison.OrdinalIgnoreCase)) pick = arg.Substring("pick=".Length);
                //"preview=<n|name>" pins which map the FRONT END hangs, the way "level=" pins which one is
                //played. The menu's camera is framed for that map since #254, so without this two shots of
                //the front end are two shots of different maps at different stand-offs.
                else if (arg.StartsWith("preview=", StringComparison.OrdinalIgnoreCase)) preview = arg.Substring("preview=".Length);
                //"balls=<beach|bubble>" overrides what every level says it is made of (#258), the way "scene="
                //overrides the backdrop each one names. Parsed through BallStyles, so the spellings are the
                //ones a level file takes; an unknown one leaves the levels' own answers standing.
                else if (arg.StartsWith("balls=", StringComparison.OrdinalIgnoreCase)
                    && BallStyles.TryParse(arg.Substring("balls=".Length), out BallStyle parsedStyle)) ballStyle = parsedStyle;
            }

            using var game = new BS3DGame(fullscreen: fullscreen, supersampleFactor: supersampleFactor, exposure: exposure,
                uncappedFps: uncappedFps, scene: scene, skyDome: skyDome, logFrameRate: logFrameRate, quality: quality,
                celebrate: celebrate, confetti: confetti, lasers: lasers, mute: mute, play: play, result: result, blockDone: blockDone, lost: lost, resultStars: resultStars, streak: streak,
                shotSeconds: shotSeconds, level: level, preview: preview, ballStyle: ballStyle, pick: pick, fpsCap: fpsCap);
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
