using BS3D.Audio;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// The Jukebox (#704, in Extras): every recording the game plays, grouped by the chapter that plays it, on the About
    /// page's visualizer. A level plays one recording of its chapter's family a time and the family rotates, so most of a
    /// chapter's music is never heard in a normal run; this is the one place to hear all of it.
    /// <para>
    /// <b>The owner's answers:</b> all the music the game has, whatever the player has unlocked ("the Jukebox plays all
    /// the music available in the game"); after a recording, the next one (<see cref="RecordingPlayer"/> walks the list
    /// and moves on, across chapters, round to the first after the last). The chapters come in campaign order, each
    /// named as the level set names it, its family's main theme first and the variants after (the order
    /// <see cref="GameMusic"/> keeps); the front end's and the pause's loops close the list as "Menus". A family no
    /// chapter plays would follow as its own group.
    /// </para>
    /// <para>
    /// Two columns, as About and Settings: the chapters on the left, the chosen chapter's recordings on the right over
    /// the visualizer and the player's buttons — so the pad walks fifteen chapters and ten recordings, not a scroller of
    /// a hundred and forty. Picking a chapter shows its recordings; picking a recording plays it. Leaving the page stops
    /// it, and the game's own music, which stepped aside, comes back.
    /// </para>
    /// </summary>
    internal sealed class JukeboxPage : MenuPage
    {
        private const int COLUMN_GAP = 110;
        private const int PLAYER_BUTTON_WIDTH = 320;
        private const int PLAYER_BUTTON_GAP = 20;
        private const int VISUALIZER_HEIGHT = 240;
        private const int VISUALIZER_BAR_GAP = 10;

        //What each column needs round its scroller (the heading, Back and the plate; on the right also the now-playing
        //line, the visualizer and the buttons), so a scroller gives up only what those take
        private const int LEFT_SURROUNDINGS = 620;
        private const int RIGHT_SURROUNDINGS = 1180;

        private const string MUSIC_DIRECTORY = "Music";
        private const string MENU_TRACK = "menu";
        private const string PAUSE_TRACK = "pause";

        //One chapter's run of the player's list
        private readonly struct Group
        {
            public readonly string Name;
            public readonly int First;
            public readonly int Count;

            public Group(string name, int first, int count)
            {
                Name = name;
                First = first;
                Count = count;
            }
        }

        private readonly List<Group> _groups = new();
        private LevelSet _builtFor;
        private bool _built;

        private int _group;
        private int _pendingGroup = -1;

        private readonly List<Button> _groupButtons = new();
        private readonly List<Label> _groupLabels = new();
        private readonly List<Button> _trackButtons = new();
        private readonly List<Label> _trackLabels = new();
        private Label _nowPlaying;
        private MediaGlyph _playGlyph;

        //What the labels last said, as one number, so they are written when the player's state changes and not every frame
        private int _shownState = -1;

        public JukeboxPage(BS3DGame game) : base(game) { }

        //The jukebox= argument's "play": the chapter's first recording, once the list exists
        private bool _playOnShow;

        /// <summary>Opens the page on chapter <paramref name="chapter"/> (1-based), playing its first recording when asked - the <c>jukebox=</c> argument.</summary>
        internal void Pin(int chapter, bool play)
        {
            _group = Math.Max(0, chapter - 1);
            _playOnShow = play;
        }

        protected override Widget BuildTree()
        {
            EnsureTracks();

            _groupButtons.Clear();
            _groupLabels.Clear();
            _trackButtons.Clear();
            _trackLabels.Clear();
            _shownState = -1;
            _group = Math.Clamp(_group, 0, Math.Max(0, _groups.Count - 1));

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("JUKEBOX"));

            HorizontalStackPanel columns = new()
            {
                Spacing = Scaled(COLUMN_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Added in this order, and that IS the pad's order: the chapters, then the recordings, then the buttons
            columns.Widgets.Add(BuildChapters());
            columns.Widgets.Add(BuildPlayer());

            column.Widgets.Add(columns);
            column.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(column));
        }

        private Widget BuildChapters()
        {
            VerticalStackPanel list = SubColumn();

            for (int g = 0; g < _groups.Count; g++)
            {
                int group = g;
                Button button = MenuButton(_groups[g].Name, () => _pendingGroup = group, out Label label);
                button.Width = ColumnWidth;
                _groupButtons.Add(button);
                _groupLabels.Add(label);
                list.Widgets.Add(button);
            }

            return MenuScroll(list, LEFT_SURROUNDINGS);
        }

        private Widget BuildPlayer()
        {
            VerticalStackPanel right = SubColumn();

            _nowPlaying = new Label
            {
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            right.Widgets.Add(_nowPlaying);

            VerticalStackPanel tracks = SubColumn();
            RecordingPlayer player = Game.Recordings;

            if (_groups.Count > 0 && player != null)
            {
                Group group = _groups[_group];
                for (int i = 0; i < group.Count; i++)
                {
                    int index = group.First + i;
                    Button button = MenuButton(player.Tracks[index].Title, () => Game.Recordings?.PlayAt(index), out Label label);
                    button.Width = ColumnWidth;
                    _trackButtons.Add(button);
                    _trackLabels.Add(label);
                    tracks.Widgets.Add(button);
                }
            }

            right.Widgets.Add(MenuScroll(tracks, RIGHT_SURROUNDINGS));

            right.Widgets.Add(new MusicVisualizer(player, ColumnWidth, Scaled(VISUALIZER_HEIGHT), Scaled(VISUALIZER_BAR_GAP))
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Palette = Game.Birthday ? MusicVisualizer.VisualizerPalette.Spectrum : MusicVisualizer.VisualizerPalette.Led,
            });

            HorizontalStackPanel controls = new()
            {
                Spacing = Scaled(PLAYER_BUTTON_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            Button previous = MenuButton(ButtonGlyph(MediaGlyph.Symbol.Previous), () => Game.Recordings?.Previous());
            previous.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(previous);

            _playGlyph = ButtonGlyph(MediaGlyph.Symbol.Play);
            Button play = MenuButton(_playGlyph, () => Game.Recordings?.PlayPause());
            play.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(play);

            Button next = MenuButton(ButtonGlyph(MediaGlyph.Symbol.Next), () => Game.Recordings?.Next());
            next.Width = Scaled(PLAYER_BUTTON_WIDTH);
            controls.Widgets.Add(next);

            right.Widgets.Add(controls);

            return right;
        }

        private VerticalStackPanel SubColumn() => new()
        {
            Spacing = Scaled(20),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            //A chapter picked: its recordings replace the right column. Taken here, not in the click, because the click
            //comes from inside Myra's own processing of the tree it would be replacing (Help's page turn does the same)
            if (_pendingGroup >= 0)
            {
                int group = _pendingGroup;
                _pendingGroup = -1;

                if (group != _group)
                {
                    _group = group;
                    InvalidateTree();
                    Game.RebuildPage(this, () => group < _groupButtons.Count ? _groupButtons[group] : null);
                }
            }

            if (_playOnShow && _built && _groups.Count > 0)
            {
                _playOnShow = false;
                Game.Recordings?.PlayAt(_groups[_group].First);
            }

            ShowPlayer();
        }

        /// <summary>The cursor arrives on the recording playing, or on the chosen chapter.</summary>
        internal override Button NavArrival
        {
            get
            {
                RecordingPlayer player = Game.Recordings;
                if (player != null && _groups.Count > 0)
                {
                    int at = player.Index - _groups[_group].First;
                    if (at >= 0 && at < _trackButtons.Count) return _trackButtons[at];
                }

                return _group < _groupButtons.Count ? _groupButtons[_group] : null;
            }
        }

        /// <summary>The recording stops when the page is left, and the game's own music comes back (<see cref="GameMusic.Yielding"/>).</summary>
        public override void Leave()
        {
            Game.Recordings?.Stop();
            base.Leave();
        }

        internal override void Refresh()
        {
            _shownState = -1;
            ShowPlayer();
        }

        /// <summary>The now-playing line, the play symbol and the marks on the lists, written only when they change.</summary>
        private void ShowPlayer()
        {
            RecordingPlayer player = Game.Recordings;
            if (player == null || _nowPlaying == null) return;

            int action = player.IsLoading ? 1 : player.IsPlaying ? 2 : player.HoldsTrack ? 3 : 0;
            int state = ((player.Index + 1) * 8 + action) * 64 + _group;

            if (state == _shownState) return;
            _shownState = state;

            int index = player.Index;
            if (index >= 0 && index < player.Tracks.Count)
            {
                RecordingPlayer.Track track = player.Tracks[index];
                int number = 0, count = 0;
                foreach (Group group in _groups)
                {
                    if (index < group.First || index >= group.First + group.Count) continue;
                    number = index - group.First + 1;
                    count = group.Count;
                }

                _nowPlaying.Text = string.Format(CultureInfo.InvariantCulture, "{0} · {1} · {2} / {3}", track.Group, track.Title, number, count);
            }
            else _nowPlaying.Text = "Pick a recording";

            //Pause while it sounds, play otherwise; play in the aside grey while a recording is still being read
            _playGlyph.Shape = action == 2 ? MediaGlyph.Symbol.Pause : MediaGlyph.Symbol.Play;
            _playGlyph.Tint = action == 1 ? BS3DGame.MENU_TEXT_DIM : BS3DGame.MENU_TEXT;

            //The chapter shown and the recording playing stand out by brightness, the rest step back - ScenePage's marking
            for (int g = 0; g < _groupLabels.Count; g++)
                _groupLabels[g].TextColor = g == _group ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

            int first = _groups.Count > 0 ? _groups[_group].First : 0;
            for (int i = 0; i < _trackLabels.Count; i++)
                _trackLabels[i].TextColor = first + i == index && player.HoldsTrack ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
        }

        /// <summary>
        /// The player's list, built once per level set: the chapters in campaign order, each with its family's recordings
        /// (its first level's <c>music</c>, the family the name before the first dash), then any family no chapter plays,
        /// then the menus. Read off the music folder's file names and the level files, nothing decoded.
        /// </summary>
        private void EnsureTracks()
        {
            LevelSet set = Game.LevelSet;
            RecordingPlayer player = Game.Recordings;
            if (player == null || (_built && ReferenceEquals(set, _builtFor))) return;

            _built = true;
            _builtFor = set;
            _groups.Clear();

            string directory = Path.Combine(AppContext.BaseDirectory, MUSIC_DIRECTORY);
            Dictionary<string, List<string>> families = ReadFamilies(directory);
            List<RecordingPlayer.Track> tracks = new();
            HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);

            if (set != null && set.HasBlocks)
            {
                for (int index = 0; index < set.Count; index++)
                {
                    set.BlockRange(index, out int first, out _);
                    if (first != index) continue;

                    string family = ReadFamily(set, index);
                    if (family == null || !families.ContainsKey(family) || !placed.Add(family)) continue;

                    AddGroup(tracks, set.BlockName(index) ?? Title(family), family, families[family]);
                }
            }

            List<string> rest = new();
            foreach (string family in families.Keys) if (!placed.Contains(family)) rest.Add(family);
            rest.Sort(StringComparer.Ordinal);
            foreach (string family in rest) AddGroup(tracks, Title(family), family, families[family]);

            //The front end's and the pause's own loops, always last
            List<RecordingPlayer.Track> menus = new();
            string menu = Path.Combine(directory, MENU_TRACK + ".ogg");
            string pause = Path.Combine(directory, PAUSE_TRACK + ".ogg");
            if (File.Exists(menu)) menus.Add(new RecordingPlayer.Track { Path = menu, Title = "Main menu", Group = "Menus" });
            if (File.Exists(pause)) menus.Add(new RecordingPlayer.Track { Path = pause, Title = "Pause", Group = "Menus" });
            if (menus.Count > 0)
            {
                _groups.Add(new Group("Menus", tracks.Count, menus.Count));
                tracks.AddRange(menus);
            }

            player.SetTracks(tracks);
        }

        private void AddGroup(List<RecordingPlayer.Track> tracks, string name, string family, List<string> files)
        {
            _groups.Add(new Group(name, tracks.Count, files.Count));

            foreach (string file in files)
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                string title = stem.Length > family.Length ? Title(stem.Substring(family.Length + 1)) : "Main theme";
                tracks.Add(new RecordingPlayer.Track { Path = file, Title = title, Group = name });
            }
        }

        /// <summary>The music folder's recordings by family (the name before the first dash), the bare file first, as GameMusic orders them.</summary>
        private static Dictionary<string, List<string>> ReadFamilies(string directory)
        {
            Dictionary<string, List<string>> families = new(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directory)) return families;

            string[] files = Directory.GetFiles(directory, "*.ogg");
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(stem, MENU_TRACK, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stem, PAUSE_TRACK, StringComparison.OrdinalIgnoreCase)) continue;

                int dash = stem.IndexOf('-');
                string family = dash < 0 ? stem : stem.Substring(0, dash);

                if (!families.TryGetValue(family, out List<string> members)) families[family] = members = new List<string>();
                members.Add(file);
            }

            foreach ((string family, List<string> members) in families)
                members.Sort((a, b) =>
                {
                    bool bareA = string.Equals(Path.GetFileNameWithoutExtension(a), family, StringComparison.OrdinalIgnoreCase);
                    bool bareB = string.Equals(Path.GetFileNameWithoutExtension(b), family, StringComparison.OrdinalIgnoreCase);
                    return bareA == bareB ? string.CompareOrdinal(a, b) : bareA ? -1 : 1;
                });

            return families;
        }

        /// <summary>The family a chapter's first level plays (its <c>music</c> word up to the first dash), or null.</summary>
        private static string ReadFamily(LevelSet set, int index)
        {
            try
            {
                string path = set.ResolvePath(index);
                if (!Level.IsLevelFile(path)) return null;

                string music = Level.Load(path).Music;
                if (string.IsNullOrWhiteSpace(music)) return null;

                int dash = music.IndexOf('-');
                return (dash < 0 ? music : music.Substring(0, dash)).Trim().ToLowerInvariant();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>"punk-01" as "Punk 01": the words of a file name, each capitalised.</summary>
        private static string Title(string words)
        {
            string[] parts = words.Split('-', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
