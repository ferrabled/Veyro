using System;
using System.Text;

namespace MotionRunner.Track
{
    /// What a challenge link carries once it has been parsed back out of a URL: the exact run to
    /// re-generate, and the score to beat. Engine-free, like everything else in this assembly, so
    /// the round trip (build -> URL -> parse) is a unit test rather than a device test.
    public readonly struct ChallengeLink
    {
        /// The track to play. The whole point of the link: seed + content version + world id is
        /// the run (handoff 3.3), so the receiver plays the sender's track and not a lookalike.
        public readonly RunSeed Seed;

        /// The sender's score - what the receiver is being asked to beat.
        public readonly int Score;

        /// Whether the sender played it as that day's Daily Run. Carried for the wording only:
        /// the receiver always plays a challenge as a FREE run, because overriding today's daily
        /// seed would let a link rewrite the one track everybody is supposed to share (T-008).
        public readonly bool WasDaily;

        /// "2026-09-21" - the UTC day the sender's run belonged to, or empty.
        public readonly string DailyLabel;

        public ChallengeLink(RunSeed seed, int score, bool wasDaily, string dailyLabel)
        {
            Seed = seed;
            Score = score;
            WasDaily = wasDaily;
            DailyLabel = dailyLabel ?? string.Empty;
        }

        public override string ToString() =>
            "challenge " + Seed + " score=" + Score + (WasDaily ? " daily=" + DailyLabel : " free");
    }

    /// The "challenge a friend" payload (T-024): one line of text, one link, and the parser that
    /// turns the link back into a run.
    ///
    /// Deliberately in the Track assembly (noEngineReferences) rather than next to the share
    /// sheet: the text and the URL are pure string arithmetic over a RunSummary, and keeping them
    /// engine-free is what makes the round trip testable in EditMode without a phone, a share
    /// sheet, or a browser.
    ///
    /// The URL is short on purpose - it is pasted into chat apps that truncate, and every
    /// character after the first ~60 is a character somebody's messenger may hide:
    ///   https://veyro.ferrabled.com/challenge/?s=1234&amp;v=1&amp;w=greybox&amp;p=4210&amp;m=free
    /// Keys are single letters for the same reason. `d` is omitted entirely for free runs.
    public static class ChallengeMessage
    {
        /// Query keys. Single letters, fixed forever: a link in somebody's chat history has to
        /// keep working after the app updates.
        public const string KeySeed = "s";
        public const string KeyContentVersion = "v";
        public const string KeyWorldId = "w";
        public const string KeyScore = "p";
        public const string KeyMode = "m";
        public const string KeyDailyLabel = "d";

        public const string ModeDaily = "daily";
        public const string ModeFree = "free";

        /// The path segment both link shapes share - the https page and the custom scheme. Used
        /// by TryParse to refuse URLs that are not challenges (a deep link into the app for some
        /// future feature must not start a run).
        public const string PathMarker = "challenge";

        /// Builds the whole share text: one playful line plus the link.
        public static string Build(in RunSummary summary, string baseUrl) =>
            BuildText(summary, BuildUrl(summary, baseUrl));

        /// Same, with the link already built - so a caller that needs the URL separately (to log
        /// it, or to put it on a button) cannot end up shipping a different one in the text.
        public static string BuildText(in RunSummary summary, string url)
        {
            var text = new StringBuilder(160);

            // The record flag leads: it is the reason this particular run is worth sending.
            if (summary.IsNewRecord) text.Append("\U0001F3C6 new personal best! ");

            text.Append("I scored ").Append(FormatScore(summary.Score)).Append(" in Veyro Run");
            if (summary.IsDaily) text.Append(" on today's Daily Run");
            if (summary.Distance > 0) text.Append(" (").Append(FormatScore(summary.Distance)).Append(" m)");
            text.Append(" — can you beat me? ").Append(url);
            return text.ToString();
        }

        /// The challenge URL. `baseUrl` is the page that receives it - the https landing page in
        /// the share text, and the custom scheme when the page hands the link back to the app.
        public static string BuildUrl(in RunSummary summary, string baseUrl)
        {
            string root = (baseUrl ?? string.Empty).TrimEnd('?', '&');
            var url = new StringBuilder(root.Length + 72);
            url.Append(root);
            url.Append(root.IndexOf('?') >= 0 ? '&' : '?');

            Append(url, KeySeed, summary.Seed.Seed.ToString(Invariant));
            Append(url, KeyContentVersion, summary.Seed.ContentVersion);
            Append(url, KeyWorldId, summary.Seed.WorldId);
            Append(url, KeyScore, summary.Score.ToString(Invariant));
            Append(url, KeyMode, summary.IsDaily ? ModeDaily : ModeFree);

            // Free runs belong to no day, so the key is left out rather than sent empty.
            if (summary.IsDaily && !string.IsNullOrEmpty(summary.DailyLabel))
                Append(url, KeyDailyLabel, summary.DailyLabel);

            return url.ToString();
        }

        /// Turns a link - `veyro://challenge?...` or `https://veyro.ferrabled.com/challenge/?...` -
        /// back into the run it describes. Returns false for anything that is not a challenge
        /// link or has no usable seed, and never throws: this runs on whatever string the OS
        /// hands the app at boot.
        public static bool TryParse(string url, out ChallengeLink link)
        {
            link = default;
            if (string.IsNullOrEmpty(url)) return false;

            int query = url.IndexOf('?');
            if (query < 0) return false;

            // Only the two shapes the manifest registers: `veyro://challenge` and
            // `https://veyro.ferrabled.com/challenge`. The parser and the intent-filters must
            // agree on what a challenge link is.
            string path = url.Substring(0, query);
            if (!IsChallengePath(path)) return false;

            bool hasSeed = false;
            int seed = 0;
            int score = 0;
            // A missing `v` means this build's content, like a missing `w` means the shipped
            // world: the sender's version is unknown rather than different, and RunFlow refuses
            // a version it cannot generate - so defaulting to "" would drop every truncated link.
            string contentVersion = ChunkLibrary.ContentVersion;
            string worldId = RunSeed.DefaultWorldId;
            string dailyLabel = string.Empty;
            bool daily = false;

            // Hand-rolled rather than a Uri/HttpUtility parse: Uri rejects unknown schemes on
            // some runtimes, and HttpUtility is not in the engine-free reference set.
            string rest = url.Substring(query + 1);
            int at = 0;
            while (at < rest.Length)
            {
                int amp = rest.IndexOf('&', at);
                if (amp < 0) amp = rest.Length;
                int eq = rest.IndexOf('=', at);
                if (eq >= 0 && eq < amp)
                {
                    string key = rest.Substring(at, eq - at);
                    string value = Unescape(rest.Substring(eq + 1, amp - eq - 1));
                    switch (key)
                    {
                        case KeySeed: hasSeed = TryParseInt(value, out seed); break;
                        // Bounded: both end up in a RunSeed and in a board submission, and a
                        // link is attacker-typed text.
                        // An oversized one is present but no version any build ships: kept
                        // unmatchable ("") so it is refused, not mistaken for an absent one.
                        case KeyContentVersion:
                            if (value.Length > 0) contentVersion = value.Length <= MaxTokenLength ? value : string.Empty;
                            break;
                        case KeyWorldId: if (value.Length > 0 && value.Length <= MaxTokenLength) worldId = value; break;
                        case KeyScore: TryParseInt(value, out score); break;
                        case KeyMode: daily = string.Equals(value, ModeDaily, StringComparison.OrdinalIgnoreCase); break;
                        case KeyDailyLabel: dailyLabel = value; break;
                    }
                }
                at = amp + 1;
            }

            if (!hasSeed) return false;
            link = new ChallengeLink(new RunSeed(seed, contentVersion, worldId),
                score < 0 ? 0 : score, daily, dailyLabel);
            return true;
        }

        /// The https host a challenge link may live on. Must match the App Link intent-filter
        /// (Editor/AndroidChallengeLinks) and GameLinks.ChallengeBaseUrl.
        public const string ChallengeHost = "veyro.ferrabled.com";
        public const string ChallengeScheme = "veyro";

        /// Longest `v` / `w` value accepted from a link.
        public const int MaxTokenLength = 32;

        static bool IsChallengePath(string path)
        {
            int sep = path.IndexOf("://", StringComparison.Ordinal);
            if (sep < 0) return false;
            string scheme = path.Substring(0, sep);
            string rest = path.Substring(sep + 3);
            if (string.Equals(scheme, ChallengeScheme, StringComparison.OrdinalIgnoreCase))
                return rest.Equals("challenge", StringComparison.OrdinalIgnoreCase) ||
                       rest.StartsWith("challenge/", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)) return false;
            string prefix = ChallengeHost + "/challenge";
            if (!rest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            // "/challenge" or "/challenge/", not "/challenger": the manifest's pathPrefix is
            // looser, the parser is the one that decides what actually starts a run.
            return rest.Length == prefix.Length || rest[prefix.Length] == '/';
        }

        /// "4210" -> "4 210". Grouped with a plain space rather than a locale separator: the text
        /// goes into somebody else's chat app, where a comma reads as a decimal point in half of
        /// Europe and a thin space does not survive every font.
        public static string FormatScore(int value)
        {
            bool negative = value < 0;
            string digits = (negative ? -(long)value : value).ToString(Invariant);
            if (digits.Length <= 3) return negative ? "-" + digits : digits;

            var grouped = new StringBuilder(digits.Length + digits.Length / 3 + 1);
            if (negative) grouped.Append('-');
            int lead = digits.Length % 3;
            if (lead == 0) lead = 3;
            grouped.Append(digits, 0, lead);
            for (int i = lead; i < digits.Length; i += 3) grouped.Append(' ').Append(digits, i, 3);
            return grouped.ToString();
        }

        static System.Globalization.CultureInfo Invariant =>
            System.Globalization.CultureInfo.InvariantCulture;

        static void Append(StringBuilder url, string key, string value)
        {
            if (url[url.Length - 1] != '?' && url[url.Length - 1] != '&') url.Append('&');
            url.Append(key).Append('=').Append(Escape(value));
        }

        /// Percent-encoding for query values. `Uri.EscapeDataString` escapes everything a query
        /// value may not contain (including `&`, `=`, `#` and spaces) and leaves the unreserved
        /// set alone, so a plain worldId like "greybox" survives unchanged.
        static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : Uri.EscapeDataString(value);

        /// The inverse. `+` is decoded as a space because some chat apps and form encoders rewrite
        /// it that way; a literal `+` in one of our own values arrives as %2B, so nothing we emit
        /// can be damaged by it.
        static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try { return Uri.UnescapeDataString(value.Replace("+", " ")); }
            catch (Exception) { return value; }
        }

        static bool TryParseInt(string value, out int parsed) =>
            int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign, Invariant, out parsed);
    }
}
