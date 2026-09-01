using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The pause state machine and the back-button mapping. Both are pure, which is the point:
    /// "pause released the camera and resume never got it back" and "back quit the app mid-run"
    /// are not failures a playtest reliably reproduces.
    public sealed class PauseStateTests
    {
        PauseState _pause;

        [SetUp]
        public void SetUp() => _pause = new PauseState();

        [Test]
        public void ARunStartsRunning()
        {
            _pause.BeginRun(false);
            Assert.AreEqual(PausePhase.Running, _pause.Phase);
            Assert.IsFalse(_pause.IsFrozen);
        }

        [Test]
        public void Pause_FreezesTheRun()
        {
            _pause.BeginRun(false);
            Assert.IsTrue(_pause.Pause());
            Assert.AreEqual(PausePhase.Paused, _pause.Phase);
            Assert.IsTrue(_pause.IsFrozen);
        }

        [Test]
        public void Pause_IsIdempotent()
        {
            _pause.BeginRun(false);
            _pause.Pause();
            Assert.IsFalse(_pause.Pause(), "a second pause must not re-open the pause menu");
        }

        [Test]
        public void Resume_StaysFrozenUntilItIsFinished()
        {
            _pause.BeginRun(false);
            _pause.Pause();

            Assert.IsTrue(_pause.BeginResume());
            Assert.AreEqual(PausePhase.Resuming, _pause.Phase);
            Assert.IsTrue(_pause.IsFrozen, "the world must not move while the resume is still landing");

            Assert.IsTrue(_pause.FinishResume());
            Assert.AreEqual(PausePhase.Running, _pause.Phase);
            Assert.IsFalse(_pause.IsFrozen);
        }

        [Test]
        public void Resume_OnlyStartsFromPaused()
        {
            _pause.BeginRun(false);
            Assert.IsFalse(_pause.BeginResume());
            Assert.AreEqual(PausePhase.Running, _pause.Phase);
        }

        [Test]
        public void FinishResume_DoesNothingWhenNothingIsResuming()
        {
            _pause.BeginRun(false);
            _pause.Pause();
            Assert.IsFalse(_pause.FinishResume(), "a stale ready-callback must not unfreeze a paused run");
            Assert.AreEqual(PausePhase.Paused, _pause.Phase);
        }

        [Test]
        public void Pause_CancelsAResumeInFlight()
        {
            _pause.BeginRun(true);
            _pause.Pause();
            _pause.BeginResume();

            Assert.IsTrue(_pause.Pause());
            Assert.AreEqual(PausePhase.Paused, _pause.Phase);
            Assert.IsFalse(_pause.HoldsCamera, "cancelling must put the camera back down");
        }

        [Test]
        public void TiltMode_NeverHoldsTheCamera()
        {
            _pause.BeginRun(false);
            Assert.IsFalse(_pause.HoldsCamera);
            _pause.Pause();
            Assert.IsFalse(_pause.HoldsCamera);
        }

        [Test]
        public void CameraMode_ReleasesTheCameraWhilePausedAndTakesItBackToResume()
        {
            _pause.BeginRun(true);
            Assert.IsTrue(_pause.HoldsCamera);

            _pause.Pause();
            Assert.IsFalse(_pause.HoldsCamera, "a paused run must not hold the camera");

            _pause.BeginResume();
            Assert.IsTrue(_pause.HoldsCamera, "re-acquisition needs the camera before the run does");

            _pause.FinishResume();
            Assert.IsTrue(_pause.HoldsCamera);
        }

        [Test]
        public void CameraGivingUp_FinishesTheRunOnTilt()
        {
            _pause.BeginRun(true);
            _pause.Pause();
            _pause.BeginResume();

            Assert.IsTrue(_pause.DropCameraMode());
            Assert.IsFalse(_pause.CameraMode);
            Assert.IsFalse(_pause.HoldsCamera);
            Assert.AreEqual(PausePhase.Paused, _pause.Phase,
                "the player chooses when to go back in, rather than being dropped into a moving run");

            // ...and resuming from there works exactly like a tilt run.
            Assert.IsTrue(_pause.BeginResume());
            Assert.IsTrue(_pause.FinishResume());
            Assert.IsFalse(_pause.IsFrozen);
        }

        [Test]
        public void BeginRun_ClearsThePreviousRunsPause()
        {
            _pause.BeginRun(true);
            _pause.Pause();

            _pause.BeginRun(false);
            Assert.AreEqual(PausePhase.Running, _pause.Phase);
            Assert.IsFalse(_pause.IsFrozen);
            Assert.IsFalse(_pause.CameraMode);
        }

        [Test]
        public void Back_PausesALiveRun()
        {
            Assert.AreEqual(BackAction.Pause, PauseState.BackFor(PausePhase.Running, true, false));
        }

        [Test]
        public void Back_ResumesFromThePauseMenu()
        {
            Assert.AreEqual(BackAction.Resume, PauseState.BackFor(PausePhase.Paused, true, false));
        }

        [Test]
        public void Back_CancelsAResumeInFlight()
        {
            Assert.AreEqual(BackAction.Pause, PauseState.BackFor(PausePhase.Resuming, true, false));
        }

        [Test]
        public void Back_LeavesForTheMenuFromTheResultScreen()
        {
            Assert.AreEqual(BackAction.QuitToMenu, PauseState.BackFor(PausePhase.Running, false, false));
        }

        [Test]
        public void Back_BelongsToAnOverlayThatOwnsTheScreen()
        {
            Assert.AreEqual(BackAction.Ignore, PauseState.BackFor(PausePhase.Running, true, true));
            Assert.AreEqual(BackAction.Ignore, PauseState.BackFor(PausePhase.Paused, true, true));
            Assert.AreEqual(BackAction.Ignore, PauseState.BackFor(PausePhase.Running, false, true));
        }

        [Test]
        public void Back_DoesNothingAtTheModePicker()
        {
            // No session on screen: back belongs to Android, not to a run that does not exist.
            Assert.AreEqual(BackAction.Ignore,
                PauseState.BackFor(PausePhase.Running, false, false, guideOpen: false, inSession: false));
        }

        [Test]
        public void Back_AfterLeavingTheResultScreenBelongsToAndroidAgain()
        {
            // The result screen's QUIT TO MENU button ends exactly where the pause menu's does:
            // RunFlow.QuitToMenu resets the state to a fresh un-started run and disables the
            // session, so back stops meaning "leave the result screen" the moment the picker is
            // up. Without inSession the phase alone still reads as the result screen and back
            // would try to quit a run that no longer exists.
            Assert.AreEqual(BackAction.QuitToMenu,
                PauseState.BackFor(PausePhase.Running, false, false), "on the result screen");

            _pause.BeginRun(false); // what QuitToMenu leaves behind before it shows the picker
            Assert.AreEqual(BackAction.Ignore,
                PauseState.BackFor(_pause.Phase, false, false, guideOpen: false, inSession: false),
                "at the picker the result screen reached");
        }

        [Test]
        public void Back_ClosesTheGuideBeforeAnythingElse()
        {
            // The guide is the top-most screen wherever it is opened from, so it takes back off
            // every other rule - including the mode picker, where back is otherwise ignored and
            // would fall straight through the guide to Android.
            foreach (PausePhase phase in System.Enum.GetValues(typeof(PausePhase)))
                foreach (bool runLive in new[] { true, false })
                    foreach (bool overlayOpen in new[] { true, false })
                        foreach (bool inSession in new[] { true, false })
                            Assert.AreEqual(BackAction.CloseGuide,
                                PauseState.BackFor(phase, runLive, overlayOpen, true, inSession),
                                "back must close the guide in phase " + phase);
        }

        [Test]
        public void Back_KeepsItsOldMeaningWhileNoGuideIsUp()
        {
            // The guide's arrival must not have moved anything underneath it.
            Assert.AreEqual(BackAction.Pause,
                PauseState.BackFor(PausePhase.Running, true, false, false, true));
            Assert.AreEqual(BackAction.Resume,
                PauseState.BackFor(PausePhase.Paused, true, false, false, true));
            Assert.AreEqual(BackAction.QuitToMenu,
                PauseState.BackFor(PausePhase.Running, false, false, false, true));
            Assert.AreEqual(BackAction.Ignore,
                PauseState.BackFor(PausePhase.Running, true, true, false, true));
        }

        [Test]
        public void Back_CancelsCameraStagingAtTheModePicker()
        {
            // The rule-3 hole this closes: while the picker stages the camera, both mode buttons
            // are disabled (tapping one is what started it) and there is no session, so back was
            // Ignore — a player the camera never finds could only force-quit the app. Verified on
            // the device before this existed.
            Assert.AreEqual(BackAction.CancelStaging,
                PauseState.BackFor(PausePhase.Running, false, false,
                    guideOpen: false, inSession: false, stagingCamera: true));
        }

        [Test]
        public void Back_StillDoesNothingAtAnIdlePicker()
        {
            // And nothing else moved: with no staging in flight the picker is the same dead end for
            // back it was before (Unity 6 GameActivity does not quit on back).
            Assert.AreEqual(BackAction.Ignore,
                PauseState.BackFor(PausePhase.Running, false, false,
                    guideOpen: false, inSession: false, stagingCamera: false));
        }

        [Test]
        public void Back_ClosesTheGuideEvenOverCameraStaging()
        {
            // "how to play" is tappable during staging (it is outside SetButtonsInteractable), so
            // the guide can be the top-most screen while the camera is still coming up. It keeps
            // back; the next press cancels the staging.
            Assert.AreEqual(BackAction.CloseGuide,
                PauseState.BackFor(PausePhase.Running, false, false,
                    guideOpen: true, inSession: false, stagingCamera: true));
        }

        [Test]
        public void Back_TheStagingFlagCannotChangeAnythingInsideASession()
        {
            // The pause menu stages the camera too, and that wait is already cancelled by the
            // Resuming row (back -> Pause -> PauseMenu.CancelResume). The new flag must therefore
            // be inert whenever a run is on screen, or there would be two answers for one state.
            foreach (PausePhase phase in System.Enum.GetValues(typeof(PausePhase)))
                foreach (bool runLive in new[] { true, false })
                    foreach (bool overlayOpen in new[] { true, false })
                        Assert.AreEqual(
                            PauseState.BackFor(phase, runLive, overlayOpen, false, true, false),
                            PauseState.BackFor(phase, runLive, overlayOpen, false, true, true),
                            "the staging flag changed back's meaning in phase " + phase);
        }

        [Test]
        public void Back_NeverLeavesALiveRun()
        {
            // The one rule that must hold in every phase: while there is a run going, back can
            // only ever stop it on screen. Leaving is a deliberate choice from the pause menu.
            foreach (PausePhase phase in System.Enum.GetValues(typeof(PausePhase)))
            {
                Assert.AreNotEqual(BackAction.QuitToMenu, PauseState.BackFor(phase, true, false),
                    "back must not abandon a live run in phase " + phase);
                Assert.AreNotEqual(BackAction.QuitToMenu, PauseState.BackFor(phase, true, true),
                    "back must not abandon a live run in phase " + phase);
            }
        }
    }
}
