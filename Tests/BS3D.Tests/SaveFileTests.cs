using Prazsky.BS3D.Levels;
using Prazsky.Core.Tools;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The player's files: <see cref="AtomicFile"/>'s swap and backup generation (#353, #571), and every outcome
    /// <see cref="PlayerProgress.Load"/> can report, including the copy it keeps of a save it could not read.
    /// </summary>
    public class SaveFileTests
    {
        [Fact]
        public void AtomicWriteKeepsThePreviousContentsAsTheBackup()
        {
            using TempDirectory temp = new();
            string path = temp.File(Path.Combine("sub", "file.json"));

            AtomicFile.WriteText(path, "first", ".bak");
            Assert.Equal("first", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".bak"), "the first write has nothing to back up");

            AtomicFile.WriteText(path, "second", ".bak");
            Assert.Equal("second", File.ReadAllText(path));
            Assert.Equal("first", File.ReadAllText(path + ".bak"));

            AtomicFile.WriteText(path, "third", ".bak");
            Assert.Equal("third", File.ReadAllText(path));
            Assert.Equal("second", File.ReadAllText(path + ".bak"));

            Assert.False(File.Exists(path + ".tmp"), "the temporary file is swapped in, never left beside the target");
        }

        [Fact]
        public void AtomicWriteWithoutASuffixKeepsNoBackup()
        {
            using TempDirectory temp = new();
            string path = temp.File("level.json");

            AtomicFile.WriteText(path, "first", backupSuffix: null);
            AtomicFile.WriteText(path, "second", backupSuffix: null);

            Assert.Equal("second", File.ReadAllText(path));
            Assert.Equal(["level.json"], System.Array.ConvertAll(Directory.GetFiles(temp.Path), Path.GetFileName));
        }

        [Fact]
        public void KeepUnreadableCopiesTheFile()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");

            Assert.Null(AtomicFile.KeepUnreadable(path));

            File.WriteAllText(path, "{ not json");
            string kept = AtomicFile.KeepUnreadable(path);

            Assert.NotNull(kept);
            Assert.StartsWith(path + ".unreadable-", kept);
            Assert.Equal("{ not json", File.ReadAllText(kept));
            Assert.Equal("{ not json", File.ReadAllText(path));
        }

        private static PlayerProgress SavedProgress(string path, int score)
        {
            PlayerProgress progress = PlayerProgress.Load(path);
            progress.Record("One.json", score, 2);
            progress.Save();

            return progress;
        }

        [Fact]
        public void NothingThereIsFresh()
        {
            using TempDirectory temp = new();
            PlayerProgress progress = PlayerProgress.Load(temp.File("Progress.json"));

            Assert.Equal(ProgressLoad.Fresh, progress.Outcome);
            Assert.Null(progress.KeptUnreadable);
            Assert.Empty(progress.Levels);
            Assert.Equal(temp.File("Progress.json"), progress.Path);
        }

        [Fact]
        public void ASaveReadsBackAsLoaded()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");
            SavedProgress(path, 1234);

            PlayerProgress progress = PlayerProgress.Load(path);

            Assert.Equal(ProgressLoad.Loaded, progress.Outcome);
            Assert.Null(progress.KeptUnreadable);
            Assert.Equal(1234, progress.ScoreFor("One.json"));
            Assert.Equal(2, progress.StarsFor("One.json"));
        }

        [Fact]
        public void ABrokenSaveIsRecoveredFromItsBackupAndKept()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");

            SavedProgress(path, 100);
            PlayerProgress second = PlayerProgress.Load(path);
            second.Record("One.json", 200, 3);
            second.Save(); //the first save is now the backup

            File.WriteAllText(path, "{\"format\":\"bs3d-progress\",\"lev"); //cut short

            PlayerProgress progress = PlayerProgress.Load(path);

            Assert.Equal(ProgressLoad.RecoveredFromBackup, progress.Outcome);
            Assert.Equal(100, progress.ScoreFor("One.json"));
            Assert.Equal(path, progress.Path); //bound back to the real save, never to the backup

            Assert.NotNull(progress.KeptUnreadable);
            Assert.Equal("{\"format\":\"bs3d-progress\",\"lev", File.ReadAllText(progress.KeptUnreadable));
        }

        [Fact]
        public void ABrokenSaveWithNoBackupIsDiscardedAndKept()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");
            File.WriteAllText(path, "not a save");

            PlayerProgress progress = PlayerProgress.Load(path);

            Assert.Equal(ProgressLoad.Discarded, progress.Outcome);
            Assert.Empty(progress.Levels);
            Assert.NotNull(progress.KeptUnreadable);
            Assert.Equal("not a save", File.ReadAllText(progress.KeptUnreadable));
        }

        /// <summary>
        /// The #353/#571 incident's shape: a save a NEWER build wrote does not read here, and it must survive
        /// the saves this build then makes, which demote it to the backup and then overwrite the backup.
        /// </summary>
        [Fact]
        public void ANewerBuildsSaveIsKeptThroughTwoSaves()
        {
            using TempDirectory temp = new();
            string path = temp.File("Progress.json");
            string newer = "{\"format\":\"bs3d-progress\",\"version\":" + (PlayerProgress.CurrentVersion + 1) + ",\"levels\":{}}";
            File.WriteAllText(path, newer);

            PlayerProgress progress = PlayerProgress.Load(path);
            Assert.Equal(ProgressLoad.Discarded, progress.Outcome);
            Assert.NotNull(progress.KeptUnreadable);

            progress.Record("One.json", 1, 1);
            progress.Save();
            progress.Save();

            Assert.Equal(newer, File.ReadAllText(progress.KeptUnreadable));
        }
    }
}
