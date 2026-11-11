using BS3D.Audio;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #1077: on Android the victory's sounds outrun the 32 voices MonoGame plays and the fanfare could be the one dropped.
    /// The choice of which voice gives way is the <see cref="VoiceLedger{TVoice}"/>'s, pure bookkeeping held here with stand-in
    /// voices; the <c>[audio]</c> line is the <see cref="VoiceReport"/>'s. That every sound of the game plays through
    /// <c>VoiceBudget</c>, so a class is named for each, is held on the source, with the rule seen to fire on the call
    /// this change replaced (BestPractices section 10).
    /// </summary>
    public class VoiceLedgerTests
    {
        private sealed class Voice
        {
            public bool Playing = true;
        }

        private static VoiceLedger<Voice> Ledger(int capacity = 8) => new(capacity, voice => voice.Playing);

        [Fact]
        public void A_higher_sound_takes_the_oldest_voice_of_the_lowest_class_below_it()
        {
            VoiceLedger<Voice> ledger = Ledger();
            Voice shot = new(), burstOld = new(), burstNew = new(), chime = new();
            ledger.Began(shot, VoiceClass.Play, "shot", 10);
            ledger.Began(burstOld, VoiceClass.Crowd, "firework-burst", 20);
            ledger.Began(burstNew, VoiceClass.Crowd, "firework-burst", 30);
            ledger.Began(chime, VoiceClass.Event, "star", 5);

            //The fanfare: the lowest class first (Crowd, not the older shot), and of it the oldest
            int victim = ledger.Victim(VoiceClass.Music);
            Assert.Same(burstOld, ledger.VoiceAt(victim));
            Assert.Equal("firework-burst", ledger.NameAt(victim));

            //With the crowd gone, a star takes the shot; the star's own class and above are never taken for it
            burstOld.Playing = false;
            burstNew.Playing = false;
            Assert.Same(shot, ledger.VoiceAt(ledger.Victim(VoiceClass.Event)));
            Assert.Equal(-1, ledger.Victim(VoiceClass.Play));
        }

        [Fact]
        public void The_lowest_class_gives_way_and_never_takes()
        {
            VoiceLedger<Voice> ledger = Ledger();
            ledger.Began(new Voice(), VoiceClass.Crowd, "volley-tick", 1);
            ledger.Began(new Voice(), VoiceClass.Crowd, "volley-tick", 2);

            Assert.Equal(-1, ledger.Victim(VoiceClass.Crowd));
        }

        [Fact]
        public void A_voice_that_has_ended_is_never_taken_and_is_forgotten()
        {
            VoiceLedger<Voice> ledger = Ledger();
            Voice ended = new() { Playing = false }, playing = new();
            ledger.Began(ended, VoiceClass.Crowd, "firework-burst", 1);
            ledger.Began(playing, VoiceClass.Play, "shot", 2);

            Assert.Same(playing, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));

            //Forgotten: brought back to life, it is not in the ledger to be taken
            ended.Playing = true;
            Assert.Same(playing, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));
        }

        [Fact]
        public void The_music_takes_the_scenes_bed_only_when_nothing_lower_plays()
        {
            VoiceLedger<Voice> ledger = Ledger();
            Voice bed = new(), theme = new();
            ledger.Began(bed, VoiceClass.Event, "ambience", 1);
            ledger.Began(theme, VoiceClass.Music, "theme", 2);

            Assert.Same(bed, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));

            bed.Playing = false;
            Assert.Equal(-1, ledger.Victim(VoiceClass.Music));
        }

        [Fact]
        public void A_voice_played_again_keeps_its_slot_and_its_new_start()
        {
            //Two slots: a ring's voice played again must not take the other's
            VoiceLedger<Voice> ledger = Ledger(capacity: 2);
            Voice ring = new(), other = new();
            ledger.Began(ring, VoiceClass.Crowd, "firework-burst", 1);
            ledger.Began(other, VoiceClass.Crowd, "firework-burst", 2);
            ledger.Began(ring, VoiceClass.Crowd, "firework-burst", 3);

            //Both still known, and the replayed one is now the newer
            Assert.Same(other, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));
        }

        [Fact]
        public void A_full_ledger_reuses_an_ended_voices_slot_before_the_oldest()
        {
            VoiceLedger<Voice> ledger = Ledger(capacity: 3);
            Voice oldest = new(), ended = new(), newest = new(), arriving = new();
            ledger.Began(oldest, VoiceClass.Crowd, "a", 1);
            ledger.Began(ended, VoiceClass.Crowd, "b", 2);
            ledger.Began(newest, VoiceClass.Crowd, "c", 3);
            ended.Playing = false;

            ledger.Began(arriving, VoiceClass.Crowd, "d", 4);
            Assert.Same(oldest, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));

            //And with every slot playing, the oldest is the one forgotten
            Voice late = new();
            ledger.Began(late, VoiceClass.Crowd, "e", 5);
            Assert.Same(newest, ledger.VoiceAt(ledger.Victim(VoiceClass.Music)));
        }

        [Fact]
        public void Counting_what_plays_goes_by_class()
        {
            VoiceLedger<Voice> ledger = Ledger();
            ledger.Began(new Voice(), VoiceClass.Crowd, "firework-burst", 1);
            ledger.Began(new Voice(), VoiceClass.Crowd, "firework-burst", 2);
            ledger.Began(new Voice { Playing = false }, VoiceClass.Crowd, "firework-burst", 3);
            ledger.Began(new Voice(), VoiceClass.Music, "theme", 4);

            int[] held = new int[4];
            ledger.CountHolding(held);
            Assert.Equal(new[] { 2, 0, 0, 1 }, held);
        }

        [Fact]
        public void The_report_waits_for_quiet_then_says_what_gave_way_for_what()
        {
            VoiceReport report = new();
            report.Held[(int)VoiceClass.Crowd] = 20;
            report.Held[(int)VoiceClass.Music] = 1;
            report.Stopped("firework-burst", "fanfare", 1000);
            report.Stopped("firework-burst", "star", 1100);
            report.Stopped("firework-burst", "star", 1200);
            report.Dropped("volley-tick", 1300);

            Assert.True(report.Pending);
            Assert.Null(report.Flush(1300 + 1499, 1500));

            string line = report.Flush(1300 + 1500, 1500);
            Assert.Equal("[audio] at the voice limit for 0.3 s, tracked voices playing as it began: crowd 20, play 0, event 0, music 1;"
                + " stopped firework-burst for fanfare x1, firework-burst for star x2; dropped volley-tick x1", line);

            //Said once, and the next episode starts from nothing
            Assert.False(report.Pending);
            Assert.Null(report.Flush(10_000, 1500));
            report.Dropped("ui-click", 20_000);
            Assert.Equal("[audio] at the voice limit for 0.0 s, tracked voices playing as it began: crowd 0, play 0, event 0, music 0;"
                + " dropped ui-click x1", report.Flush(30_000, 1500));
        }

        [Fact]
        public void The_report_counts_past_its_pairs_as_others()
        {
            VoiceReport report = new();
            for (int i = 0; i < 15; i++) report.Dropped("sound-" + i, i);

            string line = report.Flush(100_000, 1500);
            Assert.EndsWith("; 3 others", line);
            Assert.Contains("sound-11 x1", line);
            Assert.DoesNotContain("sound-12", line);
        }

        //A sound played around the door: an instance's or a buffer's own Play or Resume, on a line that is not a comment
        private static readonly Regex PlayedAround = new(@"^(?!\s*//).*\.(Play|Resume)\(.*$", RegexOptions.Multiline | RegexOptions.Compiled);

        //What is not a sound played around it: a ring's own door, and the music director's and the session's own Play
        private static readonly Regex NotASound = new(@"\b(ring|_volleyTickRing|_music|Music\?)\.Play\(", RegexOptions.Compiled);

        [Fact]
        public void Every_sound_of_the_game_plays_through_the_budget()
        {
            string game = FindUnderRepository("Game");
            string[] sources = Directory.GetFiles(game, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    && !path.EndsWith("VoiceBudget.cs", StringComparison.Ordinal))
                .ToArray();
            Assert.Contains(sources, path => path.EndsWith("ProceduralAudio.cs", StringComparison.Ordinal));

            foreach (string path in sources)
            {
                foreach (Match match in PlayedAround.Matches(File.ReadAllText(path)))
                {
                    if (NotASound.IsMatch(match.Value)) continue;
                    Assert.Fail($"{Path.GetFileName(path)} plays a sound around VoiceBudget: '{match.Value.Trim()}'");
                }
            }
        }

        [Fact]
        public void The_source_rule_fires_on_the_calls_it_replaced()
        {
            Assert.Matches(PlayedAround, "            _partyPopper.Play(0.9f * Level * NON_SPATIAL_TRIM, NextPitch(0.08f), 0f);");
            Assert.Matches(PlayedAround, "            _streakBreak[index]?.Play(STREAK_BREAK_VOLUME * level, NextPitch(0.02f), 0f);");
            Assert.Matches(PlayedAround, "                _fanfare.Play();");
            Assert.Matches(PlayedAround, "            else _voice.Resume();");

            Assert.DoesNotMatch(PlayedAround, "            VoiceBudget.TryPlay(_partyPopper, 0.9f, 0f, 0f, VoiceClass.Event, \"party-popper\");");
            Assert.DoesNotMatch(PlayedAround, "            //_fanfare.Play() was the old way");
            Assert.Matches(NotASound, "                ring.Play(voice);");
            Assert.DoesNotMatch(NotASound, "                _fanfare.Play();");
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
