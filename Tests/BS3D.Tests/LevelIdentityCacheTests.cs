using BS3D.Screens;
using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The High Scores page's levels' board keys, read off the frame thread (#898, the cross-review of 2026-10-17). The page used
    /// to call <c>LevelIdentity.Of</c> - which reads and hashes a level file - for every level of the set on the first frame a
    /// summary arrived. <see cref="LevelIdentityCache"/> reads them on a pool thread and lets the frame ask only what is ready.
    /// These hold what the page relies on: the cache answers exactly what <c>LevelIdentity.Of</c> answers, it is filled on
    /// another thread, a level not read yet says so instead of blocking, and a failed read neither stalls the rest nor leaves
    /// the page waiting for ever.
    /// </summary>
    public class LevelIdentityCacheTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        [Fact]
        public async Task It_answers_what_LevelIdentity_Of_answers_for_every_shipped_level()
        {
            LevelSet set = LevelSet.Load(System.IO.Path.Combine(Shipped.LevelsDirectory, LevelSet.DefaultFileName));
            LevelIdentityCache cache = new(set.Count, level => LevelIdentity.Of(set, level));

            await cache.Start().WaitAsync(Patience);

            Assert.True(cache.IsComplete);
            Assert.Equal(set.Count, cache.Ready);

            for (int level = 0; level < set.Count; level++)
            {
                Assert.True(cache.TryGet(level, out LevelIdentity cached), $"level {level} was not read");
                Assert.Equal(LevelIdentity.Of(set, level), cached);
            }
        }

        [Fact]
        public async Task It_is_filled_off_the_thread_that_asked_and_a_level_not_read_yet_says_so()
        {
            using ManualResetEventSlim hold = new(false);
            int worker = -1;

            LevelIdentityCache cache = new(3, level =>
            {
                worker = Environment.CurrentManagedThreadId;
                if (level == 1) hold.Wait(Patience);
                return new LevelIdentity($"L{level}.json", "0123456789abcdef");
            });

            Task warm = cache.Start();

            //The first level is in while the second is held on its "disk"; the frame is not blocked by it
            Stopwatch wait = Stopwatch.StartNew();
            while (!cache.TryGet(0, out _))
            {
                Assert.True(wait.Elapsed < Patience, "the first level never arrived");
                await Task.Delay(1);
            }

            Assert.False(cache.TryGet(1, out LevelIdentity unread));
            Assert.Null(unread);
            Assert.False(cache.TryGet(2, out _));
            Assert.False(cache.IsComplete);
            Assert.Equal(1, cache.Ready);
            Assert.NotEqual(Environment.CurrentManagedThreadId, worker);

            hold.Set();
            await warm.WaitAsync(Patience);

            Assert.True(cache.IsComplete);
            Assert.True(cache.TryGet(1, out LevelIdentity second));
            Assert.Equal("L1.json", second.File);
        }

        [Fact]
        public async Task Starting_again_reads_each_level_once()
        {
            int reads = 0;
            LevelIdentityCache cache = new(5, level =>
            {
                Interlocked.Increment(ref reads);
                return new LevelIdentity($"L{level}.json", "0123456789abcdef");
            });

            Task first = cache.Start();
            Task again = cache.Start();
            Assert.Same(first, again);
            await first.WaitAsync(Patience);
            Assert.Same(first, cache.Start());

            Assert.Equal(5, reads);
        }

        [Fact]
        public async Task A_level_that_will_not_read_is_read_with_no_identity_and_the_others_still_arrive()
        {
            LevelIdentityCache cache = new(3, level => level switch
            {
                0 => null,
                1 => throw new InvalidOperationException("a file that vanished mid-read"),
                _ => new LevelIdentity("Two.json", "0123456789abcdef"),
            });

            await cache.Start().WaitAsync(Patience);

            //Read - so the page does not wait on it - with nothing to key a board by
            Assert.True(cache.IsComplete);
            Assert.True(cache.TryGet(0, out LevelIdentity none));
            Assert.Null(none);
            Assert.True(cache.TryGet(1, out LevelIdentity failed));
            Assert.Null(failed);
            Assert.True(cache.TryGet(2, out LevelIdentity fine));
            Assert.Equal("Two.json", fine.File);
        }

        [Fact]
        public async Task A_level_outside_the_set_is_never_ready_and_an_empty_set_is_complete_at_once()
        {
            LevelIdentityCache cache = new(2, level => new LevelIdentity("One.json", "0123456789abcdef"));
            await cache.Start().WaitAsync(Patience);

            Assert.False(cache.TryGet(-1, out _));
            Assert.False(cache.TryGet(2, out _));

            LevelIdentityCache empty = new(0, level => throw new InvalidOperationException("nothing to read"));
            Assert.True(empty.IsComplete);
            await empty.Start().WaitAsync(Patience);
            Assert.False(empty.TryGet(0, out _));
        }

        [Fact]
        public void The_page_never_reads_a_level_file_on_the_frame()
        {
            //The page may call the cache and nothing else for a level's key; LevelIdentityOf, which reads and hashes the
            //file, is handed to the cache's worker and named nowhere else in it.
            string source = Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Shipped.LevelsDirectory)!, "Screens", "HighScoresPage.cs")), @"//[^\r\n]*", string.Empty);

            int first = source.IndexOf("LevelIdentityOf", StringComparison.Ordinal);
            Assert.True(first >= 0, "the page no longer hands the read to the cache");
            Assert.Equal(first, source.LastIndexOf("LevelIdentityOf", StringComparison.Ordinal));
            Assert.DoesNotContain("LevelIdentity.Of(", source);
            Assert.Contains("new LevelIdentityCache(Game.LevelCount, Game.LevelIdentityOf)", source);
        }
    }
}
