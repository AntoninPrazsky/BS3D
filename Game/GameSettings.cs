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

        /// <summary>The tonemap's shutter, off the exposure ladder. Zero means the game's own default.</summary>
        [JsonPropertyName("exposure")]
        public float Exposure { get; set; }

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

        [JsonPropertyName("fullscreen")]
        public bool Fullscreen { get; set; }

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
        /// Whether a big collapse still takes the camera (#290). On by default and turned off by the player,
        /// which is the shape the request asked for: the flourish is part of the game and every player who has
        /// not opened this page has seen it — this is an opt-<i>out</i> for the ones who would rather keep
        /// shooting. Nothing else about a drop changes when it is off; only the camera stays where it was.
        /// </summary>
        [JsonPropertyName("dropCinematic")]
        public bool DropCinematic { get; set; } = true;

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
        /// </summary>
        [JsonPropertyName("quality")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public QualityLevel? Quality { get; set; }

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
            GameSettings settings = TryRead(path) ?? TryRead(path + BackupSuffix) ?? new GameSettings();

            //Bound to the real file whichever one answered — a recovered backup must not become where the
            //next write goes
            settings.Path = path;

            return settings;
        }

        private static GameSettings TryRead(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    GameSettings settings = JsonSerializer.Deserialize<GameSettings>(stream, Options);

                    if (settings?.Format == FormatMarker && settings.Version <= CurrentVersion) return settings;
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
