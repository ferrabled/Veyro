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
    }
}
