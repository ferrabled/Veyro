namespace MotionRunner.Core
{
    /// External URLs the game exposes. Play policy requires the privacy policy to be reachable
    /// from inside the app, not only on the store listing, and the same URL goes into the Play
    /// Console "App content → Privacy policy" field — this constant and the console field must
    /// move together. The page's source is docs/PRIVACY_POLICY.md, served via GitHub Pages
    /// (hosting choice: OPEN_QUESTIONS 9; update here if the host changes).
    public static class GameLinks
    {
        public const string PrivacyPolicyUrl = "https://ferrabled.github.io/Veyro/privacy/";
    }
}
