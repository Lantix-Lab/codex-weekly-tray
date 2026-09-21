using System;

namespace CodexWeeklyTray
{
    internal sealed class UsageSnapshot
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public UsageSnapshot(double usedPercent, int windowDurationMinutes, long resetsAtUnix)
        {
            UsedPercent = Clamp(usedPercent, 0.0, 100.0);
            RemainingPercent = Clamp(100.0 - UsedPercent, 0.0, 100.0);
            WindowDurationMinutes = windowDurationMinutes;
            ResetsAtUnix = resetsAtUnix;
        }

        public double UsedPercent { get; private set; }

        public double RemainingPercent { get; private set; }

        public int WindowDurationMinutes { get; private set; }

        public long ResetsAtUnix { get; private set; }

        public DateTime ResetTimeLocal
        {
            get
            {
                if (ResetsAtUnix <= 0)
                {
                    return DateTime.MinValue;
                }

                return UnixEpoch.AddSeconds(ResetsAtUnix).ToLocalTime();
            }
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
