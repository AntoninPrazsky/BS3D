using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// Everything the Testbed's command line can say, parsed once. It was fourteen locals in <c>Program.Main</c>
    /// handed to fourteen constructor parameters by name (#73), which is one list of the same thing written
    /// three times — the parse, the argument list and the fields it lands in — and a new switch had to be added
    /// to all three.
    /// <para>
    /// <b>Every string here is a contract, not a convenience.</b> <c>.claude/skills/verify</c>,
    /// <c>.claude/skills/screenshot</c> and its <c>screenshot.ps1</c> drive this executable by these exact
    /// argument spellings, so the surface is documented there as much as here and renaming one silently breaks
    /// a script rather than failing a build. What the parse does with an argument it cannot read is leave the
    /// default standing, deliberately: a mistyped <c>ssaa=x</c> should still open a window to look at.
    /// </para>
    /// </summary>
    public sealed class TestOptions
    {
        /// <summary>Map or level to load right after startup — the one bare argument, taken last.</summary>
        public string StartupMapPath { get; private set; }

        /// <summary>
        /// <c>switchmap=&lt;path&gt;</c>: load this on top of the running map after
        /// <see cref="SwitchMapDriver.DELAY_SECONDS"/>, which is what exercises the re-load path F2 and
        /// drag-and-drop take.
        /// </summary>
        public string SwitchMapPath { get; private set; }

        /// <summary><c>autoshoot</c>: fire at a random spot of the structure once a second and log a line.</summary>
        public bool AutoShoot { get; private set; }

        /// <summary><c>aimcheck</c>: log, per loaded map, whether the clamps let the gun aim at every cell.</summary>
        public bool AimCheck { get; private set; }

        /// <summary>
        /// <c>aimshoot</c>: enter game mode and fire a scan up the field's centre column and at its top corners.
        /// It <b>implies <see cref="AimCheck"/></b> — the sweep is only worth reading against the clamp report.
        /// </summary>
        public bool AimShoot { get; private set; }

        /// <summary><c>nocap</c>: no vsync, so real rendering headroom can be measured.</summary>
        public bool UncappedFps { get; private set; }

        /// <summary>
        /// <c>fpscap=N</c>: vsync off, but never more than N frames a second — the way to measure on a machine
        /// that must not be left rendering flat out. <see cref="UncappedFps"/> on its own lets a cheap scene run
        /// at thousands of frames a second, and that load is what the owner reported crashing his desktop
        /// outright and forcing a restart (docs/agent-notes.md, and #250's own "to the edge of a BSOD"). Under a
        /// cap a frame LIGHTER than the period idles between presents; a frame HEAVIER than it is never delayed
        /// and still reads its true cost. So set the cap under the frame rate being measured — at 150 anything
        /// dearer than 6.7 ms comes out exact — and read the cap itself as "cheaper than this", not as a cost.
        /// <para>
        /// Implies <see cref="UncappedFps"/>: a capped run still has to present immediately, or the vsync wait
        /// quantizes every reading to the refresh over an integer and the cap has nothing left to measure.
        /// </para>
        /// </summary>
        public int FpsCap { get; private set; }

        /// <summary>
        /// <c>logfps</c>: one <c>[fps]</c> line a second to stdout, in the Game's own wording — so
        /// <c>.claude/skills/benchmark</c>'s script drives either executable and their numbers are comparable.
        /// Until this existed the Testbed's only frame-rate line came out of <c>autoshoot</c>, which fires a
        /// ball a second to produce it, and the overlay's counter freezes whenever the overlay is hidden.
        /// </summary>
        public bool LogFrameRate { get; private set; }

        /// <summary>
        /// <c>nopost</c>: zero the film grain and the chromatic aberration, so a screenshot shows what the
        /// scene shaders actually drew. Both sit on top of every pixel after the tonemap — the grain re-rolls
        /// per output pixel every frame, which makes two captures of an unchanged scene differ almost
        /// everywhere, and the aberration splits high-contrast edges towards the frame's periphery. They are
        /// part of the authored image, so judge the <i>final</i> look with them back on.
        /// </summary>
        public bool NoPostEffects { get; private set; }

        /// <summary>
        /// <c>arena=&lt;list&gt;</c>: which members of the arena are drawn (see <see cref="ArenaMembers"/>),
        /// so #151's isolation — take one member out of the frame and measure again — can be run at all.
        /// Everything, as the game draws it, unless the argument says otherwise.
        /// </summary>
        public ArenaMembers Arena { get; private set; } = ArenaMembers.All;

        /// <summary>
        /// <c>capprobe=&lt;1..6&gt;</c>: #151 PROBE - TEMPORARY. Draws the stone cap through one of the
        /// cut-down copies of its pixel shader instead of the shipped one, so the cap's own per-pixel cost
        /// can be split up (the members sweep can only turn the whole cap off). 0 = the shipped shader.
        /// See the probe's header in <c>InstancedModel.fx</c> for what each number leaves out.
        /// </summary>
        public int CapProbe { get; private set; }

        /// <summary>
        /// <c>alt=&lt;members&gt;[/&lt;probe&gt;];&lt;members&gt;[/&lt;probe&gt;];…</c>: #151 PROBE - TEMPORARY.
        /// Redraws the arena a different way on each <c>[fps]</c> window, cycling the listed variants, so a
        /// sweep's variants are measured <b>inside one process under one clock</b> rather than as separate
        /// runs. Each variant is an <c>arena=</c> member list, optionally <c>/N</c> for
        /// <see cref="CapProbe"/>: <c>alt=all;none</c>, <c>alt=all/0;all/6</c>, <c>alt=all;all,-cap;none</c>.
        /// <para>
        /// <b>It exists because the weak machine cannot be measured any other way.</b> The reference desktop
        /// can compare two runs — #151's own cap sweep did, and its paired figures agreed to 0.03 ms. This
        /// project's integrated-Radeon laptop cannot: it shares one 15 W package budget between the CPU and
        /// the iGPU, so its sustained clock moves with whatever else the machine is doing, and two runs of the
        /// <i>same</i> variant came 33.6 and 25.7 ms apart — a spread larger than the arena itself. Alternating
        /// inside one process is the same step <see cref="CapProbe"/> took for the same reason (one build drawn
        /// several ways, rather than several builds measured against each other), carried one further to one
        /// <i>run</i> drawn several ways. Both members and probe are live properties on
        /// <see cref="ArenaIsland"/>, so a switch costs two assignments and nothing carries over between them.
        /// </para>
        /// </summary>
        public IReadOnlyList<ArenaVariant> Alternation { get; private set; } = Array.Empty<ArenaVariant>();

        /// <summary>One entry of <see cref="Alternation"/>: what the arena is drawn as for one window.</summary>
        public readonly struct ArenaVariant
        {
            public readonly ArenaMembers Members;
            public readonly int CapProbe;

            public ArenaVariant(ArenaMembers members, int capProbe)
            {
                Members = members;
                CapProbe = capProbe;
            }
        }

        /// <summary><c>sky=&lt;n&gt;</c>: the starting dome, pinned over a startup level's own. 0 = unset.</summary>
        public byte SkyNumber { get; private set; }

        /// <summary><c>ssaa=&lt;n&gt;</c>: supersample factor, clamped to 1–4 by the game itself.</summary>
        public int SupersampleFactor { get; private set; } = 2;

        /// <summary><c>exposure=&lt;f&gt;</c>: the renderer's shutter speed. 0 = unset, so the default stands.</summary>
        public float Exposure { get; private set; }

        /// <summary><c>scene=&lt;name&gt;</c>: the starting backdrop, through <c>SceneRenderer.TryParseScene</c>.</summary>
        public string Scene { get; private set; }

        /// <summary>
        /// <c>weather=&lt;clear|scattered|broken|overcast|storm&gt;</c>: the sky over the starting scene,
        /// overriding whatever that scene asks for (#221). Testing only, and it exists for one reason the
        /// scene defaults cannot serve: judging five skies means seeing them over the SAME backdrop under
        /// the SAME dome, and every other route ties the weather to the scene that wanted it.
        /// </summary>
        public string Weather { get; private set; }

        /// <summary><c>campos=x,y,z</c>: place the free camera at startup, so a shot can be framed from anywhere.</summary>
        public Vector3? CamPos { get; private set; }

        /// <summary><c>camtarget=x,y,z</c>: aim it. Defaults to the arena at the origin.</summary>
        public Vector3? CamTarget { get; private set; }

        /// <summary>
        /// <c>width=</c>/<c>height=</c>: the windowed back buffer, so a screenshot can be captured at play
        /// resolution from any machine (#141). The 16:9 default is the narrowest aspect the game targets, so
        /// what is framed in a window is the tightest case and a wider display only adds width.
        /// </summary>
        public int WindowWidth { get; private set; } = 1600;

        /// <inheritdoc cref="WindowWidth"/>
        public int WindowHeight { get; private set; } = 900;

        /// <summary>Not a command-line switch — F11 toggles it, and the program starts windowed.</summary>
        public bool Windowed { get; private set; } = true;

        /// <summary>
        /// Reads the switches out of <c>argv</c>. Order does not matter and an unreadable value is ignored;
        /// anything that matches no prefix is taken as the startup map path, so the last bare argument wins.
        /// </summary>
        public static TestOptions Parse(string[] args)
        {
            TestOptions options = new();

            foreach (string arg in args)
            {
                if (string.Equals(arg, "autoshoot", StringComparison.OrdinalIgnoreCase)) options.AutoShoot = true;
                //"aimcheck" logs per map whether the cannon can aim at every cell; "aimshoot" auto-enters
                //game mode and fires up the field's centre column so the aim + shoot path is exercised end to end
                else if (string.Equals(arg, "aimcheck", StringComparison.OrdinalIgnoreCase)) options.AimCheck = true;
                else if (string.Equals(arg, "aimshoot", StringComparison.OrdinalIgnoreCase)) { options.AimShoot = true; options.AimCheck = true; }
                else if (string.Equals(arg, "nocap", StringComparison.OrdinalIgnoreCase)) options.UncappedFps = true;
                //"fpscap=N" is nocap with a ceiling: it presents immediately (so nothing quantizes the
                //reading) but idles out the rest of the period, which keeps a cheap scene from running the
                //card flat out at thousands of frames a second. See TestOptions.FpsCap.
                else if (arg.StartsWith("fpscap=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("fpscap=".Length), out int parsedCap) && parsedCap > 0)
                {
                    options.FpsCap = parsedCap;
                    options.UncappedFps = true;
                }
                //"logfps" is the Game's spelling deliberately, so one benchmark script drives either
                //executable. It also has to be read here rather than ignored: an argument this parse does not
                //recognise falls through to StartupMapPath below, so passing it before now made the Testbed
                //try to load a map called "logfps".
                else if (string.Equals(arg, "logfps", StringComparison.OrdinalIgnoreCase)) options.LogFrameRate = true;
                else if (string.Equals(arg, "nopost", StringComparison.OrdinalIgnoreCase)) options.NoPostEffects = true;
                else if (arg.StartsWith("arena=", StringComparison.OrdinalIgnoreCase)) options.Arena = ParseArenaMembers(arg.Substring("arena=".Length));
                else if (arg.StartsWith("capprobe=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("capprobe=".Length), out int parsedCapProbe) && parsedCapProbe >= 0 && parsedCapProbe <= 6) options.CapProbe = parsedCapProbe;
                else if (arg.StartsWith("alt=", StringComparison.OrdinalIgnoreCase)) options.Alternation = ParseAlternation(arg.Substring("alt=".Length));
                else if (arg.StartsWith("switchmap=", StringComparison.OrdinalIgnoreCase)) options.SwitchMapPath = arg.Substring("switchmap=".Length);
                else if (arg.StartsWith("sky=", StringComparison.OrdinalIgnoreCase) && byte.TryParse(arg.Substring("sky=".Length), out byte parsedSky)) options.SkyNumber = parsedSky;
                else if (arg.StartsWith("ssaa=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("ssaa=".Length), out int parsedSsaa)) options.SupersampleFactor = parsedSsaa;
                else if (arg.StartsWith("exposure=", StringComparison.OrdinalIgnoreCase) && float.TryParse(arg.Substring("exposure=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedExposure)) options.Exposure = parsedExposure;
                else if (arg.StartsWith("scene=", StringComparison.OrdinalIgnoreCase)) options.Scene = arg.Substring("scene=".Length);
                else if (arg.StartsWith("weather=", StringComparison.OrdinalIgnoreCase)) options.Weather = arg.Substring("weather=".Length);
                else if (arg.StartsWith("width=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("width=".Length), out int parsedWidth) && parsedWidth > 0) options.WindowWidth = parsedWidth;
                else if (arg.StartsWith("height=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring("height=".Length), out int parsedHeight) && parsedHeight > 0) options.WindowHeight = parsedHeight;
                //"campos=x,y,z" / "camtarget=x,y,z" place and aim the free camera at startup, so a screenshot
                //can be taken from any vantage (e.g. under the sea, or close in on the drain).
                else if (arg.StartsWith("campos=", StringComparison.OrdinalIgnoreCase) && TryParseVec3(arg.Substring("campos=".Length), out Vector3 parsedPos)) options.CamPos = parsedPos;
                else if (arg.StartsWith("camtarget=", StringComparison.OrdinalIgnoreCase) && TryParseVec3(arg.Substring("camtarget=".Length), out Vector3 parsedTarget)) options.CamTarget = parsedTarget;
                else options.StartupMapPath = arg;
            }

            return options;
        }

        //Parses the arena member list: names of ArenaMembers, comma-separated, each added or - with a leading
        //'-' - removed, left to right. So "arena=all,-glass" draws everything but the drain's glass and
        //"arena=cap" only the stone top. Subtraction is the form the isolation actually wants (take ONE member
        //out of an otherwise complete frame and measure again), which is why "all" is a name rather than the
        //implied starting point. Lenient like the rest of the parse: an unreadable name is skipped.
        private static ArenaMembers ParseArenaMembers(string list)
        {
            ArenaMembers members = ArenaMembers.None;

            foreach (string token in list.Split(','))
            {
                string name = token.Trim();
                if (name.Length == 0) continue;

                bool remove = name[0] == '-';
                if (remove) name = name.Substring(1);

                if (!Enum.TryParse(name, ignoreCase: true, out ArenaMembers one)) continue;

                if (remove) members &= ~one;
                else members |= one;
            }

            return members;
        }

        //Parses the alternation cycle: variants separated by ';', each an arena member list with an optional
        //'/N' cap probe after it. ';' and '/' are used because ',' and '-' already mean something inside a
        //member list. A variant whose member list is unreadable still contributes ArenaMembers.None, which is
        //a legitimate variant (the arena out of the frame) - so unlike the rest of this parse, an empty result
        //is what says the argument said nothing, and the caller leaves the single-variant path alone.
        private static IReadOnlyList<ArenaVariant> ParseAlternation(string spec)
        {
            List<ArenaVariant> variants = new();

            foreach (string token in spec.Split(';'))
            {
                string one = token.Trim();
                if (one.Length == 0) continue;

                int probe = 0;
                int slash = one.LastIndexOf('/');
                if (slash >= 0)
                {
                    if (int.TryParse(one.Substring(slash + 1), out int parsed) && parsed >= 0 && parsed <= 6) probe = parsed;
                    one = one.Substring(0, slash);
                }

                variants.Add(new ArenaVariant(ParseArenaMembers(one), probe));
            }

            return variants;
        }

        //Parses "x,y,z" (invariant, so a decimal point) into a Vector3
        private static bool TryParseVec3(string s, out Vector3 result)
        {
            result = Vector3.Zero;
            string[] parts = s.Split(',');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            result = new Vector3(x, y, z);
            return true;
        }
    }
}
