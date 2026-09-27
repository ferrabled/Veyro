namespace MotionRunner.Core
{
    /// External URLs the game exposes. Play policy requires the privacy policy to be reachable
    /// from inside the app, not only on the store listing, and the same URL goes into the Play
    /// Console "App content → Privacy policy" field — this constant and the console field must
    /// move together. The page's source is docs/PRIVACY_POLICY.md, served from site/ on
    /// Cloudflare Workers static assets
    public static class GameLinks
    {
        public const string PrivacyPolicyUrl = "https://veyro.ferrabled.com/privacy/";

        /// Where a shared challenge lands (T-024). An https page rather than the custom scheme,
        /// because the text is pasted into chat apps: a `veyro://` link is dead for everybody who
        /// does not have the game yet, and this page can offer them the store instead. The page
        /// itself hands the same query string back to the app as `veyro://challenge?…` — see
        /// site/public/challenge/index.html and AndroidChallengeLinks.cs.
        ///
        /// The trailing slash is load-bearing: the site is served as static assets and
        /// /challenge (no slash) redirects, which some chat previews do not follow.
        public const string ChallengeBaseUrl = "https://veyro.ferrabled.com/challenge/";

        /// The app's own deep link. Registered by AndroidChallengeLinks; works with no domain
        /// verification at all, which is why it is the fallback the web page tries first.
        public const string ChallengeSchemeUrl = "veyro://challenge";

        /// The Play listing. Used by the web page, and kept here so the package name has one
        /// spelling in the repo.
        public const string PlayStoreUrl =
            "https://play.google.com/store/apps/details?id=com.ferrabled.veyro.run";
    }
}
