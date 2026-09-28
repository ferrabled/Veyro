using MotionRunner.Audio;
using NUnit.Framework;

namespace MotionRunner.Tests
{
    /// The start-versus-resume contract of the two-slot music deck, without an AudioSource in
    /// sight. The scenario that motivated lifting it out of GameAudio is the third test: the run
    /// track parked under the result loop has to come back mid-bar on RUN AGAIN.
    public sealed class MusicDeckTests
    {
        const string Menu = "Audio/Music/menu_loop";
        const string Run = "Audio/Music/run_04";
        const string Lost = "Audio/Music/result_lost";
        const string Best = "Audio/Music/result_best";
        const float Fade = 0.8f;

        MusicDeck _deck;

        [SetUp]
        public void SetUp() => _deck = new MusicDeck();

        /// Plan + commit, the way GameAudio does it once the clip has loaded.
        void Play(string path)
        {
            switch (_deck.Plan(path, out int slot))
            {
                case MusicDeck.Action.Resume: _deck.Resume(slot); break;
                case MusicDeck.Action.Start: _deck.Start(slot, path); break;
            }
        }

        void Settle() => _deck.Tick(10f, Fade);

        [Test]
        public void TheFirstTrackStartsOnTheFreeSlotFromSilence()
        {
            Assert.AreEqual(MusicDeck.Action.Start, _deck.Plan(Menu, out int slot));
            Assert.AreNotEqual(_deck.Active, slot, "a new track never lands on the active slot");

            _deck.Start(slot, Menu);
            Assert.AreEqual(slot, _deck.Active);
            Assert.AreEqual(Menu, _deck.ActivePath);
            Assert.AreEqual(0f, _deck.LevelOf(slot), "fades up from nothing");
            Assert.AreEqual(1f, _deck.TargetOf(slot));
        }

        [Test]
        public void AskingForTheActiveTrackResumesIt()
        {
            Play(Menu);
            Settle();
            Assert.AreEqual(MusicDeck.Action.Resume, _deck.Plan(Menu, out int slot));
            Assert.AreEqual(_deck.Active, slot);
        }

        [Test]
        public void TheRunTrackParkedUnderTheResultLoopResumesOnRunAgain()
        {
            // Crash: the run track is faded and HELD. Then the result loop starts on the other
            // slot. RUN AGAIN asks for the run track again - it must be a Resume of the parked
            // slot, not a Start from the top, even though it is no longer the active path.
            Play(Run);
            int runSlot = _deck.Active;
            Settle();

            Assert.IsTrue(_deck.FadeOut());
            Settle();
            Assert.IsTrue(_deck.IsParked(runSlot), "faded to silence and meant to stay there");
            Assert.AreEqual(Run, _deck.PathOf(runSlot), "parked, not forgotten");

            Assert.AreEqual(MusicDeck.Action.Start, _deck.Plan(Lost, out int lostSlot));
            Assert.AreEqual(1 - runSlot, lostSlot, "the result loop takes the other slot");
            _deck.Start(lostSlot, Lost);
            Settle();

            Assert.AreEqual(MusicDeck.Action.Resume, _deck.Plan(Run, out int again));
            Assert.AreEqual(runSlot, again, "the OLD rule compared against the active path and would have restarted here");
            _deck.Resume(again);

            Assert.AreEqual(runSlot, _deck.Active);
            Assert.AreEqual(1f, _deck.TargetOf(runSlot));
            Assert.AreEqual(0f, _deck.TargetOf(lostSlot), "and the result loop fades out under it");
            Assert.AreEqual(0f, _deck.LevelOf(runSlot), "crossfading back in from the silence it was parked at");
        }

        [Test]
        public void ANewTrackReplacesTheParkedSlotAndNeverTheActiveOne()
        {
            Play(Menu);
            Play(Run);
            int runSlot = _deck.Active;
            int menuSlot = 1 - runSlot;
            Assert.AreEqual(Menu, _deck.PathOf(menuSlot));

            _deck.FadeOut();
            Play(Lost);
            Assert.AreEqual(Lost, _deck.PathOf(menuSlot), "the menu loop, parked longer, is what gets replaced");
            Assert.AreEqual(Run, _deck.PathOf(runSlot), "the just-crashed run track survives for RUN AGAIN");

            // A second crash in a row with a different verdict: result_best replaces result_lost,
            // and the run track is still where RUN AGAIN will look for it.
            Play(Run);
            _deck.FadeOut();
            Play(Best);
            Assert.AreEqual(Best, _deck.PathOf(menuSlot));
            Assert.AreEqual(Run, _deck.PathOf(runSlot));
            Assert.AreEqual(MusicDeck.Action.Resume, _deck.Plan(Run, out _));
        }

        [Test]
        public void FadeOutParksTheActiveSlotAndKeepsItResumable()
        {
            Play(Run);
            Settle();
            Assert.IsTrue(_deck.FadeOut());
            Assert.AreEqual(0f, _deck.TargetOf(_deck.Active));
            Assert.IsFalse(_deck.IsParked(_deck.Active), "still audible: the fade has not run yet");

            Settle();
            Assert.IsTrue(_deck.IsParked(_deck.Active));
            Assert.AreEqual(MusicDeck.Action.Resume, _deck.Plan(Run, out _));
        }

        [Test]
        public void FadeOutWithNothingLoadedIsANoOp()
        {
            Assert.IsFalse(_deck.FadeOut());
            Assert.IsNull(_deck.ActivePath);
        }

        [Test]
        public void AnEmptyPathIsNothingToPlay()
        {
            Assert.AreEqual(MusicDeck.Action.None, _deck.Plan(null, out _));
            Assert.AreEqual(MusicDeck.Action.None, _deck.Plan(string.Empty, out _));
        }

        [Test]
        public void AMissingClipLeavesTheParkedTrackResumable()
        {
            // GameAudio plans a Start, fails to load the clip, and fades out instead of
            // committing. The deck must not have been touched by the plan alone.
            Play(Run);
            int runSlot = _deck.Active;
            _deck.FadeOut();
            Settle();

            Assert.AreEqual(MusicDeck.Action.Start, _deck.Plan("Audio/Music/does_not_exist", out int slot));
            Assert.IsNull(_deck.PathOf(slot), "a plan is not a commit");
            Assert.AreEqual(Run, _deck.ActivePath);
            _deck.FadeOut();

            Assert.AreEqual(MusicDeck.Action.Resume, _deck.Plan(Run, out int again));
            Assert.AreEqual(runSlot, again);
        }

        [Test]
        public void TickCrossfadesOverTheFadeSecondsAndStaysInRange()
        {
            Play(Menu);
            int menuSlot = _deck.Active;
            _deck.Tick(0.4f, Fade);
            Assert.AreEqual(0.5f, _deck.LevelOf(menuSlot), 1e-4f);

            Play(Run);
            int runSlot = _deck.Active;
            _deck.Tick(0.4f, Fade);
            Assert.AreEqual(0f, _deck.LevelOf(menuSlot), 1e-4f, "the outgoing slot reaches silence");
            Assert.AreEqual(0.5f, _deck.LevelOf(runSlot), 1e-4f, "while the incoming one is half way up");

            _deck.Tick(5f, Fade);
            Assert.AreEqual(1f, _deck.LevelOf(runSlot), "clamped at full");
            Assert.AreEqual(0f, _deck.LevelOf(menuSlot), "clamped at silence");
            Assert.IsTrue(_deck.IsParked(menuSlot));
            Assert.IsFalse(_deck.IsParked(runSlot));
        }
    }
}
