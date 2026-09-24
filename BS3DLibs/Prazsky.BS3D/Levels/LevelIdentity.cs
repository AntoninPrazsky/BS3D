using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// Which online board a clear belongs to (#549, the decisions in #542): a set entry's <see cref="File"/>, and a
    /// <see cref="Hash"/> over the level file's bytes <b>and</b> the entry's rules that change what a score can be.
    /// The game's client and <c>Tools/ScoreSim</c>'s ceiling table both call <see cref="Of(LevelSetEntry, byte[])"/>,
    /// so they cannot disagree about which board a clear belongs to — the server only ever compares the two.
    /// <para>
    /// <b>What starts a fresh board and what does not.</b> A level <c>LevelGen</c> regenerates changes its bytes; a
    /// retuned <c>shots</c> or <c>ceilingStep</c> changes what a score on it can be; either one changes the hash, so
    /// a new board starts instead of the new level competing against scores made on a different one. A rename
    /// (<c>name</c>) or a re-chaptering (<c>block</c>) changes neither and keeps the board, and so does the unlock
    /// gate (<c>minStars</c>), which is over before the level is played. The file is the key
    /// <c>PlayerProgress</c> already uses, the one identifier that survives a rename (#92, #353).
    /// </para>
    /// <para>
    /// <b><c>wildcardEvery</c> is in the hash as well</b>, though #549 named only the other two: a wildcard is a
    /// free match, and how often one arrives is most of how hard a level is (see
    /// <see cref="LevelSetEntry.WildcardEvery"/>) — the same level with and without them is two different boards.
    /// No shipped entry authors it, and an absent rule hashes as absent, so it costs the shipped boards nothing.
    /// </para>
    /// <para>
    /// The scoring rules themselves are <b>not</b> in the hash: they are <c>ScoreKeeper.RulesVersion</c>, sent
    /// beside it, because a rate change invalidates every board at once and saying so once is clearer than
    /// changing a hundred hashes.
    /// </para>
    /// </summary>
    public sealed record LevelIdentity(string File, string Hash)
    {
        /// <summary>
        /// Hex characters of the SHA-256 kept: 64 bits, which no two levels of one game will ever collide in,
        /// and short enough to read in a log line and a URL.
        /// </summary>
        public const int HashLength = 16;

        /// <summary>The identity of the entry at <paramref name="index"/>, read off the set's own file for it.</summary>
        public static LevelIdentity Of(LevelSet set, int index) =>
            Of(set.Levels[index], System.IO.File.ReadAllBytes(set.ResolvePath(index)));

        /// <summary>
        /// The identity of <paramref name="entry"/> as played from <paramref name="levelBytes"/> — the bytes of the
        /// file that was actually loaded, which is what makes a run pinned to a file outside the set (#332's
        /// <c>levelfile=</c>) hash as the different level it is.
        /// <para>
        /// <b>Every carriage return is dropped before hashing; nothing else is normalized.</b> The repository has
        /// stored and checked out LF everywhere since 2026-09-21 (<c>.gitattributes</c>), but a checkout made before
        /// that keeps each file's old bytes until git next rewrites it — and measured on 2026-09-24, one such
        /// checkout held 280 tracked files CRLF in its working tree, <b>51 of them shipped level files</b>. A local
        /// build copies those into its <c>Levels</c> folder as they are, so with the CRs in the hash that build
        /// hashed <b>51 of the 120 levels differently</b> from a clean <c>git archive</c> export of the same commit —
        /// exactly the 51 — and 0 once the CRs were dropped. Every submission from it for those levels would have
        /// been refused as an unknown level, silently. The release zip is built from a fresh checkout and is LF
        /// either way; the fix is for every build that is not. After the bytes comes one line of the entry's rules, each
        /// absent rule spelled <c>-</c>: <c>\nshots=30;ceilingStep=5;wildcardEvery=-</c>. That layout is the
        /// contract — change it and every board in the wild starts again.
        /// </para>
        /// </summary>
        public static LevelIdentity Of(LevelSetEntry entry, byte[] levelBytes)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(levelBytes);

            string rules = "\nshots=" + Rule(entry.Shots) + ";ceilingStep=" + Rule(entry.CeilingStep)
                + ";wildcardEvery=" + Rule(entry.WildcardEvery);

            using IncrementalHash sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            //The bytes less every CR, fed as the runs between them rather than copied out first, so a level of
            //any size costs no second buffer
            ReadOnlySpan<byte> bytes = levelBytes;
            int start = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != (byte)'\r') continue;

                sha.AppendData(bytes.Slice(start, i - start));
                start = i + 1;
            }
            sha.AppendData(bytes.Slice(start));

            sha.AppendData(Encoding.UTF8.GetBytes(rules));

            string hex = Convert.ToHexStringLower(sha.GetHashAndReset());
            return new LevelIdentity(entry.File, hex.Substring(0, HashLength));
        }

        private static string Rule(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";

        /// <summary>The board key as a log line prints it: <c>One.json#0123456789abcdef</c>.</summary>
        public override string ToString() => File + "#" + Hash;
    }
}
