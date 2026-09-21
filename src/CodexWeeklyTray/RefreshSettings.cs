using System;
using System.Globalization;
using Microsoft.Win32;

namespace CodexWeeklyTray
{
    internal static class RefreshSettings
    {
        private const string SettingsKeyPath = @"Software\CodexWeeklyTray";
        private const string IntervalValueName = "RefreshIntervalMinutes";

        public static int LoadIntervalMinutes()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
            {
                if (key == null)
                {
                    return RefreshPolicy.DefaultIntervalMinutes;
                }

                object value = key.GetValue(IntervalValueName);
                if (value == null)
                {
                    return RefreshPolicy.DefaultIntervalMinutes;
                }

                int minutes;
                try
                {
                    minutes = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return RefreshPolicy.DefaultIntervalMinutes;
                }

                return RefreshPolicy.IsSupportedInterval(minutes)
                    ? minutes
                    : RefreshPolicy.DefaultIntervalMinutes;
            }
        }

        public static void SaveIntervalMinutes(int minutes)
        {
            if (!RefreshPolicy.IsSupportedInterval(minutes))
            {
                throw new ArgumentOutOfRangeException("minutes", "The refresh interval is not supported.");
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("The current-user settings key could not be created.");
                }
                key.SetValue(IntervalValueName, minutes, RegistryValueKind.DWord);
            }
        }
    }
}
