using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    /// RunHud builds legacy UGUI Text, which silently renders nothing when its Font is null.
    /// This asserts the built-in font RunHud reaches for first actually resolves, so the
    /// failure mode is a red test here rather than an invisible HUD on the phone.
    ///
    /// It cannot cover RunHud itself: RunHud lives in the predefined Assembly-CSharp, which an
    /// assembly-definition test assembly is not allowed to reference. On-device verification
    /// still owns "the HUD is legible" (see STATUS.md).
    public sealed class HudFontTests
    {
        [Test]
        public void TheBuiltInRuntimeFontResolves()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(font, "LegacyRuntime.ttf is gone - RunHud.HudFont's fallback chain is now load-bearing");
        }
    }
}
