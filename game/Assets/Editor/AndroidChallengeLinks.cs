using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace MotionRunner.EditorTools
{
    /// Registers the two ways a challenge link (T-024) can reach the game, by patching the
    /// generated Android manifest - the same technique, and the same reason, as
    /// AndroidLaunchModeFix: no committed AndroidManifest.xml may shadow Unity's template
    /// (CLAUDE.md gotcha 11 - the last one that did switched the launcher activity and added a
    /// permission nobody asked for).
    ///
    /// Two filters, deliberately separate:
    ///   * `veyro://challenge…` - a custom scheme. Works with zero server-side setup, which is
    ///     why the web landing page tries it FIRST. Nothing verifies it, so any app could claim
    ///     it; it carries no secrets, only a seed and a score;
    ///   * `https://veyro.ferrabled.com/challenge…` with `android:autoVerify="true"` - the real
    ///     Android App Link. It only takes effect once
    ///     https://veyro.ferrabled.com/.well-known/assetlinks.json serves the RELEASE signing
    ///     certificate's SHA-256 (owner-only - OPEN_QUESTIONS 24). Until then Android simply
    ///     fails verification and the link opens in the browser, landing on the page that offers
    ///     the custom scheme - i.e. the unverified state is the shipped state and it works.
    ///
    /// Adds no permission: an intent-filter is a declaration of what the app can open, not a
    /// capability grant (docs/STORE_COMPLIANCE.md, T-024).
    public sealed class AndroidChallengeLinks : IPostGenerateGradleAndroidProject
    {
        /// After AndroidLaunchModeFix (100): both patch the same file, and running last means
        /// this one sees the launchMode the billing fix settled on.
        public int callbackOrder => 110;

        public const string Scheme = "veyro";
        public const string Host = "veyro.ferrabled.com";
        public const string SchemeHost = "challenge";
        public const string HttpsPathPrefix = "/challenge";

        /// Unity 6 ships GameActivity; this is the launcher class the manifest names
        /// (CLAUDE.md gotcha 5). Matching on it rather than on "every activity" keeps the filters
        /// off anything a package might add later.
        public const string LauncherActivity = "com.unity3d.player.UnityPlayerGameActivity";

        static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            var doc = XDocument.Load(manifestPath);
            var activity = doc.Descendants("activity").FirstOrDefault(a =>
                (string)a.Attribute(Android + "name") == LauncherActivity);

            // Fall back to whichever activity carries the LAUNCHER category, so a Unity version
            // that renames the class does not silently ship a game with no deep links.
            activity ??= doc.Descendants("activity").FirstOrDefault(a =>
                a.Elements("intent-filter").Elements("category")
                    .Any(c => (string)c.Attribute(Android + "name") == "android.intent.category.LAUNCHER"));

            if (activity == null) return;

            bool changed = false;
            changed |= AddFilter(activity, false);
            changed |= AddFilter(activity, true);

            if (changed) doc.Save(manifestPath);
        }

        /// One VIEW/BROWSABLE filter. Idempotent: a manifest that already has it (a re-run over a
        /// staged Gradle project) is left alone rather than growing a duplicate.
        static bool AddFilter(XElement activity, bool https)
        {
            var data = new XElement("data",
                new XAttribute(Android + "scheme", https ? "https" : Scheme));

            if (https)
            {
                data.Add(new XAttribute(Android + "host", Host));
                data.Add(new XAttribute(Android + "pathPrefix", HttpsPathPrefix));
            }
            else
            {
                data.Add(new XAttribute(Android + "host", SchemeHost));
            }

            if (Has(activity, data)) return false;

            var filter = new XElement("intent-filter",
                new XElement("action", new XAttribute(Android + "name", "android.intent.action.VIEW")),
                new XElement("category", new XAttribute(Android + "name", "android.intent.category.DEFAULT")),
                new XElement("category", new XAttribute(Android + "name", "android.intent.category.BROWSABLE")),
                data);

            // Only the https filter is verifiable; autoVerify on a custom scheme is meaningless
            // and makes Play's link checker complain.
            if (https) filter.Add(new XAttribute(Android + "autoVerify", "true"));

            activity.Add(filter);
            return true;
        }

        static bool Has(XElement activity, XElement data) =>
            activity.Elements("intent-filter").Elements("data").Any(existing =>
                (string)existing.Attribute(Android + "scheme") == (string)data.Attribute(Android + "scheme") &&
                (string)existing.Attribute(Android + "host") == (string)data.Attribute(Android + "host"));
    }
}
