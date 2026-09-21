using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexWeeklyTray.Tests
{
    internal static class SmokeTests
    {
        private static int failures;

        private static int Main(string[] args)
        {
            try
            {
                RunOfflineTests();
                if (Array.IndexOf(args, "--live") >= 0)
                {
                    RunLiveTest().GetAwaiter().GetResult();
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }

            if (failures > 0)
            {
                Console.Error.WriteLine("FAILED: " + failures + " assertion(s)");
                return 1;
            }

            Console.WriteLine("PASS: all requested tests succeeded");
            return 0;
        }

        private static void RunOfflineTests()
        {
            const string sampleJson = @"{
                ""rateLimits"": {
                    ""primary"": { ""usedPercent"": 80, ""windowDurationMins"": 300, ""resetsAt"": 1700000000 }
                },
                ""rateLimitsByLimitId"": {
                    ""codex"": {
                        ""primary"": { ""usedPercent"": 80, ""windowDurationMins"": 300, ""resetsAt"": 1700000000 },
                        ""secondary"": { ""usedPercent"": 24, ""windowDurationMins"": 10080, ""resetsAt"": 1800000000 }
                    }
                }
            }";

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary<string, object> sample = serializer.DeserializeObject(sampleJson) as IDictionary<string, object>;
            UsageWindowSet windows = UsageParser.Parse(sample);
            UsageSnapshot snapshot = windows.Weekly;

            AssertTrue(windows.FiveHour != null, "finds the 5-hour window");
            AssertTrue(windows.Weekly != null, "finds the weekly window");
            AssertEqual(300, windows.FiveHour.WindowDurationMinutes, "keeps the 5-hour duration");
            AssertEqual(20.0, windows.FiveHour.RemainingPercent, "calculates 5-hour remaining percent");
            AssertEqual(10080, snapshot.WindowDurationMinutes, "keeps the weekly duration");
            AssertEqual(76.0, snapshot.RemainingPercent, "calculates remaining percent");

            const string fiveHourOnlyJson = @"{
                ""rateLimits"": {
                    ""primary"": { ""usedPercent"": 65, ""windowDurationMins"": 300, ""resetsAt"": 1700000000 }
                }
            }";
            IDictionary<string, object> fiveHourOnly = serializer.DeserializeObject(fiveHourOnlyJson) as IDictionary<string, object>;
            UsageWindowSet fiveHourOnlyWindows = UsageParser.Parse(fiveHourOnly);
            AssertTrue(fiveHourOnlyWindows.FiveHour != null, "supports a 5-hour-only account");
            AssertTrue(fiveHourOnlyWindows.Weekly == null, "does not invent a weekly window");
            AssertEqual(35.0, fiveHourOnlyWindows.FiveHour.RemainingPercent, "calculates 5-hour-only remaining percent");

            UsageSnapshot full = new UsageSnapshot(0.0, 10080, 0);
            AssertEqual(100.0, full.RemainingPercent, "keeps the true 100 percent value");

            UsageSnapshot empty = new UsageSnapshot(100.0, 10080, 0);
            AssertEqual(0.0, empty.RemainingPercent, "keeps the true zero percent value");

            Color weeklyBorder;
            using (Icon icon = TrayIconRenderer.RenderWeekly(snapshot))
            {
                AssertEqual(64, icon.Width, "creates a Windows icon");
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    weeklyBorder = bitmap.GetPixel(32, 6);
                    Color removedClockwise = bitmap.GetPixel(45, 19);
                    Color remainingAfterCut = bitmap.GetPixel(45, 45);
                    AssertTrue(
                        weeklyBorder.B > weeklyBorder.R + 100,
                        "uses a blue weekly outline");
                    AssertTrue(
                        Math.Abs(removedClockwise.R - removedClockwise.G) < 20,
                        "removes usage clockwise from 12 o'clock");
                    AssertTrue(
                        remainingAfterCut.G > remainingAfterCut.R + 40,
                        "keeps the remaining sector colored");
                }
            }

            using (Icon icon = TrayIconRenderer.RenderFiveHour(windows.FiveHour))
            using (Bitmap bitmap = icon.ToBitmap())
            {
                Color fiveHourBorder = bitmap.GetPixel(32, 6);
                AssertTrue(
                    fiveHourBorder.R > weeklyBorder.R + 50,
                    "uses a purple 5-hour outline");
            }

            using (Icon icon = TrayIconRenderer.RenderWeeklyUnavailable())
            {
                AssertEqual(64, icon.Height, "creates an unavailable icon");
            }
        }

        private static async Task RunLiveTest()
        {
            using (CodexAppServerClient client = new CodexAppServerClient(Console.WriteLine))
            {
                UsageWindowSet windows = await client.FetchUsageAsync();
                WriteLiveWindows("LIVE_OK", windows);
                AssertTrue(windows.HasAny, "live response contains a supported window");

                UsageWindowSet refreshed = await client.FetchUsageAsync();
                WriteLiveWindows("LIVE_REFRESH_OK", refreshed);
                AssertTrue(refreshed.HasAny, "persistent connection supports refresh");
            }
        }

        private static void WriteLiveWindows(string prefix, UsageWindowSet windows)
        {
            if (windows.FiveHour != null)
            {
                Console.WriteLine(prefix + " 5-hour remaining=" + windows.FiveHour.RemainingPercent.ToString("0.##"));
            }
            if (windows.Weekly != null)
            {
                Console.WriteLine(prefix + " weekly remaining=" + windows.Weekly.RemainingPercent.ToString("0.##"));
            }
        }

        private static void AssertEqual(object expected, object actual, string name)
        {
            if (!Object.Equals(expected, actual))
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": expected=" + expected + " actual=" + actual);
            }
            else
            {
                Console.WriteLine("OK   " + name);
            }
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name);
            }
            else
            {
                Console.WriteLine("OK   " + name);
            }
        }
    }
}
