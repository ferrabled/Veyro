using MotionRunner.Track;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The share payload (T-024). Everything here is string arithmetic over a RunSummary, which
    /// is why it lives in the engine-free Track assembly: the round trip a challenge depends on
    /// (build a link -> somebody taps it -> parse it back into the same run) is provable without
    /// a phone, a share sheet or a browser.
    public sealed class ChallengeMessageTests
    {
        const string BaseUrl = "https://veyro.ferrabled.com/challenge/";

        static RunSummary Free(int score = 4210, int distance = 1240, bool record = false) =>
            new RunSummary(RunMode.Free, ControlScheme.Tilt, score, 12, 5, distance,
                6000, 0, string.Empty, record, false, CrashKind.WallSlam,
                new RunSeed(987654, "1", RunSeed.DefaultWorldId));

        static RunSummary Daily(int score = 4210, bool record = false) =>
            new RunSummary(RunMode.Daily, ControlScheme.Camera, score, 12, 5, 1240,
                6000, 3000, "2026-09-21", false, record, CrashKind.Trip,
                new RunSeed(-42, "1", RunSeed.DefaultWorldId));

        // ---- the message ----

        [Test]
        public void TheMessageCarriesTheScoreAndTheLink()
        {
            string message = ChallengeMessage.Build(Free(), BaseUrl);

            StringAssert.Contains("4 210", message);
            StringAssert.Contains(BaseUrl + "?", message);
            StringAssert.Contains("can you beat me?", message);
        }

        [Test]
        public void TheMessageIsOneLine()
        {
            Assert.That(ChallengeMessage.Build(Daily(record: true), BaseUrl),
                Does.Not.Contain("\n").And.Not.Contain("\r"));
        }

        [Test]
        public void ADailyRunSaysSoAndAFreeRunDoesNot()
        {
            StringAssert.Contains("today's Daily Run", ChallengeMessage.Build(Daily(), BaseUrl));
            StringAssert.DoesNotContain("Daily", ChallengeMessage.Build(Free(), BaseUrl));
        }

        [Test]
        public void ANewRecordIsAnnouncedAndOnlyThen()
        {
            StringAssert.Contains("new personal best", ChallengeMessage.Build(Free(record: true), BaseUrl));
            StringAssert.Contains("new personal best", ChallengeMessage.Build(Daily(record: true), BaseUrl));
            StringAssert.DoesNotContain("new personal best", ChallengeMessage.Build(Free(), BaseUrl));
        }

        [Test]
        public void ScoresAreGroupedInThrees()
        {
            Assert.AreEqual("0", ChallengeMessage.FormatScore(0));
            Assert.AreEqual("999", ChallengeMessage.FormatScore(999));
            Assert.AreEqual("1 000", ChallengeMessage.FormatScore(1000));
            Assert.AreEqual("12 345", ChallengeMessage.FormatScore(12345));
            Assert.AreEqual("1 234 567", ChallengeMessage.FormatScore(1234567));
        }

        // ---- the URL ----

        [Test]
        public void TheUrlCarriesEveryFieldTheReceiverNeeds()
        {
            string url = ChallengeMessage.BuildUrl(Free(), BaseUrl);

            StringAssert.StartsWith(BaseUrl + "?", url);
            StringAssert.Contains("s=987654", url);
            StringAssert.Contains("v=1", url);
            StringAssert.Contains("w=greybox", url);
            StringAssert.Contains("p=4210", url);
            StringAssert.Contains("m=free", url);

            // A free run belongs to no day, so the key is absent rather than empty.
            StringAssert.DoesNotContain("d=", url);
        }

        [Test]
        public void ADailyUrlCarriesTheDayAndTheMode()
        {
            string url = ChallengeMessage.BuildUrl(Daily(), BaseUrl);
            StringAssert.Contains("m=daily", url);
            StringAssert.Contains("d=2026-09-21", url);
            StringAssert.Contains("s=-42", url);
        }

        [Test]
        public void TheCustomSchemeGetsTheSameQuery()
        {
            var summary = Free();
            string https = ChallengeMessage.BuildUrl(summary, BaseUrl);
            string scheme = ChallengeMessage.BuildUrl(summary, "veyro://challenge");

            StringAssert.StartsWith("veyro://challenge?", scheme);
            Assert.AreEqual(https.Substring(https.IndexOf('?')), scheme.Substring(scheme.IndexOf('?')));
        }

        [Test]
        public void AwkwardWorldIdsAreEscapedAndSurviveTheTrip()
        {
            var summary = new RunSummary(RunMode.Free, ControlScheme.Tilt, 100, 0, 0, 10, 0, 0,
                string.Empty, false, false, CrashKind.Trip,
                new RunSeed(7, "2", "park & ride/v2?x=1"));

            string url = ChallengeMessage.BuildUrl(summary, BaseUrl);

            // Nothing that would end the value early, or start another parameter, may survive raw.
            StringAssert.DoesNotContain("park & ride", url);
            StringAssert.DoesNotContain("?x=1", url.Substring(url.IndexOf('?') + 1));
            StringAssert.Contains("w=park%20%26%20ride%2Fv2%3Fx%3D1", url);

            Assert.IsTrue(ChallengeMessage.TryParse(url, out var link));
            Assert.AreEqual("park & ride/v2?x=1", link.Seed.WorldId);
            Assert.AreEqual(7, link.Seed.Seed);
        }

        // ---- the round trip ----

        [Test]
        public void AFreeLinkRoundTripsToTheSameRun()
        {
            var summary = Free();
            Assert.IsTrue(ChallengeMessage.TryParse(ChallengeMessage.BuildUrl(summary, BaseUrl), out var link));

            Assert.AreEqual(summary.Seed.Seed, link.Seed.Seed);
            Assert.AreEqual(summary.Seed.ContentVersion, link.Seed.ContentVersion);
            Assert.AreEqual(summary.Seed.WorldId, link.Seed.WorldId);
            Assert.AreEqual(summary.Score, link.Score);
            Assert.IsFalse(link.WasDaily);
            Assert.AreEqual(string.Empty, link.DailyLabel);

            // The whole point: the receiver generates the identical track.
            Assert.AreEqual(summary.Seed.RngState(), link.Seed.RngState());
        }

        [Test]
        public void ADailyLinkRoundTripsAndKeepsItsDay()
        {
            var summary = Daily();
            Assert.IsTrue(ChallengeMessage.TryParse(ChallengeMessage.BuildUrl(summary, BaseUrl), out var link));

            Assert.AreEqual(-42, link.Seed.Seed);
            Assert.AreEqual(4210, link.Score);
            Assert.IsTrue(link.WasDaily);
            Assert.AreEqual("2026-09-21", link.DailyLabel);
            Assert.AreEqual(summary.Seed.RngState(), link.Seed.RngState());
        }

        [Test]
        public void TheCustomSchemeParsesTheSameWayTheWebLinkDoes()
        {
            Assert.IsTrue(ChallengeMessage.TryParse(
                "veyro://challenge?s=987654&v=1&w=greybox&p=4210&m=free", out var scheme));
            Assert.IsTrue(ChallengeMessage.TryParse(
                "https://veyro.ferrabled.com/challenge/?s=987654&v=1&w=greybox&p=4210&m=free", out var https));

            Assert.AreEqual(scheme.Seed.RngState(), https.Seed.RngState());
            Assert.AreEqual(scheme.Score, https.Score);
        }

        [Test]
        public void TheWholeShareTextCanBeParsedBackOutOfIt()
        {
            // What actually happens in a chat app: the link arrives surrounded by the sentence.
            var summary = Daily(record: true);
            string message = ChallengeMessage.Build(summary, BaseUrl);
            string url = message.Substring(message.IndexOf("https://", System.StringComparison.Ordinal));

            Assert.IsTrue(ChallengeMessage.TryParse(url, out var link));
            Assert.AreEqual(summary.Score, link.Score);
            Assert.AreEqual(summary.Seed.RngState(), link.Seed.RngState());
        }

        // ---- what must not parse ----

        [Test]
        public void NonChallengeLinksAreRefused()
        {
            Assert.IsFalse(ChallengeMessage.TryParse(null, out _));
            Assert.IsFalse(ChallengeMessage.TryParse(string.Empty, out _));
            Assert.IsFalse(ChallengeMessage.TryParse("https://veyro.ferrabled.com/privacy/", out _));
            Assert.IsFalse(ChallengeMessage.TryParse("veyro://shop?s=1", out _),
                "a future deep link must not start a run");
            Assert.IsFalse(ChallengeMessage.TryParse("veyro://challenge?p=99", out _),
                "no seed means no run to play");
            Assert.IsFalse(ChallengeMessage.TryParse("veyro://challenge?s=notanumber", out _));
        }

        [Test]
        public void OnlyTheRegisteredHostAndSchemeAreChallengeLinks()
        {
            Assert.IsFalse(ChallengeMessage.TryParse("https://evil.example/challenge?s=1", out _),
                "the parser must agree with the App Link intent-filter about the host");
            Assert.IsFalse(ChallengeMessage.TryParse("veyro://x/challenge?s=1", out _));
            Assert.IsFalse(ChallengeMessage.TryParse("https://veyro.ferrabled.com/challenger?s=1", out _));
            Assert.IsTrue(ChallengeMessage.TryParse("https://veyro.ferrabled.com/challenge/?s=1", out _));
            Assert.IsTrue(ChallengeMessage.TryParse("https://veyro.ferrabled.com/challenge?s=1", out _));
            Assert.IsTrue(ChallengeMessage.TryParse("VEYRO://challenge?s=1", out _));
        }

        [Test]
        public void OversizedVersionAndWorldTokensAreDropped()
        {
            string huge = new string('w', ChallengeMessage.MaxTokenLength + 1);
            Assert.IsTrue(ChallengeMessage.TryParse("veyro://challenge?s=1&v=" + huge + "&w=" + huge, out var link));
            Assert.AreEqual(string.Empty, link.Seed.ContentVersion);
            Assert.AreEqual(RunSeed.DefaultWorldId, link.Seed.WorldId);
        }

        [Test]
        public void AHalfBrokenLinkStillPlaysIfItHasASeed()
        {
            // Chat apps truncate and re-encode. As long as the seed survives, the run does.
            Assert.IsTrue(ChallengeMessage.TryParse("veyro://challenge?s=5&junk&=x&p=", out var link));
            Assert.AreEqual(5, link.Seed.Seed);
            Assert.AreEqual(0, link.Score);
            Assert.AreEqual(RunSeed.DefaultWorldId, link.Seed.WorldId,
                "a missing world id falls back to the shipped content set, not to empty");
            Assert.AreEqual(ChunkLibrary.ContentVersion, link.Seed.ContentVersion,
                "a missing content version is this build's - RunFlow refuses any other, so an " +
                "empty one would silently discard the link this test promises still plays");
            Assert.IsFalse(link.WasDaily);
        }

        [Test]
        public void AnEmptyVersionValueCountsAsAbsent()
        {
            Assert.IsTrue(ChallengeMessage.TryParse("veyro://challenge?s=5&v=&p=10", out var link));
            Assert.AreEqual(ChunkLibrary.ContentVersion, link.Seed.ContentVersion);
        }

        [Test]
        public void ANegativeScoreNeverComesBackOutOfALink()
        {
            Assert.IsTrue(ChallengeMessage.TryParse("veyro://challenge?s=5&p=-900", out var link));
            Assert.AreEqual(0, link.Score);
        }

        // ---- the pending slot ----

        [Test]
        public void APendingChallengeIsPlayedExactlyOnce()
        {
            PendingChallenge.Clear();
            Assert.IsFalse(PendingChallenge.Has);
            Assert.IsFalse(PendingChallenge.TryTake(out _));

            Assert.IsTrue(ChallengeMessage.TryParse("veyro://challenge?s=11&v=1&w=greybox&p=700&m=daily", out var link));
            PendingChallenge.Set(link);

            Assert.IsTrue(PendingChallenge.Has);
            Assert.AreEqual(700, PendingChallenge.Peek.Score);

            Assert.IsTrue(PendingChallenge.TryTake(out var taken));
            Assert.AreEqual(11, taken.Seed.Seed);
            Assert.IsFalse(PendingChallenge.Has, "a taken link must not restart itself at the next menu");
            Assert.IsFalse(PendingChallenge.TryTake(out _));
        }

        [Test]
        public void TheSecondLinkWins()
        {
            PendingChallenge.Clear();
            ChallengeMessage.TryParse("veyro://challenge?s=1", out var first);
            ChallengeMessage.TryParse("veyro://challenge?s=2", out var second);
            PendingChallenge.Set(first);
            PendingChallenge.Set(second);

            Assert.IsTrue(PendingChallenge.TryTake(out var taken));
            Assert.AreEqual(2, taken.Seed.Seed);
            PendingChallenge.Clear();
        }
    }
}
