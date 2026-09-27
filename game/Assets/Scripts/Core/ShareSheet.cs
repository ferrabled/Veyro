using System;
using UnityEngine;

namespace MotionRunner.Core
{
    /// The platform share sheet (T-024): hands a line of text to whatever the player wants to
    /// send it with, and never to anything we choose for them.
    ///
    /// Android's ACTION_SEND chooser is the whole implementation - no SDK, no permission, and no
    /// list of messaging apps to keep up to date. **Nothing is transmitted by the game**: the
    /// text goes to the app the user picks, in their own hands, which is also why this adds no
    /// Data-safety obligation (docs/STORE_COMPLIANCE.md, T-024 section).
    ///
    /// Everything here is wrapped: a share that fails is a log line and a copied string, never an
    /// exception thrown through a UI button's onClick.
    public static class ShareSheet
    {
        /// The chooser's own title, and the mail subject for share targets that have one (mail,
        /// notes). SMS/chat targets ignore EXTRA_SUBJECT entirely.
        public const string DefaultSubject = "Veyro Run challenge";

        /// Returns true when the system share sheet was actually opened; false when the text was
        /// copied to the clipboard instead (Editor, desktop, or a failed intent).
        public static bool Send(string text, string subject = DefaultSubject)
        {
            if (string.IsNullOrEmpty(text)) return false;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Unity 6 runs GameActivity (com.unity3d.player.UnityPlayerGameActivity), but the
                // static accessor did not move: com.unity3d.player.UnityPlayer.currentActivity is
                // still how a plugin reaches the activity, and it is what every startActivity
                // call in the Unity Android docs uses.
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    if (activity == null) { CopyToClipboard(text); return false; }

                    // Each setter returns the intent itself; the returned Java reference is
                    // disposed straight away so the JNI local-reference table cannot fill up.
                    intent.Call<AndroidJavaObject>("setAction",
                        intentClass.GetStatic<string>("ACTION_SEND")).Dispose();
                    intent.Call<AndroidJavaObject>("setType", "text/plain").Dispose();
                    intent.Call<AndroidJavaObject>("putExtra",
                        intentClass.GetStatic<string>("EXTRA_TEXT"), text).Dispose();
                    if (!string.IsNullOrEmpty(subject))
                        intent.Call<AndroidJavaObject>("putExtra",
                            intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject).Dispose();

                    // createChooser, not the bare intent: it guarantees a picker even when the
                    // player has previously set a default send app, which is what "challenge a
                    // friend" wants - a different friend may be on a different app.
                    using (var chooser = intentClass.CallStatic<AndroidJavaObject>(
                               "createChooser", intent, subject))
                        activity.Call("startActivity", chooser);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Share] share sheet failed, copied instead: " + e.Message);
                CopyToClipboard(text);
                return false;
            }
#else
            // Editor and every non-Android platform: the clipboard is the honest equivalent, and
            // it makes the text checkable in Play mode without a phone.
            CopyToClipboard(text);
            return false;
#endif
        }

        static void CopyToClipboard(string text)
        {
            try { GUIUtility.systemCopyBuffer = text; }
            catch (Exception e) { Debug.LogWarning("[Share] clipboard unavailable: " + e.Message); }
            Debug.Log("[Share] copied to clipboard: " + text);
        }
    }
}
