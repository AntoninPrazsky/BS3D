using BS3D.Screens;
using Prazsky.BS3D.Scoring;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The result page's score breakdown stays inside its plate and clear of the online boards beside it (#1148), on the phone's
    /// split page and on a monitor's, with the widest lines a shipped level can give under rules v2: the time bonus's row, a total
    /// of six figures and the longest next-star note. <c>BreakdownLayout</c>, <c>BoardLayout</c>, <c>LevelResult</c>,
    /// <c>ScoreText</c> and <c>SpaceWrap</c> are compiled in from the Game; the faces are stand-ins in ems, read off the run log's
    /// <c>[result] breakdown</c> lines of a real clear of Pennant (24 399, "Next star at 26 180 (+1 781), or 3 s faster"): the
    /// note 464 px in the 29 px small face at 1920x1080, and the three columns about 398 px at 1080p and 875 px in the phone's
    /// layout at full size. Every page figure is a design unit scaled by the layout's share, as the menu scales them.
    /// <para>
    /// Seen to fail with the behaviour removed: with the note set on one line in its own face whatever the room (the page before
    /// #1148, which measured it apart from the faces' fit), the grid is wider than the plate's content on the phone's split page
    /// at every share and on a monitor at 4:3 and 5:4, and the first test fails; with the note broken on a monitor's page as on
    /// the split one, the page spends a line it has not got and the spending test fails; with the note's copy left untied, a
    /// narrow measure breaks a figure and the comma test fails; with the gap dropped from <c>NoteRoom</c>, the plate reaches
    /// the boards and the room's own test fails.
    /// </para>
    /// </summary>
    public class BreakdownLayoutTests
    {
        private const int DESIGN_HEIGHT = 2160;

        //BS3DGame's: the menu column, a plate's side padding and the faces (the body at the front end's size on an enlarged
        //interface, #1052)
        private const int COLUMN = 1000, PLATE_PADDING_X = 106, BODY = 80, FRONT_BODY = 108, HEADING = 124, SMALL = 58;

        //ResultPage's: the grid's column spacing and the boards' plate
        private const int GRID_SPACING = 48, BOARD_WIDTH = 780, BOARD_MIN_WIDTH = 520, BOARD_MIN_EDGE_MARGIN = 24,
            BOARD_COLUMN_GAP = 24, BOARD_PADDING = 56;

        //ResultPage's captions and the pending dash
        private static readonly string[] CAPTIONS = { "matched", "detached", "streak bonus", "balls unused", "time bonus" };
        private const string PENDING = "—";

        //The display face (Anton, condensed), in ems: its letters and tabular figures alike, a word space, the middle dot
        private static float Display(string text) => text.Sum(c => c switch
        {
            ' ' or ' ' => 0.13f,
            '·' => 0.15f,
            '—' => 0.6f,
            _ => 0.31f,
        });

        //The small face (Inter), in ems, scaled so Pennant's note reads 16.0 em as the run measured it
        private static float Small(string text) => 0.87f * text.Sum(c => c switch
        {
            ' ' or ' ' => 0.27f,
            ',' or '.' => 0.25f,
            '(' or ')' => 0.33f,
            >= '0' and <= '9' or '+' => 0.6f,
            'i' or 'l' or 'j' or 't' or 'f' or 'r' => 0.32f,
            >= 'A' and <= 'Z' => 0.68f,
            _ => 0.5f,
        });

        /// <summary>A layout: the menu's share of its design size, whether the page is split, and the window's size.</summary>
        private readonly record struct Layout(float Share, bool Split, int ScreenWidth, int ScreenHeight)
        {
            public int Scaled(int units) => Math.Max(1, (int)MathF.Round(units * Share));
            public override string ToString() => Split ? $"phone at {Share:0.000}" : $"{ScreenWidth}x{ScreenHeight}";
        }

        //The phone's split page at the shares FitPage lays it out at (the X300 Ultra's result page is 1440 * 1.31 / 2160, the
        //last the most an enlarged interface is drawn at on a 1440 px screen), and a monitor's page from 5:4 to 21:9
        public static TheoryData<float, bool, int, int> Layouts() => new()
        {
            { 0.70f, true, 3168, 1440 },
            { 0.80f, true, 3168, 1440 },
            { 1440f * 1.31f / DESIGN_HEIGHT, true, 3168, 1440 },
            { 1.0f, true, 3168, 1440 },
            { 1.2f, true, 3168, 1440 },
            { 1024 / 2160f, false, 1280, 1024 },
            { 768 / 2160f, false, 1024, 768 },
            { 1080 / 2160f, false, 1440, 1080 },
            { 1050 / 2160f, false, 1680, 1050 },
            { 720 / 2160f, false, 1280, 720 },
            { 1080 / 2160f, false, 1920, 1080 },
            { 1440 / 2160f, false, 2560, 1440 },
            { 1080 / 2160f, false, 2560, 1080 },
            { 1600 / 2160f, false, 3840, 1600 },
            { 2160 / 2160f, false, 3840, 2160 },
        };

        private static LevelResult Result(int stars, int score, int levelBalls, int matched, int orphaned, int streak, int unused,
            int ballPrice, int? par, float seconds, int timeBonus) =>
            new(cleared: true, failureText: null, stars: stars, newBest: true, levelBalls: levelBalls, levelName: "Pennant",
                levelNumber: 1, hasNextLevel: true, nextLevelUnlocked: true, nextLevelMinStars: 1, totalStars: 3,
                nextLevelBeyondReach: false, frontierLevelNumber: 2, frontierLevelName: "Rainbow", canSkip: false,
                nextLevelName: "Rainbow", campaignComplete: false, blockComplete: false, blockName: null, blockNumber: 1,
                blockCount: 14, score: score, matchedBalls: matched, orphanedBalls: orphaned, streakBonus: streak,
                hadBudget: true, unusedShotsAwarded: unused, completionBonusAwarded: unused * ballPrice, unusedShotValue: ballPrice,
                parSeconds: par, seconds: seconds, timeBonusAwarded: timeBonus);

        /// <summary>
        /// The issue's clear, the staged page's, and the widest a shipped level gives (Girandole: 1 204 balls, a par up to 115 s,
        /// a ceiling of 257 966): six figures in the values and the total, the slowest time's detail, and the longest note.
        /// </summary>
        private static IEnumerable<LevelResult> Results()
        {
            yield return Result(3, 24399, 238, 242, 0, 3860, 26, 79, 20, 12.2f, 16065);
            yield return Result(3, 9260, 120, 96, 24, 640, 7, 40, 40, 34f, 6900);

            int slowBonus = new ScoreKeeper(null, null, 1204, 115).TimeBonusAt(229);
            yield return Result(3, 40000, 1204, 1204, 999, 48160, 53, 999, 115, 229f, slowBonus);
            yield return Result(3, 99999, 1204, 1204, 999, 148160, 53, 999, 115, 999f, 120400);
            yield return Result(4, 188888, 1204, 1204, 999, 148160, 53, 999, 115, 1f, 120400);
            yield return Result(2, 18888, 1204, 1204, 999, 4816, 53, 999, null, 999f, 0);
        }

        /// <summary>What the page measures, as <c>ResultPage.CacheBreakdownText</c> writes it: the details, the values, the total.</summary>
        private static (string[] Details, string[] Values, string Total) Texts(LevelResult r) => (
            new[] { r.MatchedDetail, r.OrphanedDetail, r.UnusedDetail, r.TimeDetail },
            new[] { PENDING, ScoreText.Of(r.MatchedBalls * ScoreKeeper.MatchedBallPoints),
                ScoreText.Of(r.OrphanedBalls * ScoreKeeper.OrphanedBallPoints), ScoreText.Of(r.StreakBonus),
                ScoreText.Of(r.CompletionBonusAwarded), ScoreText.Of(r.TimeBonusAwarded) },
            ScoreText.Of(r.Score));

        private static float Widest(IEnumerable<string> texts, Func<string, float> face) =>
            texts.Max(t => MathF.Ceiling(face(t)));

        /// <summary>The page's own arithmetic at a layout: the column, its content, the gaps, the boards and the note's room.</summary>
        private readonly record struct Page(int Column, int Content, float Gaps, int BoardsLeft, int Gap, int NoteRoom, int Padding);

        private static Page PageAt(Layout l)
        {
            int column = l.Scaled(COLUMN), content = column - 2 * l.Scaled(PLATE_PADDING_X);
            int gap = l.Scaled(BOARD_COLUMN_GAP), sideFloor = Math.Max(gap, l.Scaled(BOARD_MIN_EDGE_MARGIN));
            int ceiling = BoardLayout.Ceiling(l.ScreenWidth, l.ScreenHeight, l.Scaled(BOARD_WIDTH), int.MaxValue);
            (_, _, int boardsLeft) = BoardLayout.Place(l.ScreenWidth, column, 2 * l.Scaled(BOARD_PADDING), sideFloor,
                l.Scaled(BOARD_MIN_EDGE_MARGIN), l.Scaled(BOARD_MIN_WIDTH), ceiling);
            int noteRoom = BreakdownLayout.NoteRoom(l.ScreenWidth, column, content, l.Split, boardsLeft, gap);
            return new Page(column, content, 2f * l.Scaled(GRID_SPACING), boardsLeft, gap, noteRoom, column - content);
        }

        private static BreakdownLayout.Fit Lay(Layout l, Page page, LevelResult r)
        {
            (string[] details, string[] values, string total) = Texts(r);
            float body = l.Scaled(l.Split ? FRONT_BODY : BODY), heading = l.Scaled(HEADING), small = l.Scaled(SMALL);

            return BreakdownLayout.Lay(page.Content, page.NoteRoom, heightToSpare: l.Split, page.Gaps,
                scale => new BreakdownLayout.Columns(
                    Widest(CAPTIONS, t => Display(t) * body * scale),
                    Widest(details, t => Display(t) * body * scale),
                    MathF.Max(Widest(values, t => Display(t) * body * scale), Widest(new[] { PENDING, total }, t => Display(t) * heading * scale))),
                r.NextStarNote, (scale, line) => Small(line) * small * scale, otherNotes: 0f);
        }

        [Theory]
        [MemberData(nameof(Layouts))]
        public void Every_line_of_the_breakdown_stays_inside_its_plate(float share, bool split, int width, int height)
        {
            Layout layout = new(share, split, width, height);
            Page page = PageAt(layout);

            foreach (LevelResult result in Results())
            {
                BreakdownLayout.Fit fit = Lay(layout, page, result);
                float grid = fit.Pinned.Sum + page.Gaps;

                //Where the plate is held to the column (the split page), the grid is inside its content; on a monitor, inside the
                //note's room, which is never less than that
                float room = layout.Split ? page.Content : Math.Max(page.Content, page.NoteRoom);
                Assert.True(grid <= room, $"{layout}, {result.Score}: the grid is {grid:0} px in a room of {room} px");

                //And every line of the note, as it is set and in the face it is drawn in, is inside it too: a line wider than the
                //room is the overflow itself
                foreach (string line in fit.Note.Split('\n').Where(l => l.Length > 0))
                    Assert.True(Small(line) * layout.Scaled(SMALL) * fit.NoteScale <= room, $"{layout}: '{line}' is wider than {room} px");
            }
        }

        /// <summary>
        /// What the note costs the page where the room is too narrow for it: on the phone's split page, which has the height, a
        /// second line and never a smaller face; on a monitor's page, whose height is its budget (a block's milestone with NEW
        /// BEST already reaches the frame's bottom edge at 1080 rows), never a second line, and its face no smaller than it must
        /// be. At 4:3 and 5:4, where the plate cannot widen, the widest note a shipped level gives is drawn at about 0.75 of its
        /// size, which the floor below holds it to (BreakdownLayout's MIN_NOTE_SCALE is 0.7).
        /// </summary>
        [Theory]
        [MemberData(nameof(Layouts))]
        public void The_note_spends_what_the_page_has_to_spare(float share, bool split, int width, int height)
        {
            Layout layout = new(share, split, width, height);
            Page page = PageAt(layout);

            foreach (LevelResult result in Results().Where(r => r.NextStarNote.Length > 0))
            {
                BreakdownLayout.Fit fit = Lay(layout, page, result);
                float oneLine = Small(result.NextStarNote) * layout.Scaled(SMALL);

                if (layout.Split)
                    Assert.Equal(1f, fit.NoteScale);
                else
                {
                    Assert.DoesNotContain('\n', fit.Note);
                    Assert.True(fit.NoteScale >= 0.7f, $"{layout}: the note is drawn at {fit.NoteScale:0.00} of its size");
                    if (oneLine <= page.NoteRoom) Assert.Equal(1f, fit.NoteScale);
                    else Assert.True(oneLine * fit.NoteScale > page.NoteRoom * 0.97f, $"{layout}: the note was shrunk past the room");
                }
            }
        }

        [Theory]
        [MemberData(nameof(Layouts))]
        public void A_plate_wider_than_the_column_stays_clear_of_the_boards(float share, bool split, int width, int height)
        {
            Layout layout = new(share, split, width, height);
            Page page = PageAt(layout);
            if (layout.Split) return;

            foreach (LevelResult result in Results())
            {
                BreakdownLayout.Fit fit = Lay(layout, page, result);
                float plate = Math.Max(page.Column, fit.Pinned.Sum + page.Gaps + page.Padding);
                if (plate <= page.Column) continue;

                float right = layout.ScreenWidth / 2f + plate / 2f;
                Assert.True(right + page.Gap <= page.BoardsLeft,
                    $"{layout}, {result.Score}: the plate's right edge {right:0} is within {page.Gap} px of the boards at {page.BoardsLeft}");
            }
        }

        /// <summary><c>NoteRoom</c>'s own promise, at any boards' edge: a plate of that content, centred, stops a gap short of it.</summary>
        [Theory]
        [InlineData(1920, 500, 394, 1342, 12)]
        [InlineData(1440, 500, 394, 982, 12)]
        [InlineData(1680, 486, 383, 1150, 12)]
        [InlineData(3840, 741, 584, 3100, 18)]
        [InlineData(3840, 741, 584, 2300, 18)]
        public void The_note_room_ends_a_gap_short_of_the_boards(int screen, int column, int content, int boardsLeft, int gap)
        {
            int room = BreakdownLayout.NoteRoom(screen, column, content, heldToColumn: false, boardsLeft, gap);

            Assert.True(room >= content);
            if (room > content)
                Assert.True(screen / 2f + (room + column - content) / 2f + gap <= boardsLeft,
                    $"a plate of {room + column - content} px centred on {screen} reaches {boardsLeft - gap}");

            Assert.Equal(content, BreakdownLayout.NoteRoom(screen, column, content, heldToColumn: true, boardsLeft, gap));
        }

        /// <summary>
        /// The note breaks at its comma or not at all (#1148): at every measure, from narrower than either clause to wider than
        /// the whole note, each line it is set in is the whole note, the score's clause or the time's, so no figure is parted
        /// from its group or its unit.
        /// </summary>
        [Fact]
        public void The_next_star_note_breaks_at_its_comma_or_not_at_all()
        {
            int checkedNotes = 0;

            foreach (LevelResult result in Results().Where(r => r.NextStarNote.Length > 0))
            {
                string whole = result.NextStarNote.Replace(SpaceWrap.TIE, ' ');
                int comma = whole.IndexOf(", or ", StringComparison.Ordinal);
                string[] clauses = comma < 0 ? new[] { whole } : new[] { whole[..(comma + 1)], whole[(comma + 2)..] };

                for (int width = 4; width <= whole.Length + 4; width++)
                {
                    string[] lines = SpaceWrap.Wrap(result.NextStarNote, width, l => l.Length).Split('\n');
                    Assert.True(lines.Length == 1 && lines[0] == whole || lines.SequenceEqual(clauses),
                        $"'{result.NextStarNote}' at a measure of {width} was set as '{string.Join(" | ", lines)}'");
                }

                checkedNotes++;
            }

            Assert.True(checkedNotes >= 4, $"only {checkedNotes} notes were checked");
        }
    }
}
