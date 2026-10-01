using BS3D.Audio;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #495's drums layer: at full gain the recording plays exactly as it was recorded, below it the drums (stored at
    /// half) come off it, the mix runs straight on across the loop's end, and a change of gain is a ramp, never a step.
    /// </summary>
    public class DrumLayerTests
    {
        private static byte[] Pcm(params short[] samples)
        {
            byte[] pcm = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                pcm[i * 2] = (byte)(samples[i] & 0xff);
                pcm[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xff);
            }
            return pcm;
        }

        private static short Sample(byte[] pcm, int index) => (short)(pcm[index * 2] | (pcm[index * 2 + 1] << 8));

        //Four stereo frames: the recording, and its drums as stored, at half their level
        private static readonly byte[] Full = Pcm(1000, -1000, 2000, -2000, 3000, -3000, 4000, -4000);
        private static readonly byte[] Drums = Pcm(100, -100, 200, -200, 300, -300, 400, -400);

        [Fact]
        public void AtFullGainTheRecordingPlaysUntouched()
        {
            byte[] output = new byte[16];
            DrumLayer.Mix(Full, Drums, 0, 4, 1f, 1f, output);
            Assert.Equal(Full, output);
        }

        [Fact]
        public void AtNoGainTheDrumsComeOffAtTheirTrueLevel()
        {
            byte[] output = new byte[16];
            DrumLayer.Mix(Full, Drums, 0, 4, 0f, 0f, output);

            //A stored 100 is a true 200 (STORED_SCALE 0.5)
            Assert.Equal(1000 - 200, Sample(output, 0));
            Assert.Equal(-1000 + 200, Sample(output, 1));
            Assert.Equal(4000 - 800, Sample(output, 6));
        }

        [Fact]
        public void TheMixRunsOnAcrossTheLoopsEnd()
        {
            byte[] output = new byte[12];
            int next = DrumLayer.Mix(Full, Drums, 3, 3, 1f, 1f, output);

            Assert.Equal(4000, Sample(output, 0));   //the last frame
            Assert.Equal(1000, Sample(output, 2));   //then the first again
            Assert.Equal(2000, Sample(output, 4));
            Assert.Equal(2, next);
        }

        [Fact]
        public void AChangeOfGainIsARampAcrossTheChunk()
        {
            byte[] output = new byte[16];
            DrumLayer.Mix(Full, Drums, 0, 4, 0f, 1f, output);

            //Gains 0.25, 0.5, 0.75 and 1 across the four frames: the cut is (1 - g) / 0.5 of the stored drums
            Assert.Equal(1000 - 150, Sample(output, 0));
            Assert.Equal(2000 - 200, Sample(output, 2));
            Assert.Equal(3000 - 150, Sample(output, 4));
            Assert.Equal(4000, Sample(output, 6));
        }

        [Fact]
        public void TheMixIsClampedRatherThanWrapped()
        {
            byte[] loud = Pcm(32000, -32000);
            byte[] against = Pcm(-2000, 2000);
            byte[] output = new byte[4];

            DrumLayer.Mix(loud, against, 0, 1, 0f, 0f, output);

            Assert.Equal(short.MaxValue, Sample(output, 0));
            Assert.Equal(short.MinValue, Sample(output, 1));
        }
    }
}
