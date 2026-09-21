namespace CodexWeeklyTray
{
    internal sealed class UsageWindowSet
    {
        public UsageWindowSet(UsageSnapshot fiveHour, UsageSnapshot weekly)
        {
            FiveHour = fiveHour;
            Weekly = weekly;
        }

        public UsageSnapshot FiveHour { get; private set; }

        public UsageSnapshot Weekly { get; private set; }

        public bool HasAny
        {
            get { return FiveHour != null || Weekly != null; }
        }
    }
}
