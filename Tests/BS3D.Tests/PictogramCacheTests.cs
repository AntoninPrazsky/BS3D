using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The chips' pictograms baked once a size and kept (#1146, <see cref="PictogramCache"/>): a launch after the first reads
    /// what the first drew, pixel for pixel, and rasters nothing; what the load asked for ahead is what the level's build takes;
    /// a file of another drawing's version, or a damaged one, is never read; the sizes kept are the ones last used.
    /// </summary>
    public class PictogramCacheTests
    {
        //The chips at 1920x1080, as the game measured them: what the Pi and Potato ask for
        private static readonly PictogramKey Key = new(58, 14, 9f);

        private static void AssertIsTheRaster(PictogramSet set)
        {
            for (int k = 0; k < PictogramCache.KINDS.Length; k++)
            {
                (Color[] picture, Color[] backing) = PowerupPictograms.RasterWithBacking(PictogramCache.KINDS[k], set.Key.Size, set.Key.Pad, set.Key.Soft);
                Assert.Equal(picture, set.Pictures[k]);
                Assert.Equal(backing, set.Backings[k]);
            }
        }

        [Fact]
        public void ASizeIsBakedOnceAndReadByEveryLaunchAfter()
        {
            using TempDirectory temp = new();

            var first = new PictogramCache(temp.Path);
            PictogramSet baked = first.Get(Key);
            first.WaitForWrites();
            Assert.True(baked.Baked);
            Assert.Equal(1, first.Rasters);
            Assert.True(File.Exists(first.PathFor(Key)), "the first launch keeps what it baked");
            AssertIsTheRaster(baked);

            //The next launch: a cache of its own over the same folder
            var next = new PictogramCache(temp.Path);
            PictogramSet read = next.Get(Key);
            Assert.False(read.Baked);
            Assert.Equal(0, next.Rasters);
            AssertIsTheRaster(read);
        }

        [Fact]
        public void TheBuildTakesWhatTheLoadAskedForAhead()
        {
            using TempDirectory temp = new();

            //The first launch: the load's read-ahead bakes on a worker before anything asks for the set, once however often
            //the load asks
            var first = new PictogramCache(temp.Path);
            first.Prefetch(Key);
            first.Prefetch(Key);
            WaitUntilReady(first, Key);
            Assert.Equal(1, first.Rasters);

            //and the level's build takes it rather than baking again
            PictogramSet set = first.Get(Key);
            Assert.True(set.Baked);
            Assert.Equal(1, first.Rasters);
            Assert.Same(set, first.Get(Key));
            first.WaitForWrites();

            //And a launch after it reads it ahead and rasters nothing at all
            var next = new PictogramCache(temp.Path);
            next.Prefetch(Key);
            WaitUntilReady(next, Key);
            Assert.False(next.Get(Key).Baked);
            Assert.Equal(0, next.Rasters);

            //A size nobody asked for ahead (a window resized in play) is drawn when the HUD asks
            PictogramKey other = new(40, 6, 3f);
            Assert.True(next.Get(other).Baked);
            Assert.Equal(1, next.Rasters);
            next.WaitForWrites();
        }

        [Fact]
        public void AnotherVersionOfTheDrawingIsNeverRead()
        {
            using TempDirectory temp = new();

            var old = new PictogramCache(temp.Path, version: 1);
            old.Get(Key);
            old.WaitForWrites();

            //The key a file is kept under carries the drawing's version
            Assert.NotEqual(PictogramCache.FileName(Key, 1), PictogramCache.FileName(Key, 2));

            var changed = new PictogramCache(temp.Path, version: 2);
            Assert.True(changed.Get(Key).Baked);
            Assert.Equal(1, changed.Rasters);
            changed.WaitForWrites();
            Assert.True(File.Exists(changed.PathFor(Key)));
            Assert.False(File.Exists(old.PathFor(Key)), "the old version's file goes at the next write");

            //Even a file of the old version under the new one's name is not read: its header says what it is
            File.Copy(changed.PathFor(Key), Path.Combine(temp.Path, "keep.bin"));
            var renamed = new PictogramCache(temp.Path, version: 1);
            File.Move(Path.Combine(temp.Path, "keep.bin"), renamed.PathFor(Key));
            Assert.True(renamed.Get(Key).Baked);
            renamed.WaitForWrites();
        }

        [Fact]
        public void ADamagedFileIsBakedOver()
        {
            using TempDirectory temp = new();

            var first = new PictogramCache(temp.Path);
            first.Get(Key);
            first.WaitForWrites();
            string path = first.PathFor(Key);
            byte[] whole = File.ReadAllBytes(path);

            //A flipped bit in the middle of a picture
            byte[] flipped = (byte[])whole.Clone();
            flipped[whole.Length / 2] ^= 0x10;
            File.WriteAllBytes(path, flipped);
            var second = new PictogramCache(temp.Path);
            Assert.True(second.Get(Key).Baked);
            second.WaitForWrites();
            Assert.Equal(whole, File.ReadAllBytes(path));

            //A torn write
            File.WriteAllBytes(path, whole.Take(whole.Length - 100).ToArray());
            var third = new PictogramCache(temp.Path);
            Assert.True(third.Get(Key).Baked);

            //And the whole file reads
            third.WaitForWrites();
            Assert.False(new PictogramCache(temp.Path).Get(Key).Baked);
        }

        [Fact]
        public void NoMoreSizesAreKeptThanKept()
        {
            using TempDirectory temp = new();

            var cache = new PictogramCache(temp.Path);
            for (int size = 8; size < 8 + PictogramCache.KEPT + 3; size++)
            {
                cache.Get(new PictogramKey(size, 4, 1.5f));
                cache.WaitForWrites();
            }

            Assert.Equal(PictogramCache.KEPT, Directory.GetFiles(temp.Path).Length);
            //The newest stay
            Assert.True(File.Exists(cache.PathFor(new PictogramKey(8 + PictogramCache.KEPT + 2, 4, 1.5f))));
        }

        [Fact]
        public void ASizeReadIsKeptOverOnesNotUsedSince()
        {
            using TempDirectory temp = new();

            //KEPT sizes, the first written longest ago: their times set a minute apart, so the file system clock's grain
            //decides nothing
            var cache = new PictogramCache(temp.Path);
            PictogramKey[] keys = Enumerable.Range(0, PictogramCache.KEPT).Select(i => new PictogramKey(8 + i, 4, 1.5f)).ToArray();
            foreach (PictogramKey key in keys) cache.Get(key);
            cache.WaitForWrites();
            DateTime now = DateTime.UtcNow;
            for (int i = 0; i < keys.Length; i++)
                File.SetLastWriteTimeUtc(cache.PathFor(keys[i]), now - TimeSpan.FromMinutes(keys.Length - i));

            //A later launch reads the oldest and bakes one size more: the one dropped is the oldest not used since
            var later = new PictogramCache(temp.Path);
            Assert.False(later.Get(keys[0]).Baked);
            Assert.True(later.Get(new PictogramKey(8 + PictogramCache.KEPT, 4, 1.5f)).Baked);
            later.WaitForWrites();

            Assert.Equal(PictogramCache.KEPT, Directory.GetFiles(temp.Path).Length);
            Assert.True(File.Exists(later.PathFor(keys[0])), "a size read is in use, and is kept");
            Assert.False(File.Exists(later.PathFor(keys[1])), "the least recently used size goes");
        }

        //The read-ahead's work done, on its worker, before anything takes it; a read-ahead that does nothing never gets here
        private static void WaitUntilReady(PictogramCache cache, PictogramKey key)
        {
            var clock = Stopwatch.StartNew();
            while (!cache.IsReady(key))
            {
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "the read-ahead did not read or bake the set it was asked for");
                Thread.Sleep(1);
            }
        }

        [Fact]
        public void NoFolderKeepsNothingAndStillDraws()
        {
            var cache = new PictogramCache(null);
            PictogramSet set = cache.Get(Key);
            cache.WaitForWrites();
            Assert.True(set.Baked);
            AssertIsTheRaster(set);
        }
    }
}
