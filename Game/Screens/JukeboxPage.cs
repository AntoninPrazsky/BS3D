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
    /// the music available in the game"); the recording playing loops until the player picks another or leaves the page
    /// (#899: "play all the time until the user switches manually to another one or leaves the Jukebox"), so
    /// <see cref="RecordingPlayer"/> never moves on by itself - Next and Previous walk the list, across chapters, round
    /// to the first after the last. The chapters come in campaign order, each
    /// named as the level set names it, its family's main theme first and the variants after (the order
    /// <see cref="GameMusic"/> keeps); the front end's and the pause's loops close the list as "Menus". A family no
    /// chapter plays would follow as its own group.
    /// </para>
    /// <para>
    /// Two columns: the chapters on the left, the chosen chapter's recordings on the right over
    /// the visualizer, the now-playing line and the player's buttons, in that order — so the pad walks fifteen chapters
    /// and ten recordings, not a scroller of a hundred and forty. Picking a chapter shows its recordings; picking a
    /// recording plays it. Leaving the page stops it, and the game's own music, which stepped aside, comes back.
    /// </para>
    /// <para>
    /// <b>The now-playing line</b> (#900) names the chapter, the recording and its place in the chapter ("The Silhouettes ·
    /// Harpsichord · 4 / 10"), or says "Pick a recording" before one is chosen. It stands under the visualizer and over
    /// the buttons that change it, is the column's width and ends in an ellipsis rather than widen it, and is not a nav
    /// entry. It is rewritten by <see cref="ShowPlayer"/> when the player's state changes: a pick, Next and Previous move
    /// the player's index, and a loop's wrap (#899) does not, since the same recording goes on, so the line stands as it is.
    /// </para>
    /// <para>
    /// <b>Picking a chapter does not rebuild the page</b> (#901): the recordings' column is cleared and refilled in place
    /// (<see cref="ShowChapter"/>), so the chapters' scroller is never recreated and keeps its offset, and the player who
    /// scrolled down for a chapter finds the list where it was for the next one. A rebuild made two new scrollers, and a
    /// new scroller starts at the top. Only the recordings' own scroller goes back to the top, for the new chapter.
    /// </para>
    /// <para>
    /// <b>The chapter playing keeps a mark of its own</b> (#901: "the currently playing one should stay visibly
    /// selected"): the player's play triangle at its slab's left inner edge, whichever chapter is shown. Brightness says
    /// which chapter is shown, as it says it on every list of the menu; the triangle says where the recording that sounds
    /// lives, so a player who picks the next chapter to look through it, or crosses into one on Next, still sees where the
    /// music is. It is on while a recording is held and dimmed while that one is paused or still being read, as the play
    /// button's own symbol is. The left edge because the pad's focus prompt stands at the right one.
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
        private readonly List<MediaGlyph> _groupMarks = new();
        private readonly List<Button> _trackButtons = new();
        private readonly List<Label> _trackLabels = new();

        //The recordings' column and the scroller round it, kept so a chapter pick refills the one and returns the other to
        //the top without building either again
        private VerticalStackPanel _tracks;
        private ScrollViewer _trackScroll;
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
            _groupMarks.Clear();
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
            column.Widgets.Add(BackButton());

            return ScreenRoot(Plate(column));
        }

        private Widget BuildChapters()
        {
            VerticalStackPanel list = SubColumn();

            for (int g = 0; g < _groups.Count; g++)
            {
                int group = g;
                Button button = MenuButton(ChapterContent(_groups[g].Name, out Label label, out MediaGlyph mark), () => _pendingGroup = group);
                button.Width = ColumnWidth;
                _groupButtons.Add(button);
                _groupLabels.Add(label);
                _groupMarks.Add(mark);
                list.Widgets.Add(button);
            }

            return MenuScroll(list, LEFT_SURROUNDINGS);
        }

        /// <summary>
        /// A chapter's slab: its name centred, as a worded button's is, and the play mark at the left inner edge, hidden
        /// until the chapter holds the recording playing. The mark overlays the slab rather than standing beside the name, so
        /// it never moves the name and every chapter's name stands where a worded entry's would.
        /// </summary>
        private Widget ChapterContent(string name, out Label label, out MediaGlyph mark)
        {
            label = new Label
            {
                Text = name,
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            mark = ButtonGlyph(MediaGlyph.Symbol.Play);
            mark.HorizontalAlignment = HorizontalAlignment.Left;
            mark.Visible = false;

            Panel content = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Widgets.Add(label);
            content.Widgets.Add(mark);

            return content;
        }

        private Widget BuildPlayer()
        {
            VerticalStackPanel right = SubColumn();

            _tracks = SubColumn();
            FillTracks();

            _trackScroll = MenuScroll(_tracks, RIGHT_SURROUNDINGS);
            right.Widgets.Add(_trackScroll);

            RecordingPlayer player = Game.Recordings;
            right.Widgets.Add(new MusicVisualizer(player, ColumnWidth, Scaled(VISUALIZER_HEIGHT), Scaled(VISUALIZER_BAR_GAP))
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Palette = Game.Birthday ? MusicVisualizer.VisualizerPalette.Spectrum : MusicVisualizer.VisualizerPalette.Led,
            });

            //What is playing stands under the bars that show it and over the buttons that change it (#900): above the list
            //it read as the list's heading. A fixed width, the column's, and an ellipsis for what will not fit it: a label
            //left at its text's extent widens the whole column, and the plate round it, for a long chapter's name beside a
            //long recording's title, as the level picker's detail line did before it was bound (#539). It is not a nav
            //entry, so the pad's walk is the order of the buttons alone and is the same wherever the line stands.
            _nowPlaying = new Label
            {
                Font = FontBody,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center,
                AutoEllipsisMethod = FontStashSharp.RichText.AutoEllipsisMethod.Character,
                AutoEllipsisString = "…",
                Width = ColumnWidth,
            };
            right.Widgets.Add(_nowPlaying);

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

        /// <summary>
        /// Puts the shown chapter's recordings into the recordings' column, taking out whatever it held. The buttons are
        /// taken out one at a time rather than by <c>Clear</c>: Myra detaches a cleared child from its parent only through
        /// a copy of the child list that is current only while no change is pending.
        /// </summary>
        private void FillTracks()
        {
            while (_tracks.Widgets.Count > 0) _tracks.Widgets.RemoveAt(_tracks.Widgets.Count - 1);

            _trackButtons.Clear();
            _trackLabels.Clear();

            RecordingPlayer player = Game.Recordings;
            if (_groups.Count == 0 || player == null) return;

            Group group = _groups[_group];
            for (int i = 0; i < group.Count; i++)
            {
                int index = group.First + i;
                Button button = MenuButton(player.Tracks[index].Title, () => Game.Recordings?.PlayAt(index), out Label label);
                button.Width = ColumnWidth;
                _trackButtons.Add(button);
                _trackLabels.Add(label);
                _tracks.Widgets.Add(button);
            }
        }

        /// <summary>
        /// Shows chapter <paramref name="group"/>'s recordings in place (#901): the column is refilled and its scroller
        /// returned to the top, the chapters' scroller is not touched. The pad's cursor stays on the chapter it picked - the
        /// entries are read again, because the recordings it could have walked to are new buttons - and a cursor that was
        /// down (the pointer drove) stays down.
        /// </summary>
        private void ShowChapter(int group)
        {
            _group = group;
            FillTracks();
            _trackScroll.ScrollPosition = Point.Zero;

            //The marks on the lists are written when the state they are written for changes, and the new buttons have none
            _shownState = -1;

            if (IsActive) Game.RefreshNavEntries(group < _groupButtons.Count ? _groupButtons[group] : null);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            //A chapter picked: its recordings replace the right column's. Taken here, not in the click, because the click
            //comes from inside Myra's own processing of the tree it is changing (Help's page turn does the same)
            if (_pendingGroup >= 0)
            {
                int group = _pendingGroup;
                _pendingGroup = -1;

                if (group != _group) ShowChapter(group);
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
            int playing = -1;
            if (index >= 0 && index < player.Tracks.Count)
            {
                RecordingPlayer.Track track = player.Tracks[index];
                int number = 0, count = 0;
                for (int g = 0; g < _groups.Count; g++)
                {
                    Group group = _groups[g];
                    if (index < group.First || index >= group.First + group.Count) continue;
                    number = index - group.First + 1;
                    count = group.Count;
                    playing = g;
                }

                _nowPlaying.Text = string.Format(CultureInfo.InvariantCulture, "{0} · {1} · {2} / {3}", track.Group, track.Title, number, count);
            }
            else _nowPlaying.Text = "Pick a recording";

            //Pause while it sounds, play otherwise; play in the aside grey while a recording is still being read
            _playGlyph.Shape = action == 2 ? MediaGlyph.Symbol.Pause : MediaGlyph.Symbol.Play;
            _playGlyph.Tint = action == 1 ? BS3DGame.MENU_TEXT_DIM : BS3DGame.MENU_TEXT;

            //The chapter shown and the recording playing stand out by brightness, the rest step back - ScenePage's marking.
            //The chapter that holds the recording carries the play mark besides, shown or not (#901)
            for (int g = 0; g < _groupLabels.Count; g++)
                _groupLabels[g].TextColor = g == _group ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;

            for (int g = 0; g < _groupMarks.Count; g++)
            {
                _groupMarks[g].Visible = g == playing && player.HoldsTrack;
                _groupMarks[g].Tint = action == 2 ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
            }

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
