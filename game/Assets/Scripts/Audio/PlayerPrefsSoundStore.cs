using UnityEngine;

namespace MotionRunner.Audio
{
    /// PlayerPrefs behind the ISoundSettingsStore seam. Too thin to test; the keys, clamps and
    /// the perceptual curve are all in SoundSettings, which is tested against an in-memory
    /// store instead.
    public sealed class PlayerPrefsSoundStore : ISoundSettingsStore
    {
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
