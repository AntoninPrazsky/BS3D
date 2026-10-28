using BS3D.Online;
using BS3D.Screens;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Top Players (#827): a ranking page from the service is made safe before the frame reads it, as a board page is, and
    /// what the page says is held to the copy rules.
    /// </summary>
    public class RankingTests
    {
        [Fact]
        public void APageIsMadeSafeForTheFrame()
        {
            var page = new RankingPageBody
            {
                Total = -4,
                Entries = new List<RankingEntryBody>
                {
                    new() { Rank = 1, Name = "Karel", Levels = 58, Stars = 221, Score = 1_804_320 },
                    null,
                    new() { Rank = -2, Name = "  ", Levels = -1, Stars = -3, Score = -10 },
                    new() { Rank = 3, Name = "Third", Levels = 1, Stars = 1, Score = 1 },
                },
                Me = new RankingMeBody { Rank = -1, Levels = -1, Stars = -1, Score = -1 },
            };

            RankingPageBody safe = OnlineScores.SanitizeRanking(page, limit: 2);

            Assert.Equal(0, safe.Total);
            Assert.Equal(2, safe.Entries.Count);
            Assert.Equal("Karel", safe.Entries[0].Name);
            Assert.Equal(0, safe.Entries[1].Rank);
            Assert.Equal(0, safe.Entries[1].Levels);
            Assert.Equal(0, safe.Entries[1].Stars);
            Assert.Equal(0, safe.Entries[1].Score);
            Assert.Equal(0, safe.Me.Rank);
            Assert.Equal(0, safe.Me.Score);
        }

        [Fact]
        public void AnExplicitNullListComesBackEmpty()
        {
            RankingPageBody safe = OnlineScores.SanitizeRanking(new RankingPageBody { Entries = null }, limit: 10);
            Assert.NotNull(safe.Entries);
            Assert.Empty(safe.Entries);
        }

        [Fact]
        public void ThePlayersLineSaysWhereTheyStand()
        {
            Assert.Equal("You lead · 58 levels, ★ 221, 1 804 320", RankingCopy.YouLine(1, 37, 58, 221, 1_804_320));
            Assert.Equal("You: 12th of 37 · 1 level, ★ 3, 444 065", RankingCopy.YouLine(12, 37, 1, 3, 444_065));
            Assert.Equal("You are not ranked yet: clear a level to the end to join", RankingCopy.YouLine(0, 37, 0, 0, 0));
            Assert.Equal("Nobody is ranked yet", RankingCopy.YouLine(0, 0, 0, 0, 0));
        }

        [Fact]
        public void ThePagersLineIsOnlyForMoreThanAPage()
        {
            Assert.Equal("Ranks 11 to 20 of 37", RankingCopy.PageLine(10, 10, 37));
            Assert.Equal("Ranks 31 to 37 of 37", RankingCopy.PageLine(30, 10, 37));
            Assert.Equal(" ", RankingCopy.PageLine(0, 10, 7));
        }

        [Fact]
        public void WhatThePageSaysKeepsTheCopyRules()
        {
            string[] lines =
            {
                RankingCopy.TITLE, RankingCopy.LOADING, RankingCopy.OFF, RankingCopy.NO_SERVER, RankingCopy.NOT_OFFERED,
                RankingCopy.NO_ANSWER, RankingCopy.YouLine(2, 5, 3, 7, 1234), RankingCopy.PageLine(10, 10, 37),
            };

            foreach (string line in lines)
                Assert.Null(CopyRules.Fault(line) is { } fault ? $"'{line}': {fault}" : null);
        }
    }
}
