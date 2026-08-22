namespace MotionRunner.Track
{
    public enum RunMode
    {
        /// Everyone on the same UTC date runs the same track (handoff 6.1). The default: it is the
        /// mode the retention loop, the OneSignal campaigns and the challenge links are built on.
        Daily = 0,

        /// A different track every run. For playtesting, and for a free-play mode later.
        Free = 1
    }
}
