using Myra.Graphics2D.UI;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// What Play opens: the level set's entries in <b>play order</b>, as a grid of tiles rather than a
    /// scrolling list (#91). A tile carries the number, the name and the player's stars (or the lock); what
    /// does <b>not</b> fit a tile — the entry's rules, a lock's arithmetic — moves to the one detail line
    /// under the grid, spoken for whichever tile the pointer or the focus cursor stands on.
    /// <para>
    /// <b>One chapter to a page since #273, paged left and right</b>, where it used to be the whole campaign
    /// in one vertically scrolled grid. Ninety levels (#255) is over twenty rows of tiles behind one
    /// scrollbar; ten is two rows that fit the window with room to spare. The trade is deliberate and it is
    /// the reverse of #91's own reasoning — "the whole campaign at a glance" becomes "the whole chapter at a
    /// glance" — and it only became the better bargain because chapters (<c>LevelSetEntry.Block</c>, #184)
    /// did not exist when #91 was written. What the paging takes away, the chapter's own readout and the row
    /// of pips under the name give back: how far this chapter has got, and which of the others are finished.
    /// </para>
    /// <para>
    /// <b>A pager, not a strip of tabs.</b> Nine clickable chapter headers is a row that stops being legible
    /// the day a set has twenty chapters, and "switch left and right" is a pager's gesture; the pips are a
    /// <i>readout</i> rather than nine more buttons, which is what keeps the pad's walk over this page down to
    /// the ten tiles, the two arrows and Back. The arrows wrap, so the far end of the campaign is four presses
    /// away rather than eight — which is also why the pips have to be there: with wrapping, "where am I" is a
    /// question the arrows cannot answer.
    /// </para>
    /// <para>
    /// <b>An unchaptered set is still one grid</b> — the page it was before #273, four tiles to a row inside
    /// the scroller. A set naming no blocks has every entry in a run of one (see <c>LevelSet.BlockRange</c>),
    /// so paging it would deal ninety chapters of a single level each. Every other block-aware corner of the
    /// game is gated on <c>HasBlocks</c> for the same reason and this is the same gate.
    /// </para>
    /// <para>
    /// A locked entry is shown rather than hidden, because a gate the player cannot see is a campaign that
    /// appears to simply end — and a locked <i>chapter</i> is likewise still a page, with its levels carrying
    /// the same treatment and its readout saying what opens it. It is a page rather than a jump straight into
    /// the first level because a campaign is a list the player chooses from — and because the alternative,
    /// showing it only when the set has more than one entry, makes the flow depend on the data and surprises
    /// the player the day a second level is authored.
    /// </para>
    /// </summary>
    internal sealed class LevelSelectPage : MenuPage
    {
        public LevelSelectPage(BS3DGame game) : base(game) { }

        //The tile: wide enough for the longest shipped name in the small face with room to spare, tall
        //enough for the number, the name and the star row without crowding — in the menu's 2160p design
        //units, resolved at build time like every other size here.
        private const int TILE_WIDTH = 440;
        private const int TILE_HEIGHT = 300;

        //Four to a row was cut for a column that scrolled; a chapter of ten wants FIVE, which is two full rows
        //and no scrollbar (#273). The tile itself was re-checked against the wider grid and kept: nothing about
        //a name got longer, and five of these plus the plate's padding still fit the design width at 5:4, which
        //is the narrowest shape a window here is likely to take.
        private const int TILE_COLUMNS = 4;
        private const int CHAPTER_COLUMNS = 5;

        /// <summary>
        /// What the page needs around the grid — the heading with the star total, the detail line, the Back
        /// button and the plate's padding — so the scroller only ever gives up what those actually take. The
        /// chaptered page carries more above the grid (the pager row with the chapter's readout in it, and the
        /// pips) and therefore reserves more; both are sums of the figures below them and not guesses.
        /// </summary>
        private const int LIST_SURROUNDINGS = 700;
        private const int CHAPTER_SURROUNDINGS = 1010;

        //The chapter arrows: a square slab either side of the name, big enough that the glyph inside it reads
        //at the same weight as the heading beside it. A visible affordance is the point — the page is
        //mouse-first for most players, and a left/right key nobody can see is not discoverable.
        private const int ARROW_SIZE = 160;

        //The name and its readout sit in a block of FIXED width, so the arrows stand in the same place on
        //every chapter. Without it "The Coil" and "The Spectrum" would shift them, and paging would read as
        //the buttons jumping about under the pointer. Wide enough for the readout line, which is the longer
        //of the two.
        private const int CHAPTER_HEADER_WIDTH = 1600;

        //One pip per chapter: filled for a chapter whose every level is cleared, hollow otherwise, and the one
        //being shown is the bright one. Two orthogonal bits, each read at a glance, and no hue — emphasis here
        //is brightness (see the palette comment in BS3DGame.Menu.cs). A chapter still locked is deliberately
        //not a third state: the tiles inside it and its own readout line both say so, and a third brightness
        //on a 20-pixel glyph would not survive being looked at.
        //
        //Set in FontStars (the rating's size) rather than the small face, which is what the row was drawn in
        //first: at 58 design units a filled disc and a hollow ring are both a 20-pixel dot on a 900p window and
        //could not be told apart at all. Both faces are Inter — the display face carries neither glyph, and
        //FontStashSharp would silently draw blanks — and this is the larger of the two the menu already loads,
        //so the legible row costs no new font atlas.
        private const char PIP_CLEARED = '●';
        private const char PIP_OPEN = '○';

        //The tiles' widgets, kept so the writing pass can put the live state onto them: which entries the star
        //total has unlocked moves between two showings of this page (a level was just cleared), and which
        //LEVELS a tile shows at all moves when the chapter turns — while the tree itself is only rebuilt on a
        //resize. Cleared and refilled by BuildTree, since a rebuild makes new widgets.
        private readonly List<Button> _tiles = new();
        private readonly List<Label> _tileNumbers = new();
        private readonly List<Label> _tileNames = new();
        private readonly List<Label> _tileStarsEarned = new();
        private readonly List<Label> _tileStarsRest = new();
        private readonly List<Label> _pips = new();
        private Label _chapterName, _chapterLine, _totalStars, _detail;

        //The level each tile SLOT is currently showing, -1 for a slot this chapter does not reach (chapters
        //need not be the same length, so the grid is built for the longest and the surplus tiles are hidden).
        //Everything the tiles do goes through this rather than through arithmetic on the chapter's first index:
        //a tile's click, its state and its detail line then all read the same one answer.
        private int[] _slotLevel = Array.Empty<int>();

        //Where each chapter starts, in play order. Walked once per tree rather than per turn — the set does not
        //change while the game runs, and BlockRange walks the run on every call by design.
        private int[] _chapterStart = Array.Empty<int>();

        private bool _chaptered;

        //Which chapter is shown, and whether anything has chosen it yet. The page is held for the life of the
        //game, so this survives leaving and coming back — deliberately: the frontier below is the OPENING
        //guess, not the answer every time. A player who paged back to chapter 2 and played a level should come
        //back to chapter 2, not be thrown to the end of the campaign for having got that far.
        private int _chapter;
        private bool _chapterChosen;

        //Which slot the detail line is speaking for, -1 for none. Held so a MouseLeft can tell "the pointer
        //left the tile being described" from "the pointer left a tile the focus cursor had already replaced".
        private int _detailSlot = -1;

        //The entry the focus cursor stands on, as the host last said. Kept for one job: a chapter turn has to
        //re-read the nav entries (a different chapter is a different set of playable tiles) and the cursor must
        //stay where it was, which means naming the button it was on.
        private Button _focused;

        protected override Widget BuildTree()
        {
            _tiles.Clear();
            _tileNumbers.Clear();
            _tileNames.Clear();
            _tileStarsEarned.Clear();
            _tileStarsRest.Clear();
            _pips.Clear();
            _detailSlot = -1;
            _focused = null;

            int count = Game.LevelCount;

            //A chaptered set pages; anything else is the one grid it always was. Blocks are the same gate every
            //other block-aware corner of the game reads, and for the same reason: a set naming none has as many
            //runs as it has levels.
            _chaptered = count > 0 && Game.CampaignHasBlocks;

            _chapterStart = _chaptered ? FindChapterStarts() : Array.Empty<int>();
            _chapter = _chapterStart.Length == 0 ? 0 : Math.Clamp(_chapter, 0, _chapterStart.Length - 1);

            VerticalStackPanel page = MenuColumn();

            if (_chaptered) page.Widgets.Add(BuildPagerRow());
            else page.Widgets.Add(ScreenHeading("LEVEL"));

            if (_chaptered && _chapterStart.Length > 1) page.Widgets.Add(BuildPips());

            //The campaign's star total: the currency the locks below are weighed in, so the number to compare
            //their price against is on the same screen. Named in words since #273, because the chapter's own
            //readout above it now states a star figure too and the two must not be mistaken for each other.
            //Inter, for the glyph.
            _totalStars = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 26),
            };
            page.Widgets.Add(_totalStars);

            if (count == 0)
            {
                //A missing or broken set is not fatal anywhere else either — the game falls back to the
                //built-in cluster — so the picker offers that rather than an empty grid and no way forward
                _slotLevel = Array.Empty<int>();
                page.Widgets.Add(MenuButton("Built-in level", () => Game.StartGameAt(0)));
            }
            else
            {
                page.Widgets.Add(BuildGrid(count));
            }

            //The one detail line (#91): the rules a tile has no room for, spoken for the tile under the
            //pointer or under the focus cursor. A fixed height, so the page does not jump as it fills and
            //empties.
            _detail = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Height = Scaled(70),
                Margin = ScaledThickness(0, 13, 0, 13),
            };
            page.Widgets.Add(_detail);

            page.Widgets.Add(MenuButton("Back", GoBack));

            return ScreenRoot(Plate(page));
        }

        /// <summary>
        /// The grid of tiles, in the scroller both shapes of the page keep. <b>The chaptered page does not
        /// scroll in practice</b> — ten tiles in two rows sit well inside what <see cref="CHAPTER_SURROUNDINGS"/>
        /// leaves, so no bar appears — but the scroller stays, because a chapter is as long as an author makes
        /// it and a set with thirty in one run must still be reachable. It costs nothing when it is not needed
        /// and it is what the pad's own scroll-into-view (#245) hangs off.
        /// </summary>
        private ScrollViewer BuildGrid(int count)
        {
            int columns = _chaptered ? CHAPTER_COLUMNS : TILE_COLUMNS;

            //Chapters need not be the same length, so the grid is built for the longest one and the surplus
            //tiles are hidden on the shorter ones. Building for the current chapter instead would mean a fresh
            //tree on every turn, where this page's whole design is a tree written onto rather than rebuilt.
            int slots = _chaptered ? LongestChapter() : count;

            _slotLevel = new int[slots];

            //Unchaptered, a slot IS a level and stays one; chaptered, the writing pass fills this in per turn.
            for (int i = 0; i < slots; i++) _slotLevel[i] = _chaptered ? -1 : i;

            Grid grid = new()
            {
                ColumnSpacing = Scaled(26),
                RowSpacing = Scaled(26),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            for (int c = 0; c < columns; c++)
                grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            for (int i = 0; i < slots; i++)
            {
                if (i % columns == 0)
                    grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

                Button tile = BuildTile(i);

                Grid.SetColumn(tile, i % columns);
                Grid.SetRow(tile, i / columns);
                grid.Widgets.Add(tile);
            }

            //The grid scrolls; the heading, the detail line and Back do not — a campaign is as long as it is,
            //and the way out of the page must not be the thing that scrolled off the bottom
            return MenuScroll(grid, _chaptered ? CHAPTER_SURROUNDINGS : LIST_SURROUNDINGS);
        }

        /// <summary>
        /// The pager: an arrow either side of a fixed-width block holding the chapter's name and its own
        /// readout. The name is the page's heading — it stands where the generic "LEVEL" used to, since a
        /// chapter has a name of its own and two headings over one grid is one too many.
        /// </summary>
        private Widget BuildPagerRow()
        {
            _chapterName = new Label
            {
                Font = FontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            //Where the chapter's position and its progress are stated — "Chapter 3 of 9 · 7 of 10 cleared ·
            //★ 22 of 30". Attached to the name rather than given a line of its own further down, because it is
            //a fact about THIS chapter and reads as one only next to the name it belongs to.
            _chapterLine = new Label
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            VerticalStackPanel header = new()
            {
                Width = Scaled(CHAPTER_HEADER_WIDTH),
                Spacing = Scaled(8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            header.Widgets.Add(_chapterName);
            header.Widgets.Add(_chapterLine);

            HorizontalStackPanel row = new()
            {
                Spacing = Scaled(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 0, 0, 20),
            };

            //Added in the order the pad should step through them, since CollectNavEntries follows the order
            //widgets were added — so the walk reads left to right across the row, as it is drawn.
            row.Widgets.Add(ChapterArrow('◀', -1));
            row.Widgets.Add(header);
            row.Widgets.Add(ChapterArrow('▶', 1));

            return row;
        }

        /// <summary>
        /// One chapter arrow. Built over <c>MenuTile</c> like the level tiles are, so it is a real menu entry
        /// — same brushes, same click sound, same pad activation — and the glyph is Inter's: the display face
        /// the loud type is set in carries neither triangle, and FontStashSharp would silently draw a blank.
        /// </summary>
        private Button ChapterArrow(char glyph, int direction)
        {
            Label caption = new()
            {
                Text = glyph.ToString(),
                Font = FontStars,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            return Game.MenuTile(caption, () => TurnChapter(direction), ARROW_SIZE, ARROW_SIZE);
        }

        /// <summary>The row of chapter pips — the campaign at a glance, which is what paging otherwise costs.</summary>
        private Widget BuildPips()
        {
            HorizontalStackPanel row = new()
            {
                Spacing = Scaled(18),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            for (int c = 0; c < _chapterStart.Length; c++)
            {
                Label pip = new() { Font = FontStars, TextColor = BS3DGame.MENU_TEXT_DIM };

                _pips.Add(pip);
                row.Widgets.Add(pip);
            }

            return row;
        }

        /// <summary>
        /// One tile: the number loud, the name under it, the player's stars (or the lock's price) at the
        /// bottom. Built over the host's <see cref="MenuPage.Game"/>.<c>MenuTile</c>, so it is a real menu
        /// entry — same brushes, same click sound, same pad activation — merely tile-shaped.
        /// <para>
        /// It is built for a <b>slot</b> and not for a level: which level the slot shows changes with the
        /// chapter, so every one of the three things a tile does — starting a level, describing it, saying
        /// whether it is locked — reads <see cref="_slotLevel"/> at the moment it is asked.
        /// </para>
        /// </summary>
        private Button BuildTile(int slot)
        {
            VerticalStackPanel content = new()
            {
                Spacing = Scaled(6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Label number = new()
            {
                Font = Game.MenuFontHeading,
                TextColor = BS3DGame.MENU_TEXT,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Widgets.Add(number);

            Label name = new()
            {
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_BODY,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Widgets.Add(name);

            //Stars, the lock's price, or empty — written by the writing pass. Inter for the ★/☆ glyphs, which
            //the display face above does not carry.
            //
            //Two labels rather than one string, because since #139 a rating's earned run and its hollow
            //remainder are different colours — the tier's and the empty grey — and one Label is one colour.
            //The lock's price goes in the first and leaves the second empty: it is a sentence, not a rating.
            HorizontalStackPanel starRow = new() { HorizontalAlignment = HorizontalAlignment.Center };

            Label starsEarned = new() { Font = FontSmall, TextColor = BS3DGame.MENU_TEXT_DIM };
            Label starsRest = new() { Font = FontSmall, TextColor = BS3DGame.STAR_EMPTY };

            starRow.Widgets.Add(starsEarned);
            starRow.Widgets.Add(starsRest);
            content.Widgets.Add(starRow);

            Button tile = Game.MenuTile(content, () => StartSlot(slot), TILE_WIDTH, TILE_HEIGHT);

            //The pointer's half of the detail line; the focus cursor's half is NavFocusChanged. Leaving only
            //clears a description this tile still owns — the cursor may already have replaced it.
            tile.MouseEntered += (_, _) => ShowDetail(slot);
            tile.MouseLeft += (_, _) => { if (_detailSlot == slot) ShowDetail(-1); };

            _tiles.Add(tile);
            _tileNumbers.Add(number);
            _tileNames.Add(name);
            _tileStarsEarned.Add(starsEarned);
            _tileStarsRest.Add(starsRest);

            return tile;
        }

        private void StartSlot(int slot)
        {
            int level = LevelAt(slot);

            if (level >= 0) Game.StartGameAt(level);
        }

        #region The chapters

        /// <summary>
        /// Where every chapter starts, walked once per tree. It goes through <c>BlockRange</c> rather than
        /// asking each level which block number it is in: the range hands back the end of the run, so the next
        /// chapter's start is one past it and the whole walk is one pass over the set.
        /// </summary>
        private int[] FindChapterStarts()
        {
            List<int> starts = new();

            for (int index = 0; index < Game.LevelCount;)
            {
                starts.Add(index);
                Game.LevelBlockRange(index, out _, out int last);
                index = last + 1;
            }

            return starts.ToArray();
        }

        /// <summary>The last entry of chapter <paramref name="chapter"/>, inclusive.</summary>
        private int ChapterLast(int chapter) =>
            chapter + 1 < _chapterStart.Length ? _chapterStart[chapter + 1] - 1 : Game.LevelCount - 1;

        /// <summary>How many levels the longest chapter holds — how many tiles the grid is built for.</summary>
        private int LongestChapter()
        {
            int longest = 0;

            for (int c = 0; c < _chapterStart.Length; c++)
                longest = Math.Max(longest, ChapterLast(c) - _chapterStart[c] + 1);

            return longest;
        }

        /// <summary>
        /// Which chapter to open on the first time the page is shown: the <b>furthest one the player has
        /// reached</b> — the last chapter holding a level the star total has unlocked. A returning player with
        /// eight chapters behind them lands where the campaign actually is rather than paging past all of it.
        /// <para>
        /// Deliberately not "the first chapter still holding an uncleared level", which sounds like the same
        /// thing and is not: a player who skipped one level of chapter 1 and has since finished the campaign
        /// would be thrown back to the start of it every time. The frontier only ever moves forward, so it can
        /// never send the player somewhere they have left behind — and every chapter it passes is one press of
        /// ◀ away.
        /// </para>
        /// </summary>
        private int FrontierChapter()
        {
            for (int c = _chapterStart.Length - 1; c > 0; c--)
                for (int level = _chapterStart[c]; level <= ChapterLast(c); level++)
                    if (Game.IsLevelUnlocked(level)) return c;

            return 0;
        }

        /// <summary>
        /// Testing only (the <c>pick=&lt;chapter&gt;</c> argument): opens on this chapter, 1-based, rather than
        /// on the frontier. It counts as the page having been aimed by hand, so nothing overrides it afterwards
        /// — and the number is clamped where the chapters are actually known, in <see cref="BuildTree"/>, since
        /// this can be called before there is a tree at all.
        /// </summary>
        internal void PinChapter(int number)
        {
            _chapter = Math.Max(0, number - 1);
            _chapterChosen = true;
        }

        /// <summary>
        /// Turns to another chapter — what the arrows and the left/right axis both do. It <b>wraps</b>, which
        /// is what keeps the far end of a nine-chapter campaign four presses away instead of eight, and is why
        /// the arrows are never disabled and the pips have to say where the player is.
        /// </summary>
        private void TurnChapter(int direction)
        {
            int count = _chapterStart.Length;

            if (!_chaptered || count <= 1) return;

            _chapter = (_chapter + direction + count) % count;

            //Read before the walk below: re-collecting the entries raises NavFocusChanged, which writes this
            Button focused = _focused;

            ShowChapter();

            //A different chapter is a different set of playable tiles — a different number of them where
            //chapters differ in length, and a different set of them locked — so the entries the pad walks have
            //to be re-read. The cursor is kept on the entry it stood on, since the player has not gone
            //anywhere: without that it would drop to the ◀ arrow on every turn.
            Game.RefreshNavEntries(focused);
        }

        /// <summary>
        /// Writes the chapter that is showing onto the tree: which level each tile slot holds, the name, the
        /// readout under it, and the pips. Then the tiles' own state, since it is that mapping that just moved.
        /// </summary>
        private void ShowChapter()
        {
            int first = _chapterStart[_chapter];
            int last = ChapterLast(_chapter);
            int length = last - first + 1;

            for (int slot = 0; slot < _slotLevel.Length; slot++)
                _slotLevel[slot] = slot < length ? first + slot : -1;

            _chapterName.Text = (Game.LevelBlockName(first) ?? "LEVEL").ToUpperInvariant();

            int cleared = 0, stars = 0;
            bool anyOpen = false;
            int opensAt = int.MaxValue;

            for (int level = first; level <= last; level++)
            {
                int earned = Game.LevelStars(level);

                if (earned > 0) cleared++;

                stars += earned;

                if (Game.IsLevelUnlocked(level)) anyOpen = true;
                else opensAt = Math.Min(opensAt, Game.LevelMinStars(level));
            }

            string position = $"Chapter {_chapter + 1} of {_chapterStart.Length}";

            //A chapter with nothing open in it says what opens it instead of reporting a progress nobody could
            //have made. The price is the CHEAPEST gate in the run — that is the one that lets the chapter be
            //entered at all, and the tiles inside still each carry their own.
            _chapterLine.Text = anyOpen
                ? $"{position}  ·  {cleared} of {length} cleared  ·  {STAR_FILLED} {stars} of {length * StarRating.MAX}"
                : $"{position}  ·  locked  ·  opens at {opensAt} {STAR_FILLED}";

            for (int c = 0; c < _pips.Count; c++)
            {
                _pips[c].Text = IsChapterCleared(c) ? PIP_CLEARED.ToString() : PIP_OPEN.ToString();
                _pips[c].TextColor = c == _chapter ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
            }

            WriteTiles();
        }

        /// <summary>Whether every level of a chapter has been cleared — what fills its pip.</summary>
        private bool IsChapterCleared(int chapter)
        {
            for (int level = _chapterStart[chapter]; level <= ChapterLast(chapter); level++)
                if (Game.LevelStars(level) <= 0) return false;

            return true;
        }

        #endregion

        /// <summary>
        /// Left or right, wherever it came from: the previous or the next chapter. False on a page with nothing
        /// to turn between — an unchaptered set, or a set of one chapter — so the menu does not tick as though
        /// something had moved.
        /// </summary>
        internal override bool PageSideways(int direction)
        {
            if (!_chaptered || _chapterStart.Length <= 1) return false;

            TurnChapter(direction);

            return true;
        }

        /// <summary>
        /// Re-reads what has moved since the page was last up: a level cleared elsewhere raises the star total,
        /// which can open tiles and change their bottom lines — while the tree they are written onto only
        /// changes on a resize.
        /// </summary>
        internal override void Refresh()
        {
            //The tree may not exist yet: the page is only built when it is first shown
            if (_totalStars == null) return;

            _totalStars.Text = $"{STAR_FILLED} {Game.TotalStars} collected";
            _totalStars.Visible = Game.LevelCount > 0;

            ShowDetail(-1);

            if (_chaptered)
            {
                //The frontier is the opening guess and nothing more: from then on the page keeps whichever
                //chapter the player left it on, including the one they just played a level out of.
                if (!_chapterChosen)
                {
                    _chapter = FrontierChapter();
                    _chapterChosen = true;
                }

                //Writes the tiles as its last act, so this is the whole refresh in the chaptered case
                ShowChapter();
            }
            else
            {
                WriteTiles();
            }
        }

        /// <summary>
        /// The state onto the tiles: which are locked, which the player has rated and to how many stars.
        /// Disabling the tile is the whole of a lock's mechanics: Myra does not press a disabled button and the
        /// pad walk skips it (<c>CollectNavEntries</c> collects enabled ones), so there is no path left on
        /// which a locked level can be started. A slot this chapter does not reach is hidden outright, which
        /// takes it out of the walk by the same route (the walk returns on an unshown widget).
        /// </summary>
        private void WriteTiles()
        {
            for (int slot = 0; slot < _tiles.Count; slot++)
            {
                int level = LevelAt(slot);

                if (level < 0)
                {
                    _tiles[slot].Visible = false;
                    _tiles[slot].Enabled = false;
                    continue;
                }

                bool unlocked = Game.IsLevelUnlocked(level);
                int stars = Game.LevelStars(level);

                _tiles[slot].Visible = true;
                _tiles[slot].Enabled = unlocked;

                _tileNumbers[slot].Text = (level + 1).ToString();
                _tileNames[slot].Text = Game.LevelDisplayName(level);

                //A locked tile is present but visibly not an offer: its type drops to the dim grey the
                //palette keeps for asides, which on this menu IS the disabled state — emphasis is brightness,
                //never hue (see the palette comment in BS3DGame.Menu.cs).
                _tileNumbers[slot].TextColor = unlocked ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
                _tileNames[slot].TextColor = unlocked ? BS3DGame.MENU_TEXT_BODY : BS3DGame.MENU_TEXT_DIM;

                //The bottom line earns its place or stays empty: stars once there are any (an untouched
                //campaign is not a wall of hollow glyphs), the price on a lock, nothing on an open level
                //not yet cleared. The earned run carries the tier's colour, the same one the result screen
                //struck those stars in — so a level the player took to gold still reads gold here.
                bool rated = unlocked && stars > 0;

                _tileStarsEarned[slot].Text = !unlocked
                    ? $"Locked · {Game.LevelMinStars(level)} {STAR_FILLED}"
                    : rated ? StarsEarned(stars) : string.Empty;
                _tileStarsEarned[slot].TextColor = rated ? BS3DGame.StarTierColor(stars) : BS3DGame.MENU_TEXT_DIM;

                _tileStarsRest[slot].Text = rated ? StarsRemaining(stars) : string.Empty;
            }
        }

        /// <summary>The focus cursor's half of the detail line — see <c>BS3DGame.ApplyNavHighlight</c>.</summary>
        internal override void NavFocusChanged(Button focused)
        {
            _focused = focused;

            //Back, an arrow, or another page's entry should the stack change under the call: all describe
            //nothing, and IndexOf's -1 is exactly the "clear it" the detail line wants then.
            ShowDetail(focused == null ? -1 : _tiles.IndexOf(focused));
        }

        /// <summary>
        /// Points the detail line at one tile — the rules the tile has no room for, or a lock's full
        /// arithmetic: the price next to what the player actually holds.
        /// </summary>
        private void ShowDetail(int slot)
        {
            _detailSlot = slot;

            if (_detail == null) return;

            int level = LevelAt(slot);

            if (level < 0)
            {
                _detail.Text = string.Empty;
                return;
            }

            _detail.Text = Game.IsLevelUnlocked(level)
                ? $"{Game.LevelDisplayName(level)} — {Game.LevelRulesText(level)}"
                : $"{Game.LevelDisplayName(level)} — unlocks at {Game.LevelMinStars(level)} {STAR_FILLED}, you have {Game.TotalStars}";
        }

        /// <summary>The level a tile slot is showing, or -1 for no tile and for a slot this chapter leaves empty.</summary>
        private int LevelAt(int slot) => slot >= 0 && slot < _slotLevel.Length ? _slotLevel[slot] : -1;
    }
}
