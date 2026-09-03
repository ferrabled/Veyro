using System.Collections.Generic;
using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The per-scheme boards and — above all — the one-time migration of every existing player's
    /// scores. The migration runs exactly once per install and can never be re-run to fix a
    /// mistake, and the keys are a wire format already on phones: both are things a playtest
    /// cannot check and a rename cannot survive, so both are pinned here against an in-memory
    /// store (the reason IScoreStore exists).
    public sealed class BestBoardTests
    {
        const string Today = "2026-09-01";
        const string Yesterday = "2026-08-31";

        /// PlayerPrefs' observable behavior over the five calls the board uses, minus the disk.
        sealed class FakeScoreStore : IScoreStore
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();

            public bool HasKey(string key) => Ints.ContainsKey(key) || Strings.ContainsKey(key);

            public int GetInt(string key, int fallback) =>
                Ints.TryGetValue(key, out int value) ? value : fallback;

            public string GetString(string key, string fallback) =>
                Strings.TryGetValue(key, out string value) ? value : fallback;

            public void SetInt(string key, int value) => Ints[key] = value;
            public void SetString(string key, string value) => Strings[key] = value;

            public int KeyCount => Ints.Count + Strings.Count;
        }

        FakeScoreStore _store;
        BestBoard _board;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeScoreStore();
            _board = new BestBoard(_store);
        }

        /// A phone that played before the split: legacy keys only.
        void SeedLegacy(int allTime = 900, int daily = 400, string dailyDate = Yesterday)
        {
            _store.SetInt(BestBoard.LegacyAllTimeKey, allTime);
            _store.SetInt(BestBoard.LegacyDailyKey, daily);
            _store.SetString(BestBoard.LegacyDailyDateKey, dailyDate);
        }

        // ---- the keys are a wire format ----

        [Test]
        public void TheKeyShapesArePinned()
        {
            // These strings live in the prefs file of every installed copy. Renaming one does not
            // migrate anything — it orphans real scores the player cannot see or recover.
            Assert.AreEqual("veyro.best.alltime", BestBoard.LegacyAllTimeKey);
            Assert.AreEqual("veyro.best.daily", BestBoard.LegacyDailyKey);
            Assert.AreEqual("veyro.best.daily.date", BestBoard.LegacyDailyDateKey);

            Assert.AreEqual("veyro.best.alltime.tilt", BestBoard.AllTimeKey(ControlScheme.Tilt));
            Assert.AreEqual("veyro.best.alltime.camera", BestBoard.AllTimeKey(ControlScheme.Camera));
            Assert.AreEqual("veyro.best.daily.tilt", BestBoard.DailyKey(ControlScheme.Tilt));
            Assert.AreEqual("veyro.best.daily.camera", BestBoard.DailyKey(ControlScheme.Camera));
            Assert.AreEqual("veyro.best.daily.date.tilt", BestBoard.DailyDateKey(ControlScheme.Tilt));
            Assert.AreEqual("veyro.best.daily.date.camera", BestBoard.DailyDateKey(ControlScheme.Camera));
        }

        // ---- migration ----

        [Test]
        public void MigrationAssignsTheLegacyScoresToTilt()
        {
            SeedLegacy(allTime: 900, daily: 400, dailyDate: Yesterday);
            _board.Migrate();

            Assert.AreEqual(900, _board.AllTimeBest(ControlScheme.Tilt));
            Assert.AreEqual(400, _board.DailyBest(ControlScheme.Tilt, Yesterday),
                "the daily PAIR travels together — value and the date that scopes it");
            Assert.AreEqual(0, _board.DailyBest(ControlScheme.Tilt, Today),
                "a migrated daily from an earlier date reads as 0 today, exactly as before the split");
        }

        [Test]
        public void MigrationNeverTouchesTheCameraBoard()
        {
            SeedLegacy();
            _board.Migrate();

            // The camera board has no history to be fair about: crediting it with tilt scores
            // would hand every camera player a personal best they never earned.
            Assert.IsFalse(_store.HasKey(BestBoard.AllTimeKey(ControlScheme.Camera)));
            Assert.IsFalse(_store.HasKey(BestBoard.DailyKey(ControlScheme.Camera)));
            Assert.IsFalse(_store.HasKey(BestBoard.DailyDateKey(ControlScheme.Camera)));
            Assert.AreEqual(0, _board.AllTimeBest(ControlScheme.Camera));
        }

        [Test]
        public void MigrationNeverDeletesOrRewritesTheLegacyKeys()
        {
            SeedLegacy(allTime: 900, daily: 400, dailyDate: Yesterday);
            _board.Migrate();

            // The legacy values are the rollback path and must survive verbatim.
            Assert.AreEqual(900, _store.GetInt(BestBoard.LegacyAllTimeKey, -1));
            Assert.AreEqual(400, _store.GetInt(BestBoard.LegacyDailyKey, -1));
            Assert.AreEqual(Yesterday, _store.GetString(BestBoard.LegacyDailyDateKey, null));
        }

        [Test]
        public void MigrationIsIdempotent()
        {
            SeedLegacy(allTime: 900);
            _board.Migrate();
            int keysAfterFirst = _store.KeyCount;

            _board.Migrate();
            Assert.AreEqual(keysAfterFirst, _store.KeyCount, "a second run must change nothing");
            Assert.AreEqual(900, _board.AllTimeBest(ControlScheme.Tilt));
        }

        [Test]
        public void MigrationNeverClobbersATiltBestEarnedAfterTheSplit()
        {
            SeedLegacy(allTime: 900);
            _board.Migrate();
            _board.RecordAllTime(ControlScheme.Tilt, 1500);

            // Every launch migrates (there is no "have I run" flag); the guard is what makes that
            // safe. The legacy 900 is strictly older and must never win again.
            _board.Migrate();
            Assert.AreEqual(1500, _board.AllTimeBest(ControlScheme.Tilt));
        }

        [Test]
        public void AFreshInstallMigratesNothing()
        {
            _board.Migrate();
            Assert.AreEqual(0, _store.KeyCount,
                "no legacy keys means nothing to assign — the boards start empty, not pinned at a fabricated zero");
        }

        // ---- each scheme writes only its own keys ----

        [Test]
        public void ACameraRunTouchesOnlyTheCameraKeys()
        {
            SeedLegacy(allTime: 900, daily: 400, dailyDate: Today);
            _board.Migrate();

            Assert.IsTrue(_board.RecordAllTime(ControlScheme.Camera, 100));
            Assert.IsTrue(_board.RecordDaily(ControlScheme.Camera, Today, 100));

            Assert.AreEqual(100, _board.AllTimeBest(ControlScheme.Camera));
            Assert.AreEqual(900, _board.AllTimeBest(ControlScheme.Tilt), "tilt board untouched");
            Assert.AreEqual(400, _board.DailyBest(ControlScheme.Tilt, Today), "tilt daily untouched");
            Assert.AreEqual(900, _store.GetInt(BestBoard.LegacyAllTimeKey, -1), "legacy untouched");
            Assert.AreEqual(400, _store.GetInt(BestBoard.LegacyDailyKey, -1));
        }

        [Test]
        public void ATiltRunTouchesOnlyTheTiltKeys()
        {
            Assert.IsTrue(_board.RecordAllTime(ControlScheme.Tilt, 250));
            Assert.IsTrue(_board.RecordDaily(ControlScheme.Tilt, Today, 250));

            Assert.IsFalse(_store.HasKey(BestBoard.AllTimeKey(ControlScheme.Camera)));
            Assert.IsFalse(_store.HasKey(BestBoard.DailyKey(ControlScheme.Camera)));
            Assert.IsFalse(_store.HasKey(BestBoard.LegacyAllTimeKey),
                "the legacy keys are read-only for ever — a post-split run never writes them");
        }

        // ---- record semantics ----

        [Test]
        public void RecordAllTimeWritesOnlyImprovements()
        {
            Assert.IsTrue(_board.RecordAllTime(ControlScheme.Tilt, 500));
            Assert.IsFalse(_board.RecordAllTime(ControlScheme.Tilt, 500), "a tie is not a new best");
            Assert.IsFalse(_board.RecordAllTime(ControlScheme.Tilt, 100));
            Assert.AreEqual(500, _board.AllTimeBest(ControlScheme.Tilt));
        }

        [Test]
        public void RecordDailyWritesTheValueAndTheDateTogether()
        {
            _board.RecordDaily(ControlScheme.Camera, Today, 300);

            Assert.AreEqual(300, _store.GetInt(BestBoard.DailyKey(ControlScheme.Camera), -1));
            Assert.AreEqual(Today, _store.GetString(BestBoard.DailyDateKey(ControlScheme.Camera), null),
                "a value without its date would read as ANY day's best");
        }

        [Test]
        public void TheDailyBucketRollsOverByDate()
        {
            _board.RecordDaily(ControlScheme.Tilt, Yesterday, 9000);

            Assert.AreEqual(0, _board.DailyBest(ControlScheme.Tilt, Today),
                "yesterday's monster score is not today's bar");
            Assert.IsTrue(_board.RecordDaily(ControlScheme.Tilt, Today, 500),
                "a lower score on a NEW date is today's best and must be written");
            Assert.AreEqual(500, _board.DailyBest(ControlScheme.Tilt, Today));
        }

        [Test]
        public void RecordDailyRefusesANonImprovementWithinTheSameDate()
        {
            _board.RecordDaily(ControlScheme.Tilt, Today, 500);
            Assert.IsFalse(_board.RecordDaily(ControlScheme.Tilt, Today, 400));
            Assert.IsFalse(_board.RecordDaily(ControlScheme.Tilt, Today, 500));
            Assert.AreEqual(500, _board.DailyBest(ControlScheme.Tilt, Today));
        }
    }
}
