namespace MotionRunner.Pose
{
    /// "I'm ready" — the player's RIGHT hand held above their head, which is how a paused camera
    /// run gets resumed without anyone reaching for the phone (docs/CAMERA_TUNING.md §Resume
    /// gesture carries the dials and the mirror rule).
    ///
    /// Engine-free and clock-free like the rest of this assembly, for the same reason FaceSteering
    /// is: a gesture whose false positives cost something has to be measurable headlessly against
    /// synthetic landmarks, not eyeballed by waving at a phone. Here the cost is specific — a false
    /// confirm starts the 3-2-1 countdown and drops a player who was not ready back into a live run
    /// — so every gate below is written to refuse when it is unsure. The opposite failure is cheap:
    /// a missed confirm costs one more probe sample, and the probe is already running.
    ///
    /// This only ever accelerates. RESUME by touch and Android back keep working at every step (the
    /// spec's hard constraint, and rule 3), so a player whose pose never detects loses nothing.
    ///
    /// Coordinates are PoseLandmark's: normalized [0,1] over the upright frame, origin top-left,
    /// **y down**. "Above the head" is therefore a SMALLER y, and every comparison here reads
    /// backwards from the way it sounds.
    ///
    /// ---- head units, not frame fractions ------------------------------------------------------
    ///
    /// headUnit = shoulderMidY - noseY, the vertical span from the nose down to the shoulder line.
    /// That span is about one head height on anybody — roughly 20-25 cm, whatever the build — so it
    /// is the body's own ruler, delivered by the same frame the rule is judging.
    ///
    /// Measuring the margin in those units is the trick FaceSteering pulls with face widths, and it
    /// is here for the same reason: a fraction of the frame is not a distance. The upright frame
    /// spans roughly a metre of world per metre of distance, so a margin fixed at, say, 0.06 of the
    /// frame height would be a lazy head-high raise with the phone at arm's length and an
    /// impossible one from two metres back — and camera mode is meant to be played from two metres
    /// back, phone propped on a table.
    ///
    /// ---- the mirror trap: read WristFor before touching the labels -----------------------------
    ///
    /// The rule is stated in PLAYER terms ("their right hand"), and the model does not speak them.
    /// See WristFor: the mapping looks inverted and is not.
    public static class RaisedHand
    {
        /// How far above the nose the wrist has to be, in head units.
        ///
        /// Half a head unit is ~10-12 cm above the nose, which is about the crown of the skull: the
        /// wrist must clear the top of the head, not merely rise past the face. The margin IS the
        /// rule. The failure it exists to stop is not noise — it is a confirm from a gesture nobody
        /// made: pushing hair back, a hand on the back of the neck, scratching an ear, a wave. All
        /// of those park a wrist at or just above nose level, and with no margin every one of them
        /// resumes the run. A wrist at ear level must NOT count.
        ///
        /// A hand actually held up sits 1.5 to 2 head units above the nose — the forearm alone is
        /// longer than the head — so the gesture people will really make clears this three or four
        /// times over. There is no tuning tension here: the band between "unmistakably overhead"
        /// and "somewhere near the face" is enormous, and 0.5 sits in the middle of it.
        public const float HeadUnitsAboveNose = 0.5f;

        /// Below this the geometry is not a standing person, and the rule refuses to answer.
        ///
        /// headUnit is a SCALE, so a small one magnifies: at 0.01 the margin is 0.005 of the frame
        /// height — a few pixels — and "above the head" has quietly collapsed into "above the
        /// nose", which a hand resting on a hip satisfies the moment the player leans forward. The
        /// ways to get there are all real: someone lying down or bent over the phone (nose and
        /// shoulders level), the landmarker placing the shoulders ABOVE the nose, which is a
        /// NEGATIVE unit and would flip the comparison outright, and a frame where the torso has
        /// been extrapolated out of almost nothing.
        ///
        /// 0.01 of the frame height is an order of magnitude under any real standing pose: at the
        /// far end of the playable range a nose-to-shoulder span still covers ~0.065 of the frame
        /// height, and handheld it is ~0.25. So this is a guard, not a dial — nothing legitimate
        /// comes anywhere near it.
        public const float MinHeadUnit = 0.01f;

        /// Which landmark label carries the PLAYER'S RIGHT HAND, given whether the frame handed to
        /// the landmarker was selfie-mirrored. The mapping is inverted on purpose.
        ///
        /// Two conventions collide here, and each is right on its own:
        ///   * the upright frame has already been mirrored ONCE upstream, by
        ///     CameraFeed.MirrorForSelfie (FrameOrientation.ForCamera's mirrorHorizontally), so the
        ///     player sees themselves as in a mirror and the whole pipeline downstream works in the
        ///     player's own left/right — the contract FaceOverlay documents at its header;
        ///   * BlazePose labels landmarks ANATOMICALLY, deciding "left wrist" from which side of the
        ///     body it appears on in an image it assumes was never mirrored.
        /// Feed the mirrored image to that model and the player's right hand appears on the image's
        /// RIGHT, where an unmirrored world would put the subject's anatomical LEFT hand. So the
        /// model calls it LeftWrist, and asking it for RightWrist gets the player's other hand.
        ///
        /// This is the same double-mirror bug class FaceOverlay's header is about (flip once more
        /// and it looks correct in isolation while being backwards against the game), with one
        /// difference that makes it worse: it fails SILENTLY. The player raises the hand the screen
        /// asked for and nothing happens — no wrong movement, no clue. Hence a test that pins the
        /// mapping in both directions rather than a playtest.
        public static PoseJoint WristFor(bool selfieMirrored) =>
            selfieMirrored ? PoseJoint.LeftWrist : PoseJoint.RightWrist;

        /// The same mapping for the same arm's elbow — the fallback witness when the hand itself
        /// has left the frame (see IsRaised).
        public static PoseJoint ElbowFor(bool selfieMirrored) =>
            selfieMirrored ? PoseJoint.LeftElbow : PoseJoint.RightElbow;

        /// One sample of the geometric rule: is the player's right hand unmistakably overhead in
        /// this frame? No memory, no clock — RaisedHandConfirm does the holding.
        ///
        /// Two witnesses, either is enough, and the split comes from the device (2026-09-02): a
        /// hand held properly overhead parks the wrist at the very top of the frame — often past
        /// it — which is exactly where the landmarker's crop ends. There the wrist's PRESENCE
        /// collapses while its visibility holds, and a raise the player was faithfully holding
        /// flickered in and out of the rule.
        ///   * The WRIST is gated on visibility alone — the spec's original bar. Its coordinates
        ///     near the crop edge are extrapolated but still roughly right, and the overhead
        ///     margin plus RaisedHandConfirm's streak carry the false-positive discipline.
        ///   * The ELBOW, strictly above the nose, vouches for a hand the model lost outright:
        ///     an elbow overhead means the upper arm is vertical, which no near-face fidget (hair,
        ///     ear, wave) produces. Elbows live mid-frame, so this one keeps the full IsTracked
        ///     gate — a low-presence elbow really is a guess.
        public static bool IsRaised(PoseFrame frame, bool selfieMirrored)
        {
            if (!frame.HasPose) return false;

            PoseLandmark nose = frame[PoseJoint.Nose];
            if (!nose.IsTracked) return false;

            // Both shoulders tracked, and the pose current — TryShoulderCenter is the one place
            // that midpoint is defined, so this rule cannot drift from the rest of the geometry.
            if (!PoseGeometry.TryShoulderCenter(frame, out PosePoint shoulders)) return false;

            float headUnit = shoulders.Y - nose.Y;
            if (headUnit <= MinHeadUnit) return false;

            // y is down: strictly ABOVE the nose by the margin.
            PoseLandmark wrist = frame[WristFor(selfieMirrored)];
            if (wrist.Visibility >= PoseLandmark.TrackedThreshold &&
                wrist.Y < nose.Y - HeadUnitsAboveNose * headUnit)
                return true;

            PoseLandmark elbow = frame[ElbowFor(selfieMirrored)];
            return elbow.IsTracked && elbow.Y < nose.Y;
        }
    }

    /// The temporal half of the confirm: how many agreeing looks a raised hand is worth before the
    /// countdown starts.
    ///
    /// SAMPLES, not seconds, and the difference is deliberate. The probe self-paces — it runs one
    /// BlazePose cycle after another for as long as the paused menu is up, ~334 ms of detector plus
    /// landmarker on the shipping phone, so ~3 Hz — which makes a sample the actual unit of
    /// evidence. Three of them is three independent inferences that agreed, and agreement across
    /// inferences is the flicker discipline the rule needs. A wall clock would measure the phone
    /// instead of the gesture: on a slower device a one-second window holds two inferences, and a
    /// confirm resting on two looks is one bad frame away from being an accident. Counting samples
    /// makes a slower phone ask the player to hold a little longer, which is the harmless
    /// direction — and this assembly has no clock anyway.
    ///
    /// Confirmed latches until Reset(), because the player drops their arm the instant the
    /// countdown appears on screen. Un-confirming on the next sample would cancel the very thing
    /// the gesture just started.
    public sealed class RaisedHandConfirm
    {
        /// Three consecutive samples, ~1 s at the probe's ~3 Hz — the hold the spec asks for.
        ///
        /// One sample would fire on any single frame where the landmarker guessed a wrist well.
        /// Two is one coincidence away from that. Three is about a second of the player's time:
        /// short enough that holding it does not feel like an exam, long enough that nothing
        /// transient survives it.
        public const int RequiredSamples = 3;

        /// Net raised evidence ending with the sample just submitted. Exposed so the staging card
        /// can show the hold filling up — a gesture with no feedback is indistinguishable from a
        /// broken one. It keeps telling the truth after Confirmed latches, so a dropped arm winds
        /// it back down while the confirm stands.
        public int Streak { get; private set; }

        /// True once RequiredSamples consecutive raised samples have arrived. Latches; only Reset()
        /// clears it.
        public bool Confirmed { get; private set; }

        /// One probe sample. Returns Confirmed as it stands after this sample, so the caller can
        /// act on the return value alone.
        public bool Submit(bool raised)
        {
            // A miss pays back ONE sample instead of zeroing the streak. The hard reset read
            // better on paper ("consecutive means consecutive") and was wrong on the device
            // (2026-09-02): the lite landmarker drops the overhead wrist for a single sample
            // routinely — blur, the crop edge — and every drop sent a faithfully-held raise back
            // to "raise your right hand", the prompt flip-flopping against an arm that never
            // moved. Net evidence keeps the discipline that mattered: progress needs more hits
            // than misses, so alternating noise oscillates at 0-1 and never confirms, while a
            // one-sample dip in a real hold costs one sample, not the whole hold.
            Streak = raised ? Streak + 1 : (Streak > 0 ? Streak - 1 : 0);
            if (Streak >= RequiredSamples) Confirmed = true;
            return Confirmed;
        }

        /// Back to waiting — every time the probe starts, and after a confirm has been consumed.
        public void Reset()
        {
            Streak = 0;
            Confirmed = false;
        }
    }
}
