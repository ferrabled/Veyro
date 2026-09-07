using System;
using System.Collections.Generic;
using System.Globalization;

namespace MotionRunner.Progression
{
    /// One finished run, as the profile screen lists it. Deliberately smaller than RunSummary:
    /// this is what survives on disk for weeks, so it holds what a player recognises their run by
    /// and nothing that only mattered while the run was live.
    public readonly struct RunRecord
    {
        /// "2026-09-02" - the UTC date the run belonged to, the same label DailySeed produces.
        public readonly string DayLabel;
        public readonly bool Daily;
        public readonly int Score;
        public readonly int Coins;
        public readonly int Distance;

        public RunRecord(string dayLabel, bool daily, int score, int coins, int distance)
        {
            DayLabel = dayLabel ?? string.Empty;
            Daily = daily;
            Score = score;
            Coins = coins;
            Distance = distance;
        }
    }

    /// The player's recent runs, kept locally (D10: no backend for v1.0). The profile screen's
    /// "my last runs" is real data from the first run onward - only the leaderboard beside it is
    /// mocked, because that one genuinely needs the database that does not exist yet.
    ///
    /// Encoding is a single string so the whole list is one PlayerPrefs key: entries separated by
    /// ';', fields by '|', newest first. Pure, so the round trip is unit tested rather than
    /// discovered to be lossy on a player's phone - and forgiving on read, because the one thing
    /// this must never do is throw on startup over a truncated string.
    public static class RunHistory
    {
        /// How many runs the profile keeps. Enough to show a session's worth; short enough that
        /// the encoded string stays a few hundred bytes.
        public const int MaxEntries = 8;

        const char EntrySeparator = ';';
        const char FieldSeparator = '|';

        public static readonly IReadOnlyList<RunRecord> None = new RunRecord[0];

        /// Newest first, bounded to MaxEntries. Never throws: a string this version cannot read is
        /// an empty history, not a crash on the way to the menu.
        public static IReadOnlyList<RunRecord> Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return None;

            var entries = encoded.Split(EntrySeparator);
            var runs = new List<RunRecord>(entries.Length);
            for (int i = 0; i < entries.Length && runs.Count < MaxEntries; i++)
            {
                if (TryDecodeEntry(entries[i], out var run)) runs.Add(run);
            }
            return runs;
        }

        public static string Encode(IReadOnlyList<RunRecord> runs)
        {
            if (runs == null || runs.Count == 0) return string.Empty;

            var text = new System.Text.StringBuilder();
            int count = runs.Count < MaxEntries ? runs.Count : MaxEntries;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) text.Append(EntrySeparator);
                var run = runs[i];
                text.Append(Sanitize(run.DayLabel)).Append(FieldSeparator)
                    .Append(run.Daily ? '1' : '0').Append(FieldSeparator)
                    .Append(Number(run.Score)).Append(FieldSeparator)
                    .Append(Number(run.Coins)).Append(FieldSeparator)
                    .Append(Number(run.Distance));
            }
            return text.ToString();
        }

        /// The whole write path in one call: decode, push on the front, drop the oldest, re-encode.
        public static string Append(string encoded, in RunRecord run)
        {
            var runs = new List<RunRecord>(MaxEntries) { run };
            runs.AddRange(Decode(encoded));
            return Encode(runs);
        }

        static bool TryDecodeEntry(string entry, out RunRecord run)
        {
            run = default;
            if (string.IsNullOrEmpty(entry)) return false;

            var fields = entry.Split(FieldSeparator);
            if (fields.Length < 5) return false;

            if (!TryNumber(fields[2], out int score)) return false;
            if (!TryNumber(fields[3], out int coins)) return false;
            if (!TryNumber(fields[4], out int distance)) return false;

            run = new RunRecord(fields[0], fields[1] == "1", score, coins, distance);
            return true;
        }

        /// A day label is digits and dashes, so this only ever fires on a corrupted value - but a
        /// separator smuggled into one field would silently eat the next entry on the way back in.
        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace(EntrySeparator, '-')
                .Replace(FieldSeparator, '-');
        }

        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        static bool TryNumber(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
