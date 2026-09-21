using System;
using System.Collections.Generic;

namespace MotionRunner.Growth
{
    public interface IAnalyticsService
    {
        bool Enabled { get; }
        bool IsReady { get; }
        string SupportId { get; }
        event Action Changed;
        void SetEnabled(bool enabled);
        void Track(string name, Dictionary<string, object> properties);
    }

    public sealed class FakeAnalyticsService : IAnalyticsService
    {
        public bool Enabled { get; private set; }
        public bool IsReady => Enabled;
        public string SupportId => string.Empty;
        public event Action Changed;
        public readonly List<KeyValuePair<string, Dictionary<string, object>>> Events =
            new List<KeyValuePair<string, Dictionary<string, object>>>();

        public void SetEnabled(bool enabled) { Enabled = enabled; Changed?.Invoke(); }
        public void Track(string name, Dictionary<string, object> properties)
        {
            if (Enabled) Events.Add(new KeyValuePair<string, Dictionary<string, object>>(
                name, new Dictionary<string, object>(properties)));
        }
    }
}
