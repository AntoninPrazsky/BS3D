using BS3D.Audio;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The music is decoded with headroom and its voices play that much louder (#919). Ogg Vorbis overshoots the PCM it
    /// was given - more than half of the shipped recordings decode with samples over full scale (78 of 140 when this was
    /// measured; <c>MusicBake --shipped</c> prints today's count), up to +0.90 dBFS - and the decode
    /// pressed every such sample onto the 16-bit rail (it was NVorbis's own clip, on by default, that did it).
    /// <para>
    /// Held here: a real recording that overshoots (<c>puppet-triphop</c>, a peak of 1.086) reaches the 16-bit limit
    /// at no sample once it is decoded as the music is, and does at 2 287 of them as it was; the headroom and the voice
    /// volumes are each other's inverse, so nothing got quieter, and none is over 1; and, by reading the players'
    /// source as <see cref="RecordingLoopTests"/> reads the Jukebox's, that every voice playing a headroomed decode is given
    /// a compensated volume and every decode that is not the music's keeps its own. Every recording through the game's
    /// decode is <c>MusicBake --shipped</c>'s check, which CI runs on every push (a test that decodes them all would be
    /// minutes).
    /// </para>
    /// </summary>
    public class MusicDecodeHeadroomTests
    {
        private const int MUSIC_RATE = 48000;

        //Where the decode's clip leaves a sample: 0.99999994 converts to 32766, not to 32767
        private const int RAIL = OggTrack.RAIL_SAMPLE;

        //⚠ A regenerated recording keeps its name and loses its overshoot: mirage-musicbox was this fixture until #915 made it
        //a new recording with a peak of 0.959. Of the shipped set on 2026-10-27, puppet-triphop is the one that overshoots well
        //clear of 1.05 with more than a thousand samples on the rail at unity, in a chapter no open issue regenerates
        private const string OVERSHOOTING_RECORDING = "puppet-triphop";

        [Fact]
        public void A_recording_the_codec_overshoots_decodes_with_no_sample_on_the_rail()
        {
            byte[] pcm = OggTrack.DecodeMusic(Shipped.Music(OVERSHOOTING_RECORDING), MUSIC_RATE);
            (int rail, int loudest) = Scan(pcm);

            Assert.True(pcm.Length > 0, "the recording decoded to nothing");
            Assert.True(rail == 0, $"{rail} samples of {OVERSHOOTING_RECORDING}.ogg are on the 16-bit rail");

            //Clear of it by the margin the headroom was chosen for, not touching it
            Assert.True(loudest < short.MaxValue * 0.95, $"the loudest sample is {loudest} of {short.MaxValue}");
        }

        [Fact]
        public void The_same_recording_decoded_as_it_was_is_pressed_onto_the_rail()
        {
            //The fixture means something: this is today's decode of it, and it overshoots by a good deal
            using FileStream file = File.OpenRead(Shipped.Music(OVERSHOOTING_RECORDING));
            byte[] pcm = OggTrack.Decode(file, MUSIC_RATE, 1f, out float peak);

            Assert.True(peak > 1.05f, $"{OVERSHOOTING_RECORDING}.ogg decodes to a peak of {peak}, and the test needs one over full scale");
            Assert.True(Scan(pcm).Rail > 1000, $"only {Scan(pcm).Rail} samples are on the rail at unity");

            //And the unity overload is the plain one: what the effects and the fanfares are decoded by
            Assert.Equal(pcm, OggTrack.Decode(Shipped.Music(OVERSHOOTING_RECORDING), MUSIC_RATE));
        }

        [Fact]
        public void The_headroom_in_dB_and_as_a_factor_are_one_figure()
        {
            Assert.Equal(MusicLevels.DECODE_HEADROOM_DB, 20.0 * Math.Log10(MusicLevels.DECODE_HEADROOM), 4);

            //What MusicBake holds a recording to: exactly what the headroom takes down to full scale
            Assert.Equal(1.0, MusicLevels.MAX_CODEC_PEAK * (double)MusicLevels.DECODE_HEADROOM, 6);

            //The largest overshoot measured on the shipped set, pulse-synthpop's +0.898 dBFS, is under it with a margin
            double largest = Math.Pow(10, 0.898 / 20);
            Assert.True(largest * MusicLevels.DECODE_HEADROOM < 0.95, "the headroom leaves the largest overshoot measured too close to full scale");
        }

        //The old volumes, written out: the figures the voices were given before the headroom. Raising each by the headroom's
        //inverse is what keeps the music exactly as loud as it was
        [Theory]
        [InlineData(MusicLevels.THEME_VOICE, 0.5f)]
        [InlineData(MusicLevels.MENU_VOICE, 0.29f)]
        public void A_voice_times_the_headroom_is_the_volume_it_had_and_stays_under_one(float voice, float before)
        {
            Assert.Equal(before, voice * MusicLevels.DECODE_HEADROOM, 6);
            Assert.True(voice > before, "the voice was not raised");
            Assert.True(voice <= 1f, $"a voice volume of {voice} is over 1");
        }

        [Fact]
        public void The_authored_levels_are_the_old_volumes()
        {
            Assert.Equal(0.5f, MusicLevels.THEME_LEVEL);
            Assert.Equal(0.29f, MusicLevels.MENU_LEVEL);

            //The pause's loop is the lobby's, at its level and so at its voice
            Assert.Equal(MusicLevels.MENU_LEVEL, MusicLevels.PAUSE_LEVEL);
            Assert.Equal(MusicLevels.MENU_VOICE, MusicLevels.PAUSE_VOICE);
        }

        [Fact]
        public void The_themes_voices_are_given_the_compensated_volume_and_decode_with_the_headroom()
        {
            string code = WithoutComments(File.ReadAllText(SourcePath("GameMusic.cs")));

            Assert.Contains("MusicLevels.THEME_VOICE * _gain", code);
            Assert.Contains("MusicLevels.MENU_VOICE * _gain", code);
            Assert.Contains("MusicLevels.PAUSE_VOICE * _gain", code);

            //A voice scaled by an authored figure would play a headroomed decode 1.5 dB quieter than it was
            Assert.DoesNotContain("MUSIC_VOLUME *", code);
            Assert.DoesNotContain("MENU_VOLUME *", code);
            Assert.DoesNotContain("PAUSE_VOLUME *", code);

            //One music decode (every track, the menu's, the pause's and the drums layers go through Load), and one at
            //unity: the victory fanfare's, from the effects' folder
            Assert.Equal(1, Count(code, "OggTrack.DecodeMusic("));
            Assert.Equal(1, Count(code, "OggTrack.Decode("));
            Assert.Contains("OggTrack.Decode(", Between(code, "LoadVictory(string path)", "Task<byte[]> Load(string path)"));
            Assert.Contains("OggTrack.DecodeMusic(", Between(code, "Task<byte[]> Load(string path)", "public void Dispose()"));
        }

        [Fact]
        public void The_jukeboxs_recording_player_decodes_and_plays_as_the_game_does()
        {
            string code = WithoutComments(File.ReadAllText(SourcePath("RecordingPlayer.cs")));

            Assert.Equal(1, Count(code, "OggTrack.DecodeMusic("));
            Assert.Equal(0, Count(code, "OggTrack.Decode("));
            Assert.Equal(2, Count(code, "MusicLevels.THEME_VOICE * _gain"));
            Assert.DoesNotContain("MUSIC_VOLUME", code);

            //Its bars read the level the music is, not 1.5 dB under it
            Assert.Contains("MusicLevels.DECODE_HEADROOM", code);
        }

        [Fact]
        public void The_about_pages_procedural_pieces_keep_the_authored_volume()
        {
            //Their PCM never passes the decoder, so there is no headroom to give back
            string code = WithoutComments(File.ReadAllText(SourcePath("ProceduralJukebox.cs")));

            Assert.DoesNotContain("OggTrack", code);
            Assert.Equal(2, Count(code, "GameMusic.MUSIC_VOLUME * _gain"));

            //GameMusic holds a device and cannot be compiled in: that figure is the authored level, read off its source
            string music = WithoutComments(File.ReadAllText(SourcePath("GameMusic.cs")));
            Assert.Contains("public const float MUSIC_VOLUME = MusicLevels.THEME_LEVEL;", music);
        }

        [Fact]
        public void The_sound_effects_decode_at_unity()
        {
            //ProceduralAudio loads them (and the fanfare, GameMusic) through the plain Decode, peak-normalised to 0.9 by the bake:
            //the headroom is the music's alone
            string code = WithoutComments(File.ReadAllText(SourcePath("ProceduralAudio.cs")));

            Assert.Equal(1, Count(code, "OggTrack.Decode("));
            Assert.DoesNotContain("OggTrack.DecodeMusic(", code);
        }

        //How many samples sit on the 16-bit rail, and the largest magnitude of any
        private static (int Rail, int Loudest) Scan(byte[] pcm)
        {
            int rail = 0, loudest = 0;

            for (int i = 0; i < pcm.Length; i += 2)
            {
                int v = Math.Abs((short)(pcm[i] | (pcm[i + 1] << 8)));
                if (v >= RAIL) rail++;
                if (v > loudest) loudest = v;
            }

            return (rail, loudest);
        }

        private static int Count(string source, string needle) => Regex.Matches(source, Regex.Escape(needle)).Count;

        private static string Between(string source, string from, string to)
        {
            int start = source.IndexOf(from, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{from}' is not in the source: the test needs updating");

            int end = source.IndexOf(to, start, StringComparison.Ordinal);
            Assert.True(end > start, $"'{to}' does not follow '{from}': the test needs updating");

            return source.Substring(start, end - start);
        }

        private static string WithoutComments(string source) => Regex.Replace(source, @"//[^\r\n]*", string.Empty);

        private static string SourcePath(string file)
        {
            string relative = Path.Combine("Game", "Audio", file);

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
