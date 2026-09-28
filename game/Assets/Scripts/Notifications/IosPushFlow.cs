// The iOS player and every editor, never the Android player: EditMode pins this from either
// build target while the Android build stays byte-identical (IOS_HANDOFF §3).
#if UNITY_IOS || UNITY_EDITOR
namespace MotionRunner.Notifications
{
    /// What an ENABLE tap does on iOS.
    public enum IosPushStep { AlreadyAllowed, Ask, OpenSettings }

    /// The iOS notification flow: the tap decision OneSignalPushService takes and the words
    /// NotificationPanel shows. Engine-free, so EditMode pins it from either build target.
    /// Android keeps its own path and copy in those two files, unchanged.
    ///
    /// iOS shows its notification prompt once. OneSignal makes that request itself on iOS (on
    /// Android, Unity does), so OneSignal's CanRequestPermission is exact here: false once the
    /// prompt has been answered, and still false when notifications were turned off in Settings
    /// later. With no permission and no prompt left, only Settings can allow them, so the tap
    /// goes there. A first "Don't Allow" never jumps straight to Settings: the panel switches to
    /// ALLOW IN SETTINGS and the player decides.
    ///
    /// App Review 2.3.10: nothing in this copy may name another platform.
    public static class IosPushFlow
    {
        public const string EnableButton = "ENABLE NOTIFICATIONS";
        public const string SettingsButton = "ALLOW IN SETTINGS";
        public const string OnButton = "NOTIFICATIONS ON";

        public const string Unavailable =
            "Notifications are unavailable right now. You can keep playing; the full game works without them.";
        public const string Connecting =
            "Connecting notifications… Keep playing and check again when you're online.";
        public const string On =
            "Notifications are on. You can turn them off here at any time. Turning them off does not delete your notification record.";
        public const string Blocked =
            "Notifications are turned off in your iPhone's Settings. Tap below to open Settings and allow them. The full game works without them.";
        public const string Finishing =
            "Permission is allowed; device registration is still finishing. Check your connection and try again later.";
        public const string Ask =
            "Enable notifications about Daily Runs. You can turn them off at any time. Your iPhone will ask for permission if needed; declining changes nothing about the game.";

        /// From OneSignal's Notifications.Permission and CanRequestPermission, read at tap time.
        public static IosPushStep ForTap(bool permission, bool canRequestPermission) =>
            permission ? IosPushStep.AlreadyAllowed
            : canRequestPermission ? IosPushStep.Ask
            : IosPushStep.OpenSettings;

        /// The denied state: registered, not allowed, and iOS will not ask again.
        public static bool NeedsSettings(PushStatus status) =>
            status.Initialized && status.Registered &&
            ForTap(status.PermissionGranted, status.CanRequestPermission) == IosPushStep.OpenSettings;

        public static string Body(PushStatus status)
        {
            if (!status.Initialized) return Unavailable;
            if (!status.Registered) return Connecting;
            if (status.ReadyForPush) return On;
            if (NeedsSettings(status)) return Blocked;
            if (status.PermissionGranted && status.OptedIn && !status.HasToken) return Finishing;
            return Ask;
        }

        public static string Button(PushStatus status) =>
            status.ReadyForPush ? OnButton : NeedsSettings(status) ? SettingsButton : EnableButton;
    }
}
#endif
