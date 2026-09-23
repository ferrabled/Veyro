using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MotionRunner.Notifications
{
    /// Native SDK types never cross this boundary. Registration is not permission or delivery.
    public interface IPushService
    {
        PushStatus Status { get; }
        event Action Changed;
        event Action<NotificationOpen> Opened;
        void Initialize(bool enabled);
        Task<bool> EnableFromUserTapAsync();
        void Disable();
    }

    public sealed class NotificationOpen
    {
        public readonly string Id;
        public readonly IDictionary<string, object> Data;
        public NotificationOpen(string id, IDictionary<string, object> data)
        {
            Id = id;
            Data = data == null ? new Dictionary<string, object>() : new Dictionary<string, object>(data);
        }
    }

    public sealed class PushStatus
    {
        public readonly bool Initialized;
        public readonly string SubscriptionId;
        public readonly bool PermissionGranted;
        public readonly bool OptedIn;
        public readonly bool HasToken;
        public readonly bool CanRequestPermission;

        public PushStatus(bool initialized = false, string subscriptionId = null,
            bool permissionGranted = false, bool optedIn = false, bool hasToken = false,
            bool canRequestPermission = false)
        {
            Initialized = initialized;
            SubscriptionId = subscriptionId ?? string.Empty;
            PermissionGranted = permissionGranted;
            OptedIn = optedIn;
            HasToken = hasToken;
            CanRequestPermission = canRequestPermission;
        }

        public bool Registered => IsServerId(SubscriptionId);
        public bool ReadyForPush => Initialized && Registered && PermissionGranted && OptedIn && HasToken;

        public static bool IsServerId(string id) =>
            !string.IsNullOrWhiteSpace(id) && !id.StartsWith("local-", StringComparison.OrdinalIgnoreCase);
    }
}
