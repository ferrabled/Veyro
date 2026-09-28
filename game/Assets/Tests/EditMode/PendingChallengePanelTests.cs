using System;
using System.Reflection;
using System.Text.RegularExpressions;
using MotionRunner.Track;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MotionRunner.Tests
{
    // Runtime UI lives in Assembly-CSharp; reflection follows the existing menu tests.
    public sealed class PendingChallengePanelTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static Type Runtime(string name) =>
            Type.GetType("MotionRunner." + name + ", Assembly-CSharp", true);

        [TestCase("_notificationPanel", "NotificationPanel")]
        [TestCase("_analyticsPanel", "AnalyticsPanel")]
        public void ChallengeWaitsForTheOpenPanelBeforeItIsProcessed(string field, string panelName)
        {
            bool hadPending = PendingChallenge.Has;
            var previous = PendingChallenge.Peek;
            float previousTimeScale = Time.timeScale;
            var root = new GameObject("Pending challenge panel test");
            root.SetActive(false);
            var flowType = Runtime("Gameplay.RunFlow");
            var flow = root.AddComponent(flowType);
            var panelField = flowType.GetField(field, Private);
            try
            {
                var menuObject = new GameObject("Menu");
                menuObject.transform.SetParent(root.transform);
                var menu = menuObject.AddComponent(Runtime("Menu.MainMenu"));
                flowType.GetField("_menu", Private).SetValue(flow, menu);

                var panelObject = new GameObject(panelName);
                panelObject.transform.SetParent(root.transform);
                panelField.SetValue(flow, panelObject.AddComponent(Runtime("Menu." + panelName)));

                // A foreign version follows the normal refusal path once unblocked, without
                // creating a run, sensors or services in this isolated EditMode fixture.
                var link = new ChallengeLink(new RunSeed(123, "panel-test-foreign-version", "greybox"),
                    456, false, string.Empty);
                PendingChallenge.Set(link);
                var process = flowType.GetMethod("TryStartPendingChallenge", Private);

                process.Invoke(flow, null);
                process.Invoke(flow, null);
                Assert.IsTrue(PendingChallenge.Has, "The open panel must retain the incoming link.");
                Assert.AreEqual(link, PendingChallenge.Peek);
                Assert.IsTrue(menuObject.activeSelf, "The menu must not be dismissed under the panel.");

                panelField.SetValue(flow, null);
                UnityEngine.Object.DestroyImmediate(panelObject);
                LogAssert.Expect(LogType.Warning, new Regex(@"\[DeepLink\] challenge refused, built for another version:"));
                process.Invoke(flow, null);
                Assert.IsFalse(PendingChallenge.Has, "Closing the panel must allow the link to be processed.");
                process.Invoke(flow, null); // A consumed link is not processed a second time.
            }
            finally
            {
                panelField.SetValue(flow, null);
                UnityEngine.Object.DestroyImmediate(root);
                Time.timeScale = previousTimeScale;
                if (hadPending) PendingChallenge.Set(previous);
                else PendingChallenge.Clear();
            }
        }
    }
}
