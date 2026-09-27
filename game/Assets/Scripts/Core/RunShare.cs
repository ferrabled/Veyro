using System;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Core
{
    /// The result card's SHARE button (T-024): builds the challenge text for a finished run and
    /// hands it to the platform share sheet. The card only depends on this one entry point.
    ///
    /// The text and the link are built in MotionRunner.Track (engine-free, unit-tested); this is
    /// the two-line seam between them and the phone. It cannot throw: it is called straight from
    /// a UGUI onClick, and a share that fails must cost the player nothing but a log line.
    public static class RunShare
    {
        public static void Share(in RunSummary summary)
        {
            try
            {
                string text = ChallengeMessage.Build(summary, GameLinks.ChallengeBaseUrl);
                Debug.Log("[Share] " + text);
                ShareSheet.Send(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] could not build the challenge: " + e.Message);
            }
        }
    }
}
