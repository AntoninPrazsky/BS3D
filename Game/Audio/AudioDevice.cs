using Microsoft.Xna.Framework.Audio;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// Whether the sound engine has an output device at all (#923), asked once. A machine with every audio endpoint
    /// unplugged, disabled or asleep (a monitor's speakers over its display cable, gone with the screen) leaves
    /// MonoGame's engine without a device, and the first thing that needs one - a streaming voice - fails with a
    /// <see cref="NullReferenceException"/> out of SharpDX's <c>SourceVoice</c> rather than anything that says so (the
    /// engine's static state is null and nothing checks it). The music, the Jukebox and the About page's player each
    /// used to find that out for themselves through a <c>catch</c> and a log line that blamed an object reference; they
    /// now ask here, so the whole game states it once and plays on silently.
    /// <para>
    /// <b>The answer is final for the run.</b> MonoGame builds its engine once, when the first sound is made, and
    /// never again: a device that appears later (the monitor waking) is not picked up, and a restart brings the sound
    /// back. The probe is one throw-away streaming voice, made and disposed on the thread that asks - the same kind
    /// of voice the theme needs, so a pass means the theme's own creation will pass too. The effects
    /// (<c>ProceduralAudio</c>) need no gate: their instances come off <c>SoundEffect</c> and stay inert without a
    /// device, as MonoGame intends.
    /// </para>
    /// </summary>
    internal static class AudioDevice
    {
        private static bool _probed;
        private static bool _present;

        /// <summary>
        /// True when a streaming voice can be made. The first call probes (and, when there is no device, prints the one
        /// <c>[audio]</c> line); every later call is a field read.
        /// </summary>
        internal static bool Present
        {
            get
            {
                if (!_probed) Probe();
                return _present;
            }
        }

        private static void Probe()
        {
            _probed = true;

            try
            {
                using DynamicSoundEffectInstance voice = new(ProceduralMusic.SAMPLE_RATE, AudioChannels.Stereo);
                _present = true;
            }
            catch (Exception exception)
            {
                //Said once, with the reason's own type: a NullReferenceException here is the engine without a device, and
                //anything else would be a different fault worth reading
                _present = false;
                Console.WriteLine($"[audio] no audio device: playing without sound until the game is restarted ({exception.GetType().Name})");
            }
        }
    }
}
