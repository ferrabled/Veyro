using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Core
{
    /// PlayerPrefs behind the IScoreStore seam. The seam exists for exactly one reason: the
    /// per-scheme keying and the legacy migration in BestBoard are wire format on players'
    /// devices, and wire format gets unit tests (EditMode, in-memory store) rather than a manual
    /// check on a phone. This adapter is deliberately too thin to be worth testing.
    ///
    /// Save() is not part of the seam: RunSession batches its writes and calls PlayerPrefs.Save()
    /// once per crash, exactly as it did before the split.
    public sealed class PlayerPrefsScoreStore : IScoreStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
    }
}
