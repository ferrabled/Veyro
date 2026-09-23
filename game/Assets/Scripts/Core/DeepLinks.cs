using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Core
{
    /// The receiving half of "challenge a friend" (T-024): a `veyro://challenge?…` or
    /// `https://veyro.ferrabled.com/challenge/?…` link tapped anywhere on the phone arrives here,
    /// is parsed, and is parked in PendingChallenge until RunFlow can act on it.
    ///
    /// Two sources, because Android has two cases and Unity exposes them differently:
    ///   * the app was NOT running - the link is already in Application.absoluteURL when the
    ///     first frame runs, and no event is ever raised for it;
    ///   * the app WAS running - Application.deepLinkActivated fires with the new URL (the
    ///     launcher activity is singleTop, see AndroidLaunchModeFix, so the running instance is
    ///     reused rather than relaunched).
    /// Missing either one is the classic "works from cold, does nothing warm" deep-link bug.
    ///
    /// Nothing here starts a run: a link can arrive mid-run, and the only safe moment to play it
    /// is at the menu. RunFlow owns that.
    public static class DeepLinks
    {
        static bool _listening;

        /// Called once from GameBootstrap, before anything shows a screen.
        public static void Begin()
        {
            if (!_listening)
            {
                Application.deepLinkActivated += OnDeepLinkActivated;
                _listening = true;
            }

            // Cold start. Empty for an ordinary launch, so this costs a null check.
            Handle(Application.absoluteURL);
        }

        /// Application.deepLinkActivated is an Action&lt;string&gt;, so the event needs a void
        /// handler; Handle's bool is for callers that want to know.
        static void OnDeepLinkActivated(string url) => Handle(url);

        /// Returns true when the URL was a challenge and is now pending. Public so the parse can
        /// be exercised from the Editor without an intent.
        public static bool Handle(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!ChallengeMessage.TryParse(url, out var link))
            {
                Debug.Log("[DeepLink] ignored (not a challenge): " + url);
                return false;
            }

            Debug.Log("[DeepLink] " + link);
            PendingChallenge.Set(link);
            return true;
        }
    }
}
