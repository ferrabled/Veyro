using System;
using System.Collections.Generic;
using MotionRunner.Progression;
using UnityEngine;

namespace MotionRunner.Core
{
    /// The progress the player keeps between sessions that is NOT a best score: the daily streak
    /// and the recent-runs list. Local (D10 - no backend for v1.0; T-009 is what later makes it
    /// survive a reinstall).
    ///
    /// It exists because the profile screen has to read progress that RunSession writes, and
    /// RunSession is disabled until a mode is picked - so its in-memory copies are zero the whole
    /// time the menu is up. Two places reading the same PlayerPrefs keys is exactly how a key gets
    /// misspelled in one of them, so the keys and the shape live here and both sides call in.
    ///
    /// **Best scores are deliberately NOT here.** They belong to BestBoard, which splits them per
    /// ControlScheme and owns the one-time migration of the pre-split keys (Feature D, owner call
    /// 2026-09-01). This class briefly held its own copy of the old single-board keys; that would
    /// now be a second writer to a wire format BestBoard has already moved on from, quietly
    /// filling `veyro.best.alltime` while every reader looks at `veyro.best.alltime.tilt`. Read a
    /// best through `new BestBoard(new PlayerPrefsScoreStore())` and say which scheme you mean.
    ///
    /// Thin on purpose: the rules (what a streak is, how a history is encoded) are engine-free in
    /// MotionRunner.Progression and unit tested. This is only the PlayerPrefs adapter.
    public static class ProgressStore
    {
        const string HistoryKey = "veyro.runs";
        const string StreakDayKey = "veyro.streak.day";
        const string StreakLengthKey = "veyro.streak.len";
        const string StreakMaskKey = "veyro.streak.mask";

        // ---- daily streak ----

        public static StreakRecord Streak =>
            new StreakRecord(
                PlayerPrefs.GetInt(StreakDayKey, StreakRecord.NeverPlayed),
                PlayerPrefs.GetInt(StreakLengthKey, 0),
                PlayerPrefs.GetInt(StreakMaskKey, 0));

        /// A Daily Run was finished on this day. Idempotent within a day - see DailyStreak.Record.
        public static void StampDay(int dayNumber)
        {
            var updated = DailyStreak.Record(Streak, dayNumber);
            PlayerPrefs.SetInt(StreakDayKey, updated.LastDayNumber);
            PlayerPrefs.SetInt(StreakLengthKey, updated.Length);
            PlayerPrefs.SetInt(StreakMaskKey, updated.Mask);
        }

        // ---- recent runs ----

        public static IReadOnlyList<RunRecord> History => RunHistory.Decode(PlayerPrefs.GetString(HistoryKey, string.Empty));

        public static void AppendRun(in RunRecord run) =>
            PlayerPrefs.SetString(HistoryKey, RunHistory.Append(PlayerPrefs.GetString(HistoryKey, string.Empty), run));

        // ---- one flush ----

        /// Called once by the writer after a batch of the setters above, and it flushes BestBoard's
        /// writes too - PlayerPrefs is one process-wide store and BestBoard is deliberately
        /// Save-free. Grouped rather than saved per key: a crash writes a best, a streak stamp and
        /// a history entry together, and PlayerPrefs.Save is a synchronous disk write on Android.
        public static void Flush() => PlayerPrefs.Save();

        /// The UTC day number a run belongs to, for StampDay. Here rather than at the call site so
        /// the epoch is never sampled two different ways.
        public static int DayNumberFor(DateTime utc) => DailyStreak.DayNumber(utc);
    }
}
