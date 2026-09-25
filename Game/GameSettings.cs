using Prazsky.Core.Tools;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BS3D
{
    /// <summary>
    /// The player's answers, remembered across runs (#354): <c>%LOCALAPPDATA%\BS3D\Settings.json</c>, beside
    /// the campaign save. Until this existed the settings page's entire effect was one session long — the
    /// four volumes, the exposure ladder, the sky, the quality tier and every toggle were back to default at
    /// the next launch. Audio is the setting people change <b>once</b> and expect to stay changed; a game
    /// that forgets it reads as broken rather than as unconfigured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule that decides what lands here, stated once so a later row cannot get it wrong: a value that
    /// arrived from <see cref="Program"/> is applied but never written back; a value that arrived from a click
    /// is written.</b> It is enforced structurally rather than per row — this object <i>is</i> the player's
    /// answers, the game's own fields are what this run is doing, and only the settings verbs
    /// (<c>BS3DGame.Settings.cs</c>) ever write back into it. So a benchmark run pinned with <c>quality=</c>
    /// or muted with <c>mute</c> can click any other row without its arguments leaking into the file.
    /// </para>
    /// <para>
    /// <b>Two things deliberately do not persist.</b> <c>Unlock all</c> (#349), by its own ruling: a
    /// development switch that survives a restart is one that is eventually left on, and the single thing it
    /// must never do is make a real save look further along than it is. And the command-line pins
    /// (<c>quality=</c>, <c>ssaa=</c>, <c>scene=</c>, <c>sky=</c>, <c>exposure=</c>, <c>mute</c>,
    /// <c>fullscreen</c>, <c>nocap</c>), which are a <i>run's</i> arguments and not the player's answers.
    /// </para>
    /// <para>
    /// Lenient like <c>PlayerProgress</c> and for a stronger reason: an unreadable save costs a campaign, but
    /// an unreadable settings file costs nothing worth refusing to start over. Anything that is not a settings
    /// file comes back as the defaults, and the backup is tried first — the write is
    /// <see cref="AtomicFile.WriteText"/>'s, the same discipline the save uses.
    /// </para>
    /// </remarks>
    internal sealed class GameSettings
    {
        internal const string FormatMarker = "bs3d-settings";
        internal const int CurrentVersion = 1;

        internal const string DefaultFileName = "Settings.json";
        internal const string BackupSuffix = ".bak";

        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        //The four volume rows (#46), each 0..1. They are the reason this file exists at all: a player who
        //turns the music down turns it down again every single launch without one.
        [JsonPropertyName("master")]
        public float MasterVolume { get; set; } = 1f;

        [JsonPropertyName("sfx")]
        public float SfxVolume { get; set; } = 1f;

        [JsonPropertyName("music")]
        public float MusicVolume { get; set; } = 1f;

        [JsonPropertyName("ambience")]
        public float AmbienceVolume { get; set; } = 1f;

        /// <summary>
        /// The pad's two body motors (#378), 0 for off, on the same quarter-step ladder as the volumes above —
        /// it is the mix's fourth channel to the player (how hard the game hits back) rather than a control
        /// rate like <see cref="Sensitivity"/>, so it sits with what the volumes already are and not with what
        /// the sensitivities are.
        /// </summary>
        [JsonPropertyName("rumble")]
        public float RumbleStrength { get; set; } = 1f;

        /// <summary>The tonemap's shutter, off the exposure ladder. Zero means the game's own default.</summary>
        [JsonPropertyName("exposure")]
        public float Exposure { get; set; }

        /// <summary>
        /// The player's multiplier over <c>MouseAim.SENSITIVITY</c>, off the sensitivity ladder (#384). 1 is the
        /// shipped feel, which is why it is the default rather than 0: unlike the exposure row above, there is
        /// no sentinel here, because there is no command-line argument that could want to mean "whatever the
        /// game thinks". Clamped on the way in by the game, so a hand-edited file cannot pin the aim at zero.
        /// </summary>
        [JsonPropertyName("sensitivity")]
        public float Sensitivity { get; set; } = 1f;

        /// <summary>
        /// The player's multiplier on the cursor's rate <b>while precisely aiming</b>, over the general one and
        /// over the lens's own FOV-ratio slowing (#384), off the same ladder (#497). Snapped onto the ladder by
        /// the game like <see cref="Sensitivity"/>.
        /// <para>
        /// <b>It ships at 1.25 since #477, and the figure is not a taste.</b> <c>PreciseAim.CursorRateScale</c>
        /// slows the leaned cursor by the ratio of the two fields' half-angle tangents — 0.828 on this game's
        /// pair, a <b>17.2 %</b> slowdown — which is geometrically right and which the owner's playtest read as
        /// simply too slow. 1.25 puts the leaned rate back at <b>1.035 ×</b> the overview's, parity to within
        /// 3.5 %, so out of the box the hand moves in precise aim at about the speed it moves outside it. A
        /// player who wants #384's geometric answer sets the row to 100 %.
        /// </para>
        /// <para>
        /// ⚠ <b>An existing save keeps whatever it stored</b>, since this default only fills a missing value —
        /// so the machine that raised #477 has to touch the row once, or delete the key. That is the right way
        /// round: a settings file is the player's, and a new default silently rewriting one would be worse than
        /// the complaint it answers.
        /// </para>
        /// </summary>
        [JsonPropertyName("aimSensitivity")]
        public float AimSensitivity { get; set; } = 1.25f;

        /// <summary>
        /// The sky the front end comes up under, or 0 for "whatever the scene wants". It is seeded
        /// <b>before</b> <c>SetScene</c> runs rather than after, which is what keeps it from overriding the
        /// six scenes that state a dome of their own (the sea, the savanna, the tropics, the volcano, Mars
        /// and the storm) — those replace it, every other scene keeps it. This is therefore exactly as
        /// durable as the dome ever is: a scene change or a level still says what sky it stands under, which
        /// is the dome's nature and not this file failing to hold it.
        /// </summary>
        [JsonPropertyName("sky")]
        public byte SkyDome { get; set; }

        /// <summary>
        /// <b>True by default, so a first launch fills the screen</b> (#455). It is the one default that is
        /// about people who have never run this game: until #453 made the build downloadable, every machine
        /// that ran it had a settings file, and the `false` this used to inherit from <c>bool</c> was an
        /// omission rather than an answer — a stranger's first impression was a 1600×900 window on a 4K panel.
        /// Fullscreen here costs nothing it would not cost anyway: <c>SetGraphics</c> is borderless at
        /// <c>CurrentDisplayMode</c> (#157), so it is the panel's own resolution with nothing scaled and no
        /// display mode to lose. The moment the player answers — F11 or the Settings row, both of which write
        /// through <c>ToggleFullscreen</c> — this default never speaks again.
        /// </summary>
        [JsonPropertyName("fullscreen")]
        public bool Fullscreen { get; set; } = true;

        /// <summary>
        /// <b>True by default, because that is what the game already did</b> — <c>InfoRenderer</c> is a
        /// <c>DrawableGameComponent</c> and nothing ever set its <c>Visible</c>, so the overlay has always
        /// come up on. A default of false here would have turned it off for everyone as a side effect of
        /// making it persist, which is not what "remember what the player chose" means.
        /// </summary>
        [JsonPropertyName("fpsOverlay")]
        public bool FpsOverlay { get; set; } = true;

        [JsonPropertyName("uncappedFps")]
        public bool UncappedFps { get; set; }

        [JsonPropertyName("aberration")]
        public bool Aberration { get; set; } = true;

        [JsonPropertyName("grain")]
        public bool Grain { get; set; } = true;

        /// <summary>
        /// Whether what moves is smeared along its motion (#402) — the barrel as it is swung, the shot, a falling
        /// group, the frame under the recoil's kick. On by default, like the lens's other looks, and a taste toggle
        /// like them; a quality tier that cannot afford it leaves it off whatever this says.
        /// </summary>
        [JsonPropertyName("motionBlur")]
        public bool MotionBlur { get; set; } = true;

        /// <summary>
        /// Whether a big collapse still takes the camera (#290). On by default and turned off by the player,
        /// which is the shape the request asked for: the flourish is part of the game and every player who has
        /// not opened this page has seen it — this is an opt-<i>out</i> for the ones who would rather keep
        /// shooting. Nothing else about a drop changes when it is off; only the camera stays where it was.
        /// </summary>
        [JsonPropertyName("dropCinematic")]
        public bool DropCinematic { get; set; } = true;

        /// <summary>
        /// Whether the first chapter's tutorial cards are shown (#189). On by default, for the player the
        /// tutorial exists for — the one who has just downloaded the game and read nothing — and turned off by
        /// the player who does not want to be told; the same opt-out shape as <see cref="DropCinematic"/>.
        /// What has already been <i>taught</i> is the save's business (<c>PlayerProgress.Lessons</c>), not
        /// this file's: this is a preference, that is a record.
        /// </summary>
        [JsonPropertyName("tutorial")]
        public bool Tutorial { get; set; } = true;

        /// <summary>
        /// The tier the <b>player chose</b>, and null until they have chosen one — which is the whole of the
        /// owner's ruling on this row. The adaptive probe's verdict is deliberately <b>not</b> stored: it is
        /// measured rather than chosen, and the probe can only ever step a tier <i>down</i>, so a verdict that
        /// survived the session would be a ratchet — one unlucky window (a build running in the background, a
        /// thermal dip) would pin the game to Low for good and nothing but this row would ever raise it again.
        /// Re-measuring every launch costs a few seconds of one scene and cannot get stuck.
        /// <para>
        /// When it is set it behaves exactly as <c>quality=</c> does: the tier is applied and the probe is
        /// told not to second-guess it (<c>_qualityPinnedByPlayer</c>).
        /// </para>
        /// <para>
        /// Two clicks write it, both of them the player's (#390): the Quality row stores the tier it cycled to,
        /// and turning <see cref="AdaptiveQuality"/> off stores the tier that row is showing. The second can be a
        /// tier the probe reached, and it is still not the ratchet above — the player turned the probe off while
        /// looking at it, with the Quality row beside the switch to raise it again.
        /// </para>
        /// <para>
        /// Stored by name. <c>"Ultra"</c> joined the three in #484 without a format bump, and a file from before
        /// it loads unchanged. The one direction that does not survive is backwards: a build older than #484 fails
        /// to parse <c>"Ultra"</c> (an unknown name is a <c>JsonException</c>) and falls to the backup and then to
        /// the defaults, so every setting in the file is lost to it, not only this one. Accepted — nobody
        /// downgrades a game on purpose. A bare number the converter also accepts is range-checked in
        /// <see cref="TryRead"/> and read as none chosen when this build has no such tier.
        /// </para>
        /// </summary>
        [JsonPropertyName("quality")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public QualityLevel? Quality { get; set; }

        /// <summary>
        /// Whether the game may lower the tier on its own (#390), for the player who would rather keep one —
        /// High included — at whatever frame rate it costs. <b>True by default, because that is what the game
        /// already did</b>, so nobody who never opens the page sees a change.
        /// <para>
        /// False pins the tier at startup exactly as a stored <see cref="Quality"/> does, and wins when none is
        /// stored too: the tier is then High, the authored look. A stored tier switched the probe off before
        /// this existed, so a file written back then — a tier and no key — still pins, and the row reads Off
        /// over it. The two clicks keep the pair honest from here on: picking a tier writes this false, and
        /// turning this back on clears the tier.
        /// </para>
        /// </summary>
        [JsonPropertyName("adaptiveQuality")]
        public bool AdaptiveQuality { get; set; } = true;

        /// <summary>
        /// Whether the player's cleared levels go to the online score boards (#546, #542). <b>Off unless the
        /// player turned it on</b> — the boards are opt-in, and a file written before the row existed says
        /// nothing, which reads as off. The identity it sends under is not here but in <c>Online.json</c>
        /// (<see cref="Online.OnlineIdentity"/>): this file is rewritten by every settings click and restored by
        /// hand after test runs, and a token has to survive both. The settings row that sets this is #548's.
        /// </summary>
        [JsonPropertyName("online")]
        public bool Online { get; set; }

        /// <summary>
        /// The score service to submit to, overriding the built-in one (#546). <b>No settings row shows it,
        /// deliberately</b>: it is for pointing a build at a local run of the API
        /// (<c>http://localhost:5000</c>) or at a test host, and a player has no reason to see it. It is also
        /// the only way a <b>local</b> build submits at all — a build that did not come out of a release has no
        /// server unless this names one, so a developer's runs never land on the public boards by accident.
        /// Null means the built-in one. <see cref="Online.OnlineScores"/> refuses anything but <c>https://</c>,
        /// or <c>http://</c> to this machine.
        /// </summary>
        [JsonPropertyName("server")]
        public string Server { get; set; }

        [JsonIgnore]
        internal string Path { get; private set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Reads the settings at <paramref name="path"/>, falling back to the backup and then to the
        /// defaults. Never throws: there is no state of this file that is worth not starting the game over.
        /// </summary>
        internal static GameSettings Load(string path)
        {
            GameSettings settings = TryRead(path);

            //A file that did not read is kept aside before the first settings click demotes it to the backup and
            //the second destroys it (#571), and said once - this used to fall to the defaults without a word
            if (settings == null)
            {
                string kept = AtomicFile.KeepUnreadable(path);
                settings = TryRead(path + BackupSuffix);
                kept ??= settings == null ? AtomicFile.KeepUnreadable(path + BackupSuffix) : null;

                if (kept != null)
                    Console.WriteLine($"[settings] '{path}' would not read (damaged, or a newer build's);"
                        + $" {(settings != null ? "using its backup" : "using the defaults")}, and the file is kept as '{kept}'");
            }

            settings ??= new GameSettings();

            //Bound to the real file whichever one answered — a recovered backup must not become where the
            //next write goes
            settings.Path = path;

            return settings;
        }

        private static float UnitRow(float value) => float.IsNaN(value) ? 1f : Math.Clamp(value, 0f, 1f);

        private static GameSettings TryRead(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    GameSettings settings = JsonSerializer.Deserialize<GameSettings>(stream, Options);

                    if (settings?.Format == FormatMarker && settings.Version <= CurrentVersion)
                    {
                        //A tier this build does not have reads as "none chosen" rather than as an index off the
                        //end of QualityPreset.Presets: the string converter also accepts a bare number (#484)
                        if (settings.Quality.HasValue && !Enum.IsDefined(settings.Quality.Value)) settings.Quality = null;

                        //The mix's rows are 0..1 and go straight to SoundEffectInstance.Volume, which refuses
                        //anything outside that - a hand-edited "master": 4 must not be the reason a click throws
                        //(#571). NaN falls to full, the row's default.
                        settings.MasterVolume = UnitRow(settings.MasterVolume);
                        settings.SfxVolume = UnitRow(settings.SfxVolume);
                        settings.MusicVolume = UnitRow(settings.MusicVolume);
                        settings.AmbienceVolume = UnitRow(settings.AmbienceVolume);
                        settings.RumbleStrength = UnitRow(settings.RumbleStrength);

                        return settings;
                    }
                }
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                //Whatever is there is not settings
            }

            return null;
        }

        /// <summary>
        /// Writes the answers back, atomically. Every row is a discrete click — there is no slider to
        /// debounce — so this is called from the settings verbs directly rather than on a timer.
        /// </summary>
        internal void Save() => AtomicFile.WriteText(Path, JsonSerializer.Serialize(this, Options), BackupSuffix);
    }
}
