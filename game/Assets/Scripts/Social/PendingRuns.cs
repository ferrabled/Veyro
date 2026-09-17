using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MotionRunner.Social
{
    /// The offline queue: runs finished with no session or no network, kept until they can be
    /// submitted. Same design as RunHistory — one encoded string in one PlayerPrefs key, pure
    /// encode/decode so the round trip is unit tested, and forgiving on read because throwing on
    /// a truncated string at boot is the one unforgivable failure.
    ///
    /// Newest first, bounded: a player who spends a week offline submits their best recent runs,
    /// not an unbounded backlog (the server caps accepted runs per day anyway).
    public static class PendingRuns
    {
        public const int MaxEntries = 8;

        const char EntrySeparator = ';';
        const char FieldSeparator = '|';
        const int FieldCount = 14;

        public static readonly IReadOnlyList<RunSubmission> None = new RunSubmission[0];

        public static IReadOnlyList<RunSubmission> Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return None;

            var entries = encoded.Split(EntrySeparator);
            var runs = new List<RunSubmission>(entries.Length);
            for (int i = 0; i < entries.Length && runs.Count < MaxEntries; i++)
            {
                if (TryDecodeEntry(entries[i], out var run)) runs.Add(run);
            }
            return runs;
        }

        public static string Encode(IReadOnlyList<RunSubmission> runs)
        {
            if (runs == null || runs.Count == 0) return string.Empty;

            var text = new StringBuilder();
            int count = runs.Count < MaxEntries ? runs.Count : MaxEntries;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) text.Append(EntrySeparator);
                var run = runs[i];
                text.Append(Sanitize(run.client_run_id)).Append(FieldSeparator)
                    .Append(Sanitize(run.mode)).Append(FieldSeparator)
                    .Append(run.seed.ToString(CultureInfo.InvariantCulture)).Append(FieldSeparator)
                    .Append(Sanitize(run.content_version)).Append(FieldSeparator)
                    .Append(Sanitize(run.world_id)).Append(FieldSeparator)
                    .Append(Sanitize(run.day_label)).Append(FieldSeparator)
                    .Append(Sanitize(run.input_mode)).Append(FieldSeparator)
                    .Append(run.score.ToString(CultureInfo.InvariantCulture)).Append(FieldSeparator)
                    .Append(SocialFormat.Number(run.distance_m)).Append(FieldSeparator)
                    .Append(run.coins.ToString(CultureInfo.InvariantCulture)).Append(FieldSeparator)
                    .Append(run.best_combo.ToString(CultureInfo.InvariantCulture)).Append(FieldSeparator)
                    .Append(SocialFormat.Number(run.duration_s)).Append(FieldSeparator)
                    .Append(Sanitize(run.app_version)).Append(FieldSeparator)
                    .Append(Sanitize(run.platform));
            }
            return text.ToString();
        }

        /// Decode, push on the front, drop the oldest, re-encode.
        public static string Append(string encoded, RunSubmission run)
        {
            var runs = new List<RunSubmission>(MaxEntries) { run };
            runs.AddRange(Decode(encoded));
            return Encode(runs);
        }

        /// The queue minus one entry, matched by client_run_id — a submitted run leaves the
        /// queue whichever position it drained from.
        public static string Remove(string encoded, string clientRunId)
        {
            var runs = new List<RunSubmission>(Decode(encoded));
            runs.RemoveAll(r => r.client_run_id == clientRunId);
            return Encode(runs);
        }

        static bool TryDecodeEntry(string entry, out RunSubmission run)
        {
            run = null;
            if (string.IsNullOrEmpty(entry)) return false;

            var fields = entry.Split(FieldSeparator);
            if (fields.Length < FieldCount) return false;

            // Strict on the enums, like RunHistory's daily flag: a corrupted mode drops the
            // entry rather than refiling the run under whatever the server would reject anyway.
            if (fields[1] != RunSubmission.ModeDaily && fields[1] != RunSubmission.ModeFree) return false;
            if (fields[6] != InputModes.Tilt && fields[6] != InputModes.Camera &&
                fields[6] != InputModes.CameraFallback) return false;

            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) return false;
            if (!int.TryParse(fields[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score)) return false;
            if (!SocialFormat.TryNumber(fields[8], out float distance)) return false;
            if (!int.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int coins)) return false;
            if (!int.TryParse(fields[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int combo)) return false;
            if (!SocialFormat.TryNumber(fields[11], out float duration)) return false;

            run = new RunSubmission
            {
                client_run_id = fields[0],
                mode = fields[1],
                seed = seed,
                content_version = fields[3],
                world_id = fields[4],
                day_label = fields[5],
                input_mode = fields[6],
                score = score,
                distance_m = distance,
                coins = coins,
                best_combo = combo,
                duration_s = duration,
                app_version = fields[12],
                platform = fields[13]
            };
            return true;
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace(EntrySeparator, '-')
                .Replace(FieldSeparator, '-');
        }
    }
}
