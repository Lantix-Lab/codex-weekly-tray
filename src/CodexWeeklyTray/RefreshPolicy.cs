namespace CodexWeeklyTray
{
    internal static class RefreshPolicy
    {
        public const int DefaultIntervalMinutes = 1;
        public const int MaxConsecutiveFailures = 3;

        private static readonly int[] Intervals = { 1, 5, 15, 30 };

        public static int[] GetSupportedIntervals()
        {
            return (int[])Intervals.Clone();
        }

        public static bool IsSupportedInterval(int minutes)
        {
            int index;
            for (index = 0; index < Intervals.Length; index++)
            {
                if (Intervals[index] == minutes)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ShouldShowUnavailable(bool hasSuccessfulRefresh, int consecutiveFailures)
        {
            return !hasSuccessfulRefresh || consecutiveFailures >= MaxConsecutiveFailures;
        }
    }
}
