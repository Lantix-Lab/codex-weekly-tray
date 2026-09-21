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
            UsageSnapshot snapshot = UsageParser.Parse(sample);

            AssertEqual(10080, snapshot.WindowDurationMinutes, "selects the weekly window");
            AssertEqual(76.0, snapshot.RemainingPercent, "calculates remaining percent");
            UsageSnapshot full = new UsageSnapshot(0.0, 10080, 0);
            AssertEqual(100.0, full.RemainingPercent, "keeps the true 100 percent value");

            UsageSnapshot empty = new UsageSnapshot(100.0, 10080, 0);
            AssertEqual(0.0, empty.RemainingPercent, "keeps the true zero percent value");

            using (Icon icon = TrayIconRenderer.Render(snapshot))
            {
                AssertEqual(64, icon.Width, "creates a Windows icon");
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    Color removedClockwise = bitmap.GetPixel(45, 19);
                    Color remainingAfterCut = bitmap.GetPixel(45, 45);
                    AssertTrue(
                        Math.Abs(removedClockwise.R - removedClockwise.G) < 20,
                        "removes usage clockwise from 12 o'clock");
                    AssertTrue(
                        remainingAfterCut.G > remainingAfterCut.R + 40,
                        "keeps the remaining sector colored");
                }
            }

            using (Icon icon = TrayIconRenderer.RenderUnavailable())
            {
                AssertEqual(64, icon.Height, "creates an unavailable icon");
            }
        }

        private static async Task RunLiveTest()
        {
            using (CodexAppServerClient client = new CodexAppServerClient(Console.WriteLine))
            {
                UsageSnapshot snapshot = await client.FetchWeeklyUsageAsync();
                Console.WriteLine(
                    "LIVE_OK remaining=" + snapshot.RemainingPercent.ToString("0.##") +
                    " window=" + snapshot.WindowDurationMinutes);
                AssertEqual(10080, snapshot.WindowDurationMinutes, "live response contains a weekly window");

                UsageSnapshot refreshed = await client.FetchWeeklyUsageAsync();
                Console.WriteLine(
                    "LIVE_REFRESH_OK remaining=" + refreshed.RemainingPercent.ToString("0.##"));
                AssertEqual(10080, refreshed.WindowDurationMinutes, "persistent connection supports refresh");
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
