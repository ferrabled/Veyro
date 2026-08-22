using System;
using System.Globalization;

namespace MotionRunner.Track
{
    /// The Daily Run (handoff 6.1): everyone playing on the same UTC calendar date gets the same
    /// track, with no server involved (D10) — the date *is* the seed.
    ///
    /// Clock-free on purpose. Nothing here reads DateTime.UtcNow; the caller samples the clock and
    /// passes the date in, which keeps generation deterministic and testable (CLAUDE.md rule 4).
    public static class DailySeed
    {
        /// Seed for a UTC date, encoded as yyyyMMdd so it stays readable in logs and bug reports
        /// ("what did you play?" is answerable from the number alone). RunSeed then mixes it with
        /// the content version and the world id.
        public static int ForDate(int year, int month, int day) => year * 10000 + month * 100 + day;

        public static int ForDate(DateTime utc) => ForDate(utc.Year, utc.Month, utc.Day);

        /// "2026-08-21". Shown on the result screen, and used as the daily best-score bucket, so it
        /// is formatted with the invariant culture rather than the device's.
        public static string LabelForDate(int year, int month, int day) =>
            year.ToString("D4", CultureInfo.InvariantCulture) + "-" +
            month.ToString("D2", CultureInfo.InvariantCulture) + "-" +
            day.ToString("D2", CultureInfo.InvariantCulture);

        public static string LabelForDate(DateTime utc) => LabelForDate(utc.Year, utc.Month, utc.Day);

        /// The complete run identity for a UTC date.
        ///
        /// Note what is *not* in here: the application version. Two builds that ship the same chunk
        /// library must generate the same daily track, otherwise a bugfix release would silently
        /// split the day's players onto different tracks and make their scores incomparable. What
        /// does belong is ChunkLibrary.ContentVersion — bump that, and only that, when the library
        /// changes.
        public static RunSeed ForUtcDate(DateTime utc, string worldId) =>
            new RunSeed(ForDate(utc), ChunkLibrary.ContentVersion, worldId);

        public static RunSeed ForUtcDate(DateTime utc) => ForUtcDate(utc, RunSeed.DefaultWorldId);
    }
}
