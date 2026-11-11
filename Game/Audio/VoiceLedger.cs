using System;

namespace BS3D.Audio
{
    /// <summary>
    /// What a sound is worth when the platform has no voice left for it (#1077): a sound of a higher class stops the
    /// oldest playing voice of the lowest class below it, and a sound of the lowest class is the one that gives way.
    /// Decided once, where the sound is played: a <c>VoiceRing</c> carries its own, and every other play names one
    /// to <see cref="VoiceBudget"/>.
    /// </summary>
    internal enum VoiceClass : byte
    {
        /// <summary>Many of one kind at once, none of them missed alone: a firework's report and whistle, the volley's
        /// count ticks.</summary>
        Crowd = 0,

        /// <summary>The play's own answers: the shot, a landing, a release, a power-up, a blast, the weather.</summary>
        Play = 1,

        /// <summary>The moments: the loss's cut, a card's or the result page's chime, a menu click, the line's hum and
        /// the scene's bed.</summary>
        Event = 2,

        /// <summary>The music and the fanfares: never dropped while a voice of any class below plays.</summary>
        Music = 3,
    }

    /// <summary>
    /// The voices this game has started and not yet seen end, with each one's <see cref="VoiceClass"/> and when it began:
    /// what <see cref="VoiceBudget"/> asks which voice gives way for a sound the platform has no voice for (#1077). Pure
    /// bookkeeping over fixed arrays, so the tests hold the choice without a sound device; the voice type is the
    /// caller's, and <c>playing</c> is how the ledger asks one whether it still holds a voice.
    /// <para>
    /// <b>Bounded, and forgetful by design.</b> A slot is reused for a voice that is played again, then for one that has
    /// ended, then for the oldest. A voice the ledger has forgotten can only be one that is not stolen, never one that
    /// is stolen wrongly: it plays on.
    /// </para>
    /// </summary>
    /// <typeparam name="TVoice">The voice: a <c>SoundEffectInstance</c> in the game, a stand-in in the tests.</typeparam>
    internal sealed class VoiceLedger<TVoice> where TVoice : class
    {
        private readonly TVoice[] _voices;
        private readonly VoiceClass[] _classes;
        private readonly string[] _names;
        private readonly long[] _started;
        private readonly Func<TVoice, bool> _playing;

        /// <param name="capacity">How many voices it remembers: more than the platform can play at once.</param>
        /// <param name="playing">Whether a voice still holds one of the platform's voices (playing or paused).</param>
        internal VoiceLedger(int capacity, Func<TVoice, bool> playing)
        {
            _voices = new TVoice[capacity];
            _classes = new VoiceClass[capacity];
            _names = new string[capacity];
            _started = new long[capacity];
            _playing = playing;
        }

        /// <summary>Notes that <paramref name="voice"/> began to play at <paramref name="now"/> (any monotonic clock).</summary>
        internal void Began(TVoice voice, VoiceClass voiceClass, string name, long now)
        {
            //The voice's own slot (a ring's voice played again), else an empty one: reference compares, no native call
            int slot = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (ReferenceEquals(_voices[i], voice))
                {
                    slot = i;
                    break;
                }

                if (slot < 0 && _voices[i] == null) slot = i;
            }

            //Else one whose voice has ended, else the oldest: asked of the voices only when the ledger is full
            if (slot < 0)
            {
                long oldest = long.MaxValue;
                for (int i = 0; i < _voices.Length; i++)
                {
                    if (!_playing(_voices[i]))
                    {
                        slot = i;
                        break;
                    }

                    if (_started[i] < oldest)
                    {
                        oldest = _started[i];
                        slot = i;
                    }
                }
            }

            _voices[slot] = voice;
            _classes[slot] = voiceClass;
            _names[slot] = name;
            _started[slot] = now;
        }

        /// <summary>
        /// The slot of the voice to stop so a sound of <paramref name="wanting"/> can play: the lowest class below it,
        /// and of that class the one that began longest ago. -1 when no voice of a lower class is playing. A voice found
        /// ended on the way is forgotten.
        /// </summary>
        internal int Victim(VoiceClass wanting)
        {
            int best = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i] == null || _classes[i] >= wanting) continue;

                //Only a voice that would beat the one in hand is asked whether it still plays
                if (best >= 0 && (_classes[i] > _classes[best] || (_classes[i] == _classes[best] && _started[i] >= _started[best])))
                    continue;

                if (!_playing(_voices[i]))
                {
                    Forget(i);
                    continue;
                }

                best = i;
            }

            return best;
        }

        /// <summary>
        /// Counts the voices that still hold a voice into <paramref name="perClass"/>, indexed by <see cref="VoiceClass"/>:
        /// what was playing when the limit was met, for its <c>[audio]</c> line. Asks every voice, so once an episode.
        /// </summary>
        internal void CountHolding(int[] perClass)
        {
            Array.Clear(perClass);
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i] != null && _playing(_voices[i])) perClass[(int)_classes[i]]++;
        }

        /// <summary>The voice in <paramref name="slot"/>.</summary>
        internal TVoice VoiceAt(int slot) => _voices[slot];

        /// <summary>The name the voice in <paramref name="slot"/> was played under.</summary>
        internal string NameAt(int slot) => _names[slot];

        /// <summary>Forgets the voice in <paramref name="slot"/>: it was stopped, or has ended.</summary>
        internal void Forget(int slot)
        {
            _voices[slot] = null;
            _names[slot] = null;
        }
    }

    /// <summary>
    /// What the voice limit cost over one episode (#1077): which sound gave way for which, and which were dropped, counted
    /// by name and said in one line once the limit has been quiet, so a report like "the fanfare did not play" can be
    /// checked against the run log. Counting allocates nothing; only the line does, once an episode.
    /// </summary>
    internal sealed class VoiceReport
    {
        //Distinct (gave way, for) pairs an episode lists; any more are counted as "others"
        private const int PAIRS = 12;

        private readonly string[] _gaveWay = new string[PAIRS];
        private readonly string[] _for = new string[PAIRS];
        private readonly int[] _counts = new int[PAIRS];
        private int _pairs, _others;
        private long _first, _last;

        /// <summary>The voices the ledger saw playing when the episode began, by <see cref="VoiceClass"/>: filled by
        /// <see cref="VoiceLedger{TVoice}.CountHolding"/>, read by the line.</summary>
        internal int[] Held { get; } = new int[(int)VoiceClass.Music + 1];

        /// <summary>Whether an episode is being counted: something has been stopped or dropped since the last line.</summary>
        internal bool Pending => _pairs > 0 || _others > 0;

        /// <summary><paramref name="gaveWay"/> was stopped so <paramref name="forSound"/> could play.</summary>
        internal void Stopped(string gaveWay, string forSound, long now) => Note(gaveWay, forSound, now);

        /// <summary><paramref name="sound"/> found no voice and was not played.</summary>
        internal void Dropped(string sound, long now) => Note(sound, null, now);

        /// <summary>
        /// The episode's line, once <paramref name="quietMs"/> have passed since the last thing it counted, and the count
        /// starts again; null before then or when there is nothing to say.
        /// </summary>
        internal string Flush(long now, long quietMs)
        {
            if (!Pending || now - _last < quietMs) return null;

            var line = new System.Text.StringBuilder("[audio] at the voice limit for ");
            line.Append(((_last - _first) / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" s, tracked voices playing as it began: crowd ").Append(Held[(int)VoiceClass.Crowd])
                .Append(", play ").Append(Held[(int)VoiceClass.Play])
                .Append(", event ").Append(Held[(int)VoiceClass.Event])
                .Append(", music ").Append(Held[(int)VoiceClass.Music]);

            Append(line, "; stopped ", stopped: true);
            Append(line, "; dropped ", stopped: false);
            if (_others > 0) line.Append("; ").Append(_others).Append(" others");

            _pairs = 0;
            _others = 0;
            Array.Clear(Held);
            return line.ToString();
        }

        private void Append(System.Text.StringBuilder line, string head, bool stopped)
        {
            bool first = true;
            for (int i = 0; i < _pairs; i++)
            {
                if ((_for[i] != null) != stopped) continue;

                line.Append(first ? head : ", ").Append(_gaveWay[i]);
                if (stopped) line.Append(" for ").Append(_for[i]);
                line.Append(" x").Append(_counts[i]);
                first = false;
            }
        }

        private void Note(string gaveWay, string forSound, long now)
        {
            if (!Pending) _first = now;
            _last = now;

            for (int i = 0; i < _pairs; i++)
            {
                if (string.Equals(_gaveWay[i], gaveWay, StringComparison.Ordinal) && string.Equals(_for[i], forSound, StringComparison.Ordinal))
                {
                    _counts[i]++;
                    return;
                }
            }

            if (_pairs == PAIRS)
            {
                _others++;
                return;
            }

            _gaveWay[_pairs] = gaveWay;
            _for[_pairs] = forSound;
            _counts[_pairs] = 1;
            _pairs++;
        }
    }
}
