using MotionRunner.Social;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The engine-free half of T-009: the input-mode/board-group vocabulary, the offline queue's
    /// encoding, and FakeProfileService's contract — which is also the contract the Supabase
    /// adapter must honour (consent gates everything, not-ready queues, nothing ever throws).
    public sealed class SocialTests
    {
        static RunSubmission Run(string id = "11111111-2222-3333-4444-555555555555")
            => new RunSubmission
            {
                client_run_id = id,
                mode = RunSubmission.ModeDaily,
                seed = 20260909,
                content_version = "greybox-1",
                world_id = "greybox",
                day_label = "2026-09-09",
                input_mode = InputModes.Tilt,
                score = 1234,
                distance_m = 1034.5f,
                coins = 20,
                best_combo = 6,
                duration_s = 61.25f,
                app_version = "0.9",
                platform = SubmissionPlatform.AndroidGooglePlay
            };

        // ---- input-mode vocabulary ----

        [Test]
        public void TiltSchemeIsTiltRegardlessOfDropFlag()
        {
            Assert.That(InputModes.For(false, false), Is.EqualTo(InputModes.Tilt));
            Assert.That(InputModes.For(false, true), Is.EqualTo(InputModes.Tilt));
        }

        [Test]
        public void CameraSchemeSplitsOnWhetherTheCameraWasDropped()
        {
            Assert.That(InputModes.For(true, false), Is.EqualTo(InputModes.Camera));
            Assert.That(InputModes.For(true, true), Is.EqualTo(InputModes.CameraFallback));
        }

        [Test]
        public void OnlyAPureCameraRunLandsOnTheCameraBoard()
        {
            // The owner's 3 Sep call: a camera run that degraded to tilt competes on the
            // standard board, never the camera board.
            Assert.That(InputModes.GroupFor(InputModes.Camera), Is.EqualTo(InputModes.GroupCamera));
            Assert.That(InputModes.GroupFor(InputModes.CameraFallback), Is.EqualTo(InputModes.GroupStandard));
            Assert.That(InputModes.GroupFor(InputModes.Tilt), Is.EqualTo(InputModes.GroupStandard));
        }

        // ---- pending queue encoding ----

        [Test]
        public void PendingQueueRoundTripsEveryField()
        {
            var run = Run();
            var decoded = PendingRuns.Decode(PendingRuns.Encode(new[] { run }));

            Assert.That(decoded.Count, Is.EqualTo(1));
            var back = decoded[0];
            Assert.That(back.client_run_id, Is.EqualTo(run.client_run_id));
            Assert.That(back.mode, Is.EqualTo(run.mode));
            Assert.That(back.seed, Is.EqualTo(run.seed));
            Assert.That(back.content_version, Is.EqualTo(run.content_version));
            Assert.That(back.world_id, Is.EqualTo(run.world_id));
            Assert.That(back.day_label, Is.EqualTo(run.day_label));
            Assert.That(back.input_mode, Is.EqualTo(run.input_mode));
            Assert.That(back.score, Is.EqualTo(run.score));
            Assert.That(back.distance_m, Is.EqualTo(run.distance_m).Within(0.01f));
            Assert.That(back.coins, Is.EqualTo(run.coins));
            Assert.That(back.best_combo, Is.EqualTo(run.best_combo));
            Assert.That(back.duration_s, Is.EqualTo(run.duration_s).Within(0.01f));
            Assert.That(back.app_version, Is.EqualTo(run.app_version));
            Assert.That(back.platform, Is.EqualTo(run.platform));
        }

        [Test]
        public void PendingQueueIsBoundedNewestFirst()
        {
            string encoded = string.Empty;
            for (int i = 0; i < PendingRuns.MaxEntries + 3; i++)
            {
                var run = Run("00000000-0000-0000-0000-" + i.ToString("000000000000"));
                run.score = i;
                encoded = PendingRuns.Append(encoded, run);
            }

            var decoded = PendingRuns.Decode(encoded);
            Assert.That(decoded.Count, Is.EqualTo(PendingRuns.MaxEntries));
            Assert.That(decoded[0].score, Is.EqualTo(PendingRuns.MaxEntries + 2), "newest kept first");
        }

        [Test]
        public void CorruptedEntriesAreDroppedNotThrown()
        {
            var good = Run();
            string encoded = PendingRuns.Encode(new[] { good }) + ";garbage|entry;|||;";
            Assert.That(PendingRuns.Decode(encoded).Count, Is.EqualTo(1));
            Assert.That(PendingRuns.Decode("total nonsense").Count, Is.EqualTo(0));
            Assert.That(PendingRuns.Decode(null).Count, Is.EqualTo(0));
        }

        [Test]
        public void ACorruptedModeOrInputModeDropsTheEntry()
        {
            var run = Run();
            string encoded = PendingRuns.Encode(new[] { run })
                .Replace(RunSubmission.ModeDaily, "dailyX");
            Assert.That(PendingRuns.Decode(encoded).Count, Is.EqualTo(0),
                "an enum the server would reject anyway is not worth re-filing");
        }

        [Test]
        public void RemoveTakesOutExactlyTheSubmittedRun()
        {
            var first = Run("aaaaaaaa-0000-0000-0000-000000000001");
            var second = Run("aaaaaaaa-0000-0000-0000-000000000002");
            string encoded = PendingRuns.Append(PendingRuns.Append(string.Empty, first), second);

            encoded = PendingRuns.Remove(encoded, first.client_run_id);
            var decoded = PendingRuns.Decode(encoded);
            Assert.That(decoded.Count, Is.EqualTo(1));
            Assert.That(decoded[0].client_run_id, Is.EqualTo(second.client_run_id));
        }

        // ---- the service contract, via the fake ----

        [Test]
        public void NotReadyQueuesAndBecomingReadyDrains()
        {
            var service = new FakeProfileService();

            SubmitOutcome? outcome = null;
            service.SubmitRun(Run(), o => outcome = o);
            Assert.That(outcome?.Status, Is.EqualTo(SubmitStatus.Queued));
            Assert.That(service.Pending.Count, Is.EqualTo(1));

            service.BecomeReady(new Profile("u", "SWIFT-FOX-42", 3, 0));
            Assert.That(service.Pending, Is.Empty);
            Assert.That(service.Submitted.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReadySubmissionReportsTheScriptedRanks()
        {
            // Joining the board is automatic (owner call, 17 Sep): a ready service submits
            // every finished run without any opt-in step.
            var service = FakeProfileService.Ready();
            service.NextDailyRank = 4;
            service.NextAlltimeRank = 17;

            SubmitOutcome? outcome = null;
            service.SubmitRun(Run(), o => outcome = o);

            Assert.That(outcome?.Status, Is.EqualTo(SubmitStatus.Accepted));
            Assert.That(outcome?.DailyRank, Is.EqualTo(4));
            Assert.That(outcome?.AlltimeRank, Is.EqualTo(17));
        }

        [Test]
        public void RerollsAreUnlimitedAndAlwaysChangeTheHandle()
        {
            // Owner call, 17 Sep: no budget - players reroll until the name feels right.
            var service = FakeProfileService.Ready();

            string previous = service.Current.Handle;
            for (int i = 0; i < 10; i++)
            {
                Profile after = null;
                SocialError error = null;
                service.RerollHandle((p, e) => { after = p; error = e; });
                Assert.That(error, Is.Null);
                Assert.That(after.Handle, Is.Not.EqualTo(previous));
                previous = after.Handle;
            }
        }

        [Test]
        public void DeleteClearsTheProfileAndStaysFailOpen()
        {
            var service = FakeProfileService.Ready();

            SocialError error = new SocialError("sentinel", "never cleared");
            service.DeleteAccount(e => error = e);

            Assert.That(error, Is.Null);
            Assert.That(service.Current, Is.Null);
            Assert.That(service.IsReady, Is.False);

            // Fail-open afterwards: every call still completes, nothing throws.
            SubmitOutcome? outcome = null;
            service.SubmitRun(Run(), o => outcome = o);
            Assert.That(outcome?.Status, Is.EqualTo(SubmitStatus.Queued));

            BoardResult board = new BoardResult(null, 0, 0);
            SocialError boardError = null;
            service.FetchBoard(default, (b, e) => { board = b; boardError = e; });
            Assert.That(board, Is.Null);
            Assert.That(boardError, Is.Not.Null);
        }
    }
}
