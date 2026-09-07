using System;

namespace MotionRunner.Track
{
    /// The tiny slice of PlayerPrefs the best-score boards need, as an interface.
    ///
    /// It exists so the keying and — far more importantly — the one-time migration of every
    /// existing player's scores can be tested in EditMode. PlayerPrefs is a static, process-wide,
    /// on-disk singleton: exercising a migration against the real thing would mean tests that
    /// depend on each other's leftovers and that can wipe the developer's own saved bests. The
    /// migration is also the one piece of this feature that runs exactly once per install and can
    /// never be re-run to fix a mistake, so it is precisely the piece that has to be proven before
    /// it ships. RunSession adapts PlayerPrefs onto these five calls; tests hand over a dictionary.
    ///
    /// Deliberately write-through and Save-free: PlayerPrefs.Save() is a disk flush with a frame
    /// cost, and the caller already knows the one moment it is worth paying (the end of a run,
    /// after both bests have been offered).
    public interface IScoreStore
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetInt(string key, int value);
        void SetString(string key, string value);
    }

    /// Best scores, split per control scheme (Feature D, owner decision 2026-09-01: camera and tilt
    /// are separate games and keep separate boards — see ControlScheme for why).
    ///
    /// ---- what this owns ------------------------------------------------------------------------
    ///
    /// Three things, all of which used to be four lines inside RunSession.Crash: the key names, the
    /// daily date-bucket guard, and the migration of the pre-split scores. They moved here together
    /// because they are one contract — the guard only means anything against the key it guards, and
    /// the migration has to copy value and date as a pair for the guard to survive the copy.
    ///
    /// ---- the keys are a wire format ------------------------------------------------------------
    ///
    /// These strings are already on players' phones. A rename does not migrate anything; it orphans
    /// real scores in a file the player cannot see and cannot recover, and the board silently reads
    /// back zero. Treat the three legacy constants and the six strings the key-builders produce as
    /// fixed. The tests pin all nine of them literally for that reason.
    ///
    /// ---- the migration --------------------------------------------------------------------------
    ///
    /// The legacy keys hold scores from before the split. Camera mode shipped late and most of that
    /// history is tilt play, so the whole of it is assigned to tilt — once. It is never assigned to
    /// camera: crediting a camera board with tilt scores would hand every camera player a personal
    /// best they never earned, on the one board that has no history to be fair about.
    ///
    /// The legacy keys are never deleted and never rewritten. Costing three PlayerPrefs entries
    /// buys the ability to roll the build back, or to re-derive the boards if the split is ever
    /// reworked, and deleting them is the one step of this that cannot be undone.
    public sealed class BestBoard
    {
        /// The pre-split keys, exactly as RunSession wrote them until 2026-09-01. Read-only from
        /// here on for ever.
        public const string LegacyAllTimeKey = "veyro.best.alltime";
        public const string LegacyDailyKey = "veyro.best.daily";
        public const string LegacyDailyDateKey = "veyro.best.daily.date";

        /// The split is a suffix on the legacy names rather than a new prefix: it keeps every score
        /// key sorting together in the prefs file, and it makes the relationship between a migrated
        /// key and the legacy key it came from obvious to anyone reading a bug report's prefs dump.
        const string TiltSuffix = ".tilt";
        const string CameraSuffix = ".camera";

        readonly IScoreStore _store;

        public BestBoard(IScoreStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// "veyro.best.alltime.tilt" / "veyro.best.alltime.camera".
        public static string AllTimeKey(ControlScheme scheme) => LegacyAllTimeKey + Suffix(scheme);

        /// "veyro.best.daily.tilt" / "veyro.best.daily.camera".
        public static string DailyKey(ControlScheme scheme) => LegacyDailyKey + Suffix(scheme);

        /// "veyro.best.daily.date.tilt" / "veyro.best.daily.date.camera" — which UTC date the daily
        /// value above belongs to.
        public static string DailyDateKey(ControlScheme scheme) => LegacyDailyDateKey + Suffix(scheme);

        static string Suffix(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? CameraSuffix : TiltSuffix;

        /// Hands the pre-split scores to the tilt board. Safe to call on every launch.
        ///
        /// Each key family is guarded by the same pair of conditions — the tilt key is absent AND
        /// the legacy key is present — and that single guard does both jobs this needs:
        ///
        ///   * it makes the migration idempotent, so it can live at startup with no "have I run
        ///     yet" flag of its own to get out of step with the data it describes;
        ///   * and it stops a *later* run of the migration from clobbering a tilt best earned after
        ///     the split. Once the player has posted a tilt score the tilt key exists, and from
        ///     then on the legacy value — which is by then strictly older and possibly lower — can
        ///     never overwrite it. Without that half, every launch would reset a tilt board back to
        ///     its pre-split value.
        ///
        /// "Legacy key present" is what keeps a fresh install clean: no legacy keys, nothing
        /// written, and the boards start empty rather than pinned at a fabricated zero.
        ///
        /// The daily family copies the PAIR — value and date together, guarded by the value key —
        /// because either one alone is meaningless. The date is what says which bucket the value
        /// belongs to, so a value without it would read as today's best (a score the player did not
        /// post today), and a date without a value would silently zero a real one. Copied as a
        /// pair, a migrated daily best from an earlier date reads back as 0 today, exactly as it
        /// did before the split, while still sitting on disk should the date come round again.
        public void Migrate()
        {
            string tiltAllTime = AllTimeKey(ControlScheme.Tilt);
            if (!_store.HasKey(tiltAllTime) && _store.HasKey(LegacyAllTimeKey))
                _store.SetInt(tiltAllTime, _store.GetInt(LegacyAllTimeKey, 0));

            string tiltDaily = DailyKey(ControlScheme.Tilt);
            if (!_store.HasKey(tiltDaily) && _store.HasKey(LegacyDailyKey))
            {
                _store.SetInt(tiltDaily, _store.GetInt(LegacyDailyKey, 0));
                _store.SetString(DailyDateKey(ControlScheme.Tilt),
                    _store.GetString(LegacyDailyDateKey, string.Empty));
            }
        }

        /// This scheme's all-time best, or 0 if it has none yet.
        public int AllTimeBest(ControlScheme scheme) => _store.GetInt(AllTimeKey(scheme), 0);

        /// This scheme's best on todayLabel's Daily Run, or 0 if what is stored belongs to an
        /// earlier date.
        ///
        /// The same rolling-bucket guard RunSession.LoadDailyBest has always used: one daily bucket
        /// per scheme on disk, rather than one key per day for ever. The date on disk is not
        /// cleared when it goes stale — nothing runs at midnight to clear it — so "is this today's"
        /// is answered on every read instead.
        public int DailyBest(ControlScheme scheme, string todayLabel)
        {
            string stored = _store.GetString(DailyDateKey(scheme), string.Empty);
            if (stored != (todayLabel ?? string.Empty)) return 0;
            return _store.GetInt(DailyKey(scheme), 0);
        }

        /// Offers a finished run's score to this scheme's all-time board. True iff it beat the
        /// board and was written — which is also the caller's cue to flush and to say "new best".
        public bool RecordAllTime(ControlScheme scheme, int score)
        {
            if (score <= AllTimeBest(scheme)) return false;
            _store.SetInt(AllTimeKey(scheme), score);
            return true;
        }

        /// Offers a finished run's score to this scheme's board for todayLabel. True iff it was
        /// written.
        ///
        /// Compared against DailyBest — the date-guarded read — and not against the raw stored
        /// value, because the bucket rolls over: yesterday's 9000 is not something today's run has
        /// to beat, so a 500 on a new date is today's best and must be written. Comparing against
        /// the raw value instead would leave a player who had one good day unable to post anything
        /// again until they beat it, on a board that is supposed to reset every morning.
        ///
        /// Writes value and date together for the same reason Migrate copies them together: the
        /// date is what makes the value mean anything.
        public bool RecordDaily(ControlScheme scheme, string todayLabel, int score)
        {
            if (score <= DailyBest(scheme, todayLabel)) return false;
            _store.SetInt(DailyKey(scheme), score);
            _store.SetString(DailyDateKey(scheme), todayLabel ?? string.Empty);
            return true;
        }
    }
}
