namespace MotionRunner.Audio
{
    /// Every one-shot the game can make. The enum is the contract between call sites and the
    /// asset folder: AudioCatalog maps each entry to a Resources path, and AudioCatalogTests
    /// fails on any entry without a path or without a clip behind it - so a renamed .ogg is a
    /// red test rather than a silent button on the phone.
    public enum Sfx
    {
        UiTap,
        UiBack,
        UiConfirm,
        UiDeny,
        Coin,
        Jump,
        Slide,
        LaneSwoosh,
        Crash,
        ResultJingle,
        NewBest,
        CountdownTick,
        CountdownGo,
        Streak
    }
}
