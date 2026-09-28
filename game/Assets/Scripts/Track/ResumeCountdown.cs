using System;

namespace MotionRunner.Track
{
    /// The 3-2-1 between "the player confirmed they are ready" and the world moving again.
    ///
    /// ---- why a countdown exists at all ---------------------------------------------------------
    ///
    /// A camera resume cannot drop straight into gameplay. FaceSteering calibrates on whatever it
    /// sees after a Reset(), so a calibration started as the player walks back into shot lands them
    /// in a moving run aimed at whichever lane their mid-stride body happened to read as. RunFlow
    /// therefore resets the run's steering again the moment this countdown begins
    /// (PauseMenu.CountdownStarted). The countdown buys the three things that fix that
    /// at once: the recentred axis gets a still player to calibrate against, the framing overlay
    /// stays up long enough for the player to watch themselves settle into the middle lane, and the
    /// tap or gesture that asked to resume is spent frames before the run reads input again.
    ///
    /// ---- why it is its own engine-free class ---------------------------------------------------
    ///
    /// UI-free by design, like PauseState and CameraOutage. PauseMenu draws the numerals (RuntimeUi,
    /// code-built — rule 1) and RunFlow.ApplyPhase remains the ONLY writer of Time.timeScale; this
    /// class just counts, and answers "is the countdown over" exactly once so the caller can turn
    /// that edge into a single FinishResume. Firing twice would unfreeze a run that is already
    /// running; never firing strands a player in a frozen game with a "1" on screen. Neither is a
    /// failure a playtest reliably reproduces, and both are arithmetic over a clock — so they are
    /// pinned in EditMode instead.
    public sealed class ResumeCountdown
    {
        /// Seconds from confirm to gameplay. Three, as spec'd: it is long enough to show three
        /// distinct numerals (a two-second countdown reading "2, 1" looks like a glitch), and it is
        /// the same beat every game the player already knows uses, so nothing has to be taught. The
        /// cost is three seconds added to every recovery, which is why it is a constant with a
        /// parameter override rather than a number typed into the menu — the owner may yet want it
        /// shorter, or want tilt resumes to skip it (CAMERA_TUNING.md, open owner decisions).
        public const float DefaultDurationSeconds = 3f;

        /// The most one frame may contribute — the same 0.1 s clamp CameraOutage and FaceSteering
        /// put on their own dt, for the same reason and here with more at stake: this counts in
        /// UNSCALED time (the game is frozen, so scaled time is not moving at all), and the single
        /// most likely enormous delta in the whole app is the frame Android hands back after the
        /// player has been away in another app — which is precisely the frame a paused run resumes
        /// on. Without the clamp that one delta swallows the entire countdown and the player is
        /// dropped into a moving run with no warning at all. Clamped, a hitch merely stretches the
        /// countdown a little, which is the harmless direction.
        public const float MaxStepSeconds = 0.1f;

        /// What Begin() clamps a non-positive duration to. Not zero: a zero-length countdown would
        /// have to complete inside Begin (no tick to fire the edge on) or never complete at all, and
        /// both make the caller's "wait for the true" contract a special case. A duration this small
        /// is spent by the first real frame, so "instant" means "done next tick" — one code path.
        const float SmallestDurationSeconds = 1e-4f;

        /// True between Begin and either completion or Cancel. False the frame after it completes:
        /// the countdown is over, and something else owns the screen now.
        public bool IsRunning { get; private set; }

        /// Seconds left. Zero when nothing is counting.
        public float RemainingSeconds { get; private set; }

        /// The numeral to draw: 3, 2, 1 while running, 0 when there is nothing to draw.
        ///
        /// Ceiling, not truncation, so each digit owns a whole second — "3" for the first second,
        /// "2" for the second, "1" for the last — instead of "3" flashing for a single frame and
        /// "0" holding for a second at the end. The floor at 1 covers the last frame, where a
        /// remainder rounded to exactly zero would otherwise print a "0" nobody asked for.
        public int DisplayDigit
        {
            get
            {
                if (!IsRunning) return 0;
                int digit = (int)Math.Ceiling(RemainingSeconds);
                return digit < 1 ? 1 : digit;
            }
        }

        /// Starts counting. Called on confirm — the two hops OR the RESUME button, which must stay
        /// equivalent (rule 3: a player whose hops never register resumes by button).
        ///
        /// Restarts from the top if one is already in flight, which is what a second confirm means:
        /// there is no queue of countdowns, only the current one.
        public void Begin(float seconds = DefaultDurationSeconds)
        {
            RemainingSeconds = seconds > SmallestDurationSeconds ? seconds : SmallestDurationSeconds;
            IsRunning = true;
        }

        /// One frame. Returns true EXACTLY once, on the frame the countdown reaches zero, and false
        /// for ever after until the next Begin — the caller turns that single true into the single
        /// FinishResume that unfreezes the run.
        ///
        /// unscaledDeltaTime, because Time.timeScale is 0 for the entire countdown and the scaled
        /// clock is standing still. A non-positive delta contributes nothing: the first frame after
        /// a pause can report a garbage or zero delta, and time must not run backwards into a
        /// countdown that then never ends.
        public bool Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return false;

            if (unscaledDeltaTime > 0f)
                RemainingSeconds -= unscaledDeltaTime < MaxStepSeconds
                    ? unscaledDeltaTime
                    : MaxStepSeconds;

            if (RemainingSeconds > 0f) return false;

            RemainingSeconds = 0f;
            IsRunning = false;
            return true;
        }

        /// The countdown was abandoned — back, the face lost again, or QUIT — and no completion may
        /// fire: those paths land back on the paused menu, and a stray true here would unfreeze the
        /// run underneath it. Idempotent, because more than one of those can arrive in a frame
        /// (back during a face loss) and because RunFlow calls it defensively whenever it leaves
        /// the resuming phase.
        public void Cancel()
        {
            IsRunning = false;
            RemainingSeconds = 0f;
        }
    }
}
