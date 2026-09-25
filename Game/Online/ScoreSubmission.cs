using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BS3D.Online
{
    /// <summary>
    /// One cleared level as the score service's <c>POST /v1/scores</c> takes it — contract v1 of #542, field
    /// for field. It is also what <c>Outbox.json</c> holds, so a clear waiting for the service is the very
    /// request that will be sent, not a note from which one will be rebuilt.
    /// <para>
    /// <b>Every clear is one, not only a new best</b>: the month's board ranks the best clear each player made
    /// <i>in that month</i>, so a September clear below an all-time best still stands in September. The
    /// service is idempotent on <see cref="SubmissionId"/>, which is what lets the outbox resend after a
    /// timeout without a clear ever counting twice.
    /// </para>
    /// </summary>
    internal sealed class ScoreSubmission
    {
        [JsonPropertyName("submissionId")]
        public Guid SubmissionId { get; set; }

        [JsonPropertyName("playerId")]
        public Guid PlayerId { get; set; }

        /// <summary>The nickname at the moment of the clear; the service answers with its normalized form.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("level")]
        public SubmittedLevel Level { get; set; }

        /// <summary><c>ScoreKeeper.RulesVersion</c> — the third part of a board's key, beside the level's two.</summary>
        [JsonPropertyName("rulesVersion")]
        public int RulesVersion { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        /// <summary>Shots fired up to the moment the field emptied — not the ones fired into the clear's beat.</summary>
        [JsonPropertyName("shotsUsed")]
        public int ShotsUsed { get; set; }

        /// <summary>Seconds of play from the level's start to the clear: a pause and an unfocused window are not play.</summary>
        [JsonPropertyName("durationSeconds")]
        public float DurationSeconds { get; set; }

        /// <summary>
        /// The release this build came out of (<c>v0.2.0</c>), or <c>dev-&lt;short sha&gt;</c> for a build that
        /// did not — the name <c>release.yml</c> gives a rehearsal run, so the two read the same way everywhere.
        /// </summary>
        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; }
    }

    /// <summary>Which board: the set entry's file and its <c>LevelIdentity</c> hash.</summary>
    internal sealed class SubmittedLevel
    {
        [JsonPropertyName("file")]
        public string File { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }

    /// <summary><c>Outbox.json</c>: the submissions not yet delivered, oldest first.</summary>
    internal sealed class OutboxFile
    {
        internal const string FormatMarker = "bs3d-outbox";
        internal const int CurrentVersion = 1;

        [JsonPropertyName("format")]
        public string Format { get; set; } = FormatMarker;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("submissions")]
        public List<ScoreSubmission> Submissions { get; set; } = new();
    }

    /// <summary>
    /// The service's answer to a submission, as contract v1 words it. Read leniently: a field the service does
    /// not send stays at its default, and nothing here refuses an answer for carrying more than this knows.
    /// </summary>
    internal sealed class ScoreAnswerBody
    {
        [JsonPropertyName("accepted")]
        public bool? Accepted { get; set; }

        [JsonPropertyName("personalBest")]
        public bool PersonalBest { get; set; }

        [JsonPropertyName("month")]
        public BoardRank Month { get; set; }

        [JsonPropertyName("allTime")]
        public BoardRank AllTime { get; set; }

        /// <summary>A refusal's reason code (422), when the service gives one.</summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// The nickname in the service's own normalized form (#544, #548), when it sends one — written back into
        /// <c>Online.json</c> so the settings page shows the name the boards show.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>The body of <c>PUT /v1/players/{id}</c>: a new nickname (#548).</summary>
    internal sealed class PlayerNameBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    internal enum OnlineNoticeKind : byte
    {
        /// <summary>The service forgot this player and the outbox is gone; the identity is the frame's to delete.</summary>
        Removed,

        /// <summary>The removal did not happen; <see cref="OnlineNotice.Text"/> says why. Nothing was removed.</summary>
        RemoveFailed,

        /// <summary>The service answered with its own form of the nickname, in <see cref="OnlineNotice.Text"/>.</summary>
        NameNormalized,

        /// <summary>The service refused the nickname; <see cref="OnlineNotice.Text"/> is its reason.</summary>
        NameRefused,
    }

    /// <summary>What became of one of the player's own requests (#548) — a removal or a name — handed to the frame.</summary>
    internal readonly struct OnlineNotice
    {
        public readonly OnlineNoticeKind Kind;
        public readonly string Text;

        public OnlineNotice(OnlineNoticeKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    internal sealed class BoardRank
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    /// <summary>What became of one submission, as far as the result page is concerned.</summary>
    internal enum OnlineOutcome : byte
    {
        /// <summary>The service took it and ranked it.</summary>
        Accepted,

        /// <summary>The service refused it for good (a 4xx other than 408 and 429); it will not be sent again.</summary>
        Refused,

        /// <summary>No answer — no network, the service down, a timeout, a 5xx. It waits in the outbox.</summary>
        Offline,
    }

    /// <summary>
    /// One submission's fate, handed from the client's worker to the frame. A struct and a queue of them, so the
    /// frame's check for news allocates nothing when there is none — which is every frame but a handful.
    /// </summary>
    internal readonly struct OnlineAnswer
    {
        public readonly Guid SubmissionId;
        public readonly OnlineOutcome Outcome;
        public readonly bool PersonalBest;
        public readonly int MonthRank;
        public readonly int MonthTotal;
        public readonly int AllTimeRank;
        public readonly int AllTimeTotal;

        public OnlineAnswer(Guid submissionId, OnlineOutcome outcome, ScoreAnswerBody body = null)
        {
            SubmissionId = submissionId;
            Outcome = outcome;
            PersonalBest = body?.PersonalBest ?? false;
            MonthRank = body?.Month?.Rank ?? 0;
            MonthTotal = body?.Month?.Total ?? 0;
            AllTimeRank = body?.AllTime?.Rank ?? 0;
            AllTimeTotal = body?.AllTime?.Total ?? 0;
        }
    }
}
