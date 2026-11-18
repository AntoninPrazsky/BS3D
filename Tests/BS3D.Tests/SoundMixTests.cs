using BS3D.Capture;
using System;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>A capture's own mix of the game's sound (#1179): where a voice lands, how pitch, loop, stop and fades read, the pan laws and the WAV.</summary>
    public class SoundMixTests
    {
        private const int Rate = SoundMix.OUTPUT_RATE;

        //A click: one sample at full, the rest silent, at the output's own rate so a sample is a sample
        private static float[] Click(int length)
        {
            float[] signal = new float[length];
            signal[0] = 1f;
            return signal;
        }

        private static int FirstNonZero(float[] interleaved, int channel)
        {
            for (int i = channel; i < interleaved.Length; i += 2)
                if (Math.Abs(interleaved[i]) > 1e-6f) return i / 2;
            return -1;
        }

        [Theory]
        [InlineData(10.0, 10.0, 0)]
        [InlineData(10.5, 10.0, 24000)]
        //A voice on the frame at 1/60 s past the window's start lands 800 samples in: the frames and the sound are one clock
        [InlineData(10.0 + 1.0 / 60.0, 10.0, 800)]
        public void AVoiceLandsOnTheSampleOfItsClock(double start, double from, int expected)
        {
            SoundMix mix = new();
            int buffer = mix.AddBuffer(Click(100), Rate);
            mix.Begin(buffer, start, 1f, 0f, 1f, 1f, looped: false);

            float[] output = mix.Render(from, Rate);

            Assert.Equal(expected, FirstNonZero(output, 0));
        }

        [Fact]
        public void AVoiceBegunBeforeTheWindowIsHeardWhereItHasGotTo()
        {
            SoundMix mix = new();
            float[] ramp = new float[Rate];
            for (int i = 0; i < ramp.Length; i++) ramp[i] = i / (float)ramp.Length;
            int buffer = mix.AddBuffer(ramp, Rate);
            mix.Begin(buffer, 0.0, 1f, 0f, 1f, 1f, looped: false);

            //Half a second into a one-second ramp: the window's first sample is the ramp's middle
            float[] output = mix.Render(0.5, 10);

            Assert.Equal(0.5f, output[0], 3);
        }

        [Fact]
        public void AnOctaveUpPlaysTheBufferInHalfTheTime()
        {
            SoundMix mix = new();
            float[] tone = new float[Rate];
            Array.Fill(tone, 0.5f);
            int buffer = mix.AddBuffer(tone, Rate);
            mix.Begin(buffer, 0.0, 1f, 1f, 1f, 1f, looped: false);

            float[] output = mix.Render(0.0, Rate);

            Assert.NotEqual(0f, output[2 * (Rate / 2 - 10)]);
            Assert.Equal(0f, output[2 * (Rate / 2 + 10)]);
        }

        [Fact]
        public void ALoopSoundsUntilItIsStopped()
        {
            SoundMix mix = new();
            float[] tone = new float[Rate / 10];
            Array.Fill(tone, 0.5f);
            int buffer = mix.AddBuffer(tone, Rate);
            int voice = mix.Begin(buffer, 0.0, 1f, 0f, 1f, 1f, looped: true);
            mix.End(voice, 0.75);

            float[] output = mix.Render(0.0, Rate);

            Assert.NotEqual(0f, output[2 * (int)(0.7 * Rate)]);
            Assert.Equal(0f, output[2 * (int)(0.8 * Rate)]);
        }

        [Fact]
        public void AFadeIsARampBetweenItsPoints()
        {
            SoundMix mix = new();
            float[] tone = new float[Rate];
            Array.Fill(tone, 1f);
            int buffer = mix.AddBuffer(tone, Rate);
            int voice = mix.Begin(buffer, 0.0, 1f, 0f, 1f, 1f, looped: false);
            mix.Point(voice, 0.5, 1f, 0f);
            mix.Point(voice, 1.0, 0f, 0f);

            float[] output = mix.Render(0.0, Rate);

            Assert.Equal(1f, output[2 * (Rate / 4)], 3);
            Assert.Equal(0.5f, output[2 * (3 * Rate / 4)], 2);
        }

        [Fact]
        public void TheTwoChannelsTakeTheVoicesGains()
        {
            SoundMix mix = new();
            int buffer = mix.AddBuffer(Click(10), Rate);
            mix.Begin(buffer, 0.0, 0.8f, 0f, 0.25f, 0.75f, looped: false);

            float[] output = mix.Render(0.0, 10);

            Assert.Equal(0.2f, output[0], 4);
            Assert.Equal(0.6f, output[1], 4);
        }

        [Theory]
        [InlineData(0f, 1f, 1f)]
        [InlineData(1f, 0f, 1f)]
        [InlineData(-1f, 1f, 0f)]
        [InlineData(0.5f, 0.5f, 1f)]
        public void AnUnplacedVoiceKeepsMonoGamesCentreHotLaw(float pan, float left, float right)
        {
            (float l, float r) = SoundMix.CentreHotGains(pan);
            Assert.Equal(left, l, 4);
            Assert.Equal(right, r, 4);
        }

        [Theory]
        [InlineData(0f, 0.5f, 0.5f)]
        [InlineData(1f, 0f, 1f)]
        [InlineData(-1f, 1f, 0f)]
        [InlineData(0.5f, 0.25f, 0.75f)]
        public void APlacedVoicesGainsSumToOneAsX3DAudiosMatrixDoes(float side, float left, float right)
        {
            (float l, float r) = SoundMix.PlacedGains(side);
            Assert.Equal(left, l, 4);
            Assert.Equal(right, r, 4);
            Assert.Equal(1f, l + r, 4);
        }

        [Fact]
        public void TheWavIsAFloatStereoFileOfTheSamplesGiven()
        {
            float[] samples = { 0.5f, -0.25f, 1.5f, 0f };
            using MemoryStream stream = new();
            SoundMix.WriteWav(stream, samples, Rate);
            byte[] bytes = stream.ToArray();

            Assert.Equal(44 + samples.Length * 4, bytes.Length);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal(3, BitConverter.ToInt16(bytes, 20));
            Assert.Equal(2, BitConverter.ToInt16(bytes, 22));
            Assert.Equal(Rate, BitConverter.ToInt32(bytes, 24));
            Assert.Equal(32, BitConverter.ToInt16(bytes, 34));
            //Above full scale is kept, not clipped: the cut tool sets the level
            Assert.Equal(1.5f, BitConverter.ToSingle(bytes, 44 + 8));
        }
    }
}
