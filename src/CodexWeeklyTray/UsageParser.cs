using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodexWeeklyTray
{
    internal static class UsageParser
    {
        private const int WeeklyWindowMinutes = 7 * 24 * 60;

        public static UsageSnapshot Parse(IDictionary<string, object> result)
        {
            if (result == null)
            {
                throw new InvalidOperationException("App Server returned no rate-limit data.");
            }

            List<IDictionary<string, object>> windows = new List<IDictionary<string, object>>();

            IDictionary<string, object> byLimitId = GetDictionary(result, "rateLimitsByLimitId");
            if (byLimitId != null)
            {
                IDictionary<string, object> codexBucket = GetDictionary(byLimitId, "codex");
                AddWindows(codexBucket, windows);
            }

            if (windows.Count == 0)
            {
                AddWindows(GetDictionary(result, "rateLimits"), windows);
            }

            if (windows.Count == 0)
            {
                throw new InvalidOperationException("The account has no readable Codex rate-limit window.");
            }

            IDictionary<string, object> selected = null;
            int selectedDuration = -1;
            int index;
            for (index = 0; index < windows.Count; index++)
            {
                int duration = ToInt(GetValue(windows[index], "windowDurationMins"));
                if (duration == WeeklyWindowMinutes)
                {
                    selected = windows[index];
                    selectedDuration = duration;
                    break;
                }

                if (duration > selectedDuration)
                {
                    selected = windows[index];
                    selectedDuration = duration;
                }
            }

            if (selected == null || selectedDuration <= 0)
            {
                throw new InvalidOperationException("The rate-limit response has no valid window duration.");
            }

            double usedPercent = ToDouble(GetValue(selected, "usedPercent"));
            long resetsAt = ToLong(GetValue(selected, "resetsAt"));
            return new UsageSnapshot(usedPercent, selectedDuration, resetsAt);
        }

        private static void AddWindows(IDictionary<string, object> bucket, IList<IDictionary<string, object>> windows)
        {
            if (bucket == null)
            {
                return;
            }

            IDictionary<string, object> primary = GetDictionary(bucket, "primary");
            IDictionary<string, object> secondary = GetDictionary(bucket, "secondary");
            if (primary != null)
            {
                windows.Add(primary);
            }
            if (secondary != null)
            {
                windows.Add(secondary);
            }
        }

        private static object GetValue(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return dictionary != null && dictionary.TryGetValue(key, out value) ? value : null;
        }

        private static IDictionary<string, object> GetDictionary(IDictionary<string, object> dictionary, string key)
        {
            return GetValue(dictionary, key) as IDictionary<string, object>;
        }

        private static int ToInt(object value)
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static long ToLong(object value)
        {
            if (value == null)
            {
                return 0L;
            }
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }
}
