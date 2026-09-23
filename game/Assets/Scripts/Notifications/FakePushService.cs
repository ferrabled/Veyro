using System;
using System.Threading.Tasks;

namespace MotionRunner.Notifications
{
    /// Editor/unsupported-platform fallback never pretends to have registered with OneSignal.
    public sealed class FakePushService : IPushService
    {
        public PushStatus Status { get; private set; } = new PushStatus();
        public event Action Changed;
        public event Action<NotificationOpen> Opened;
        public void Open(NotificationOpen notification) => Opened?.Invoke(notification);
        public void Initialize(bool enabled) { }
        public Task<bool> EnableFromUserTapAsync() => Task.FromResult(false);
        public void Disable() { }

        public void SetStatus(PushStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }
    }
}
