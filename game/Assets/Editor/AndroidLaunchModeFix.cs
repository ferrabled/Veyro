using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;

namespace MotionRunner.EditorTools
{
    /// Unity's generated manifest gives UnityPlayerGameActivity launchMode="singleTask".
    /// RevenueCat/Play Billing requires "standard" or "singleTop": with singleTask, a purchase
    /// is cancelled when the user is bounced out to a banking/3DS app mid-payment and comes
    /// back (REVENUECAT_PLAN §2.2 - found as launchMode=2 in the merged manifest, 26 Aug).
    /// singleTop keeps the single-activity behaviour a game wants; only the billing-hostile
    /// task clearing goes away. Patched post-generate so no committed AndroidManifest.xml has
    /// to shadow Unity's template (the gotcha-11 debris class).
    public sealed class AndroidLaunchModeFix : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            XNamespace android = "http://schemas.android.com/apk/res/android";
            var doc = XDocument.Load(manifestPath);
            bool changed = false;

            foreach (var activity in doc.Descendants("activity"))
            {
                var mode = activity.Attribute(android + "launchMode");
                if (mode == null || mode.Value != "singleTask") continue;
                mode.Value = "singleTop";
                changed = true;
            }

            if (changed) doc.Save(manifestPath);
        }
    }
}
