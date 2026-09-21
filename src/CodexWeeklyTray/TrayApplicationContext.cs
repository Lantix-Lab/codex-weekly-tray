using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexWeeklyTray
{
    internal sealed class TrayApplicationContext : ApplicationContext, IDisposable
    {
        private const string UsagePageUrl = "https://chatgpt.com/codex/settings/usage";
        private readonly NotifyIcon weeklyNotifyIcon;
        private readonly NotifyIcon fiveHourNotifyIcon;
        private readonly ToolStripMenuItem weeklyStatusItem;
        private readonly ToolStripMenuItem fiveHourStatusItem;
        private readonly ToolStripMenuItem refreshItem;
        private readonly ToolStripMenuItem refreshIntervalItem;
        private readonly ToolStripMenuItem[] refreshIntervalOptions;
        private readonly ToolStripMenuItem startupItem;
        private readonly Timer refreshTimer;
        private readonly CodexAppServerClient client;
        private Icon weeklyCurrentIcon;
        private Icon fiveHourCurrentIcon;
        private string weeklyLastStatus;
        private string fiveHourLastStatus;
        private int refreshIntervalMinutes;
        private int consecutiveRefreshFailures;
        private bool hasSuccessfulRefresh;
        private bool refreshing;
        private bool disposed;

        public TrayApplicationContext()
        {
            client = new CodexAppServerClient(AppLog.Write);
            weeklyCurrentIcon = TrayIconRenderer.RenderWeeklyUnavailable();

            weeklyStatusItem = new ToolStripMenuItem("Codex weekly remaining: --") { Enabled = false };
            fiveHourStatusItem = new ToolStripMenuItem("Codex 5-hour remaining: --")
            {
                Enabled = false,
                Visible = false
            };
            refreshItem = new ToolStripMenuItem("Refresh now");
            refreshItem.Click += delegate { BeginRefresh(); };

            refreshIntervalMinutes = SafeGetRefreshInterval();
            refreshIntervalItem = new ToolStripMenuItem("Refresh interval");
            int[] intervals = RefreshPolicy.GetSupportedIntervals();
            refreshIntervalOptions = new ToolStripMenuItem[intervals.Length];
            int intervalIndex;
            for (intervalIndex = 0; intervalIndex < intervals.Length; intervalIndex++)
            {
                ToolStripMenuItem option = CreateRefreshIntervalOption(intervals[intervalIndex]);
                refreshIntervalOptions[intervalIndex] = option;
                refreshIntervalItem.DropDownItems.Add(option);
            }

            ToolStripMenuItem usagePageItem = new ToolStripMenuItem("Open Codex usage page");
            usagePageItem.Click += delegate { OpenUsagePage(); };

            startupItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = SafeGetStartupState(),
                CheckOnClick = false
            };
            startupItem.Click += delegate { ToggleStartup(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitThread(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(weeklyStatusItem);
            menu.Items.Add(fiveHourStatusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refreshItem);
            menu.Items.Add(refreshIntervalItem);
            menu.Items.Add(usagePageItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            weeklyNotifyIcon = new NotifyIcon
            {
                Icon = weeklyCurrentIcon,
                Text = "Codex weekly limit: loading...",
                ContextMenuStrip = menu,
                Visible = true
            };
            weeklyNotifyIcon.DoubleClick += delegate { BeginRefresh(); };

            fiveHourNotifyIcon = new NotifyIcon
            {
                Text = "Codex 5-hour limit: loading...",
                ContextMenuStrip = menu,
                Visible = false
            };
            fiveHourNotifyIcon.DoubleClick += delegate { BeginRefresh(); };

            refreshTimer = new Timer { Interval = 250 };
            refreshTimer.Tick += delegate
            {
                refreshTimer.Stop();
                refreshTimer.Interval = GetRefreshIntervalMilliseconds();
                BeginRefresh();
                refreshTimer.Start();
            };
            refreshTimer.Start();
        }

        private async void BeginRefresh()
        {
            if (refreshing || disposed)
            {
                return;
            }

            refreshing = true;
            refreshItem.Enabled = false;
            try
            {
                UsageWindowSet windowSet = await client.FetchUsageAsync();
                consecutiveRefreshFailures = 0;
                hasSuccessfulRefresh = true;
                UpdateFromWindows(windowSet);
            }
            catch (Exception exception)
            {
                consecutiveRefreshFailures = Math.Min(
                    consecutiveRefreshFailures + 1,
                    RefreshPolicy.MaxConsecutiveFailures);
                AppLog.Write(
                    "Refresh failed (" + consecutiveRefreshFailures + "/" +
                    RefreshPolicy.MaxConsecutiveFailures + "): " + exception);
                if (RefreshPolicy.ShouldShowUnavailable(hasSuccessfulRefresh, consecutiveRefreshFailures))
                {
                    UpdateUnavailable(exception.Message);
                }
                else
                {
                    UpdateStale();
                }
            }
            finally
            {
                refreshing = false;
                refreshItem.Enabled = true;
            }
        }

        private void UpdateFromWindows(UsageWindowSet windowSet)
        {
            UpdateWindow(
                windowSet.Weekly,
                weeklyNotifyIcon,
                weeklyStatusItem,
                "Codex weekly remaining: ",
                false,
                ref weeklyCurrentIcon);
            weeklyLastStatus = windowSet.Weekly == null ? null : weeklyStatusItem.Text;
            UpdateWindow(
                windowSet.FiveHour,
                fiveHourNotifyIcon,
                fiveHourStatusItem,
                "Codex 5-hour remaining: ",
                true,
                ref fiveHourCurrentIcon);
            fiveHourLastStatus = windowSet.FiveHour == null ? null : fiveHourStatusItem.Text;
        }

        private void UpdateStale()
        {
            MarkWindowStale(weeklyNotifyIcon, weeklyStatusItem, weeklyLastStatus);
            MarkWindowStale(fiveHourNotifyIcon, fiveHourStatusItem, fiveHourLastStatus);
        }

        private void MarkWindowStale(NotifyIcon notifyIcon, ToolStripMenuItem statusItem, string lastStatus)
        {
            if (!notifyIcon.Visible || String.IsNullOrWhiteSpace(lastStatus))
            {
                return;
            }

            statusItem.Text = lastStatus + " (stale)";
            SetTooltip(
                notifyIcon,
                lastStatus + " | Stale " + consecutiveRefreshFailures + "/" +
                RefreshPolicy.MaxConsecutiveFailures);
        }

        private static void UpdateWindow(
            UsageSnapshot snapshot,
            NotifyIcon notifyIcon,
            ToolStripMenuItem statusItem,
            string label,
            bool fiveHour,
            ref Icon currentIcon)
        {
            if (snapshot == null)
            {
                notifyIcon.Visible = false;
                statusItem.Visible = false;
                return;
            }

            Icon icon = fiveHour
                ? TrayIconRenderer.RenderFiveHour(snapshot)
                : TrayIconRenderer.RenderWeekly(snapshot);
            ReplaceIcon(notifyIcon, icon, ref currentIcon);
            notifyIcon.Visible = true;
            statusItem.Visible = true;

            string actualRemaining = Math.Round(snapshot.RemainingPercent, 0, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture);
            statusItem.Text = label + actualRemaining + "%";

            string tooltip = label + actualRemaining + "%";
            if (snapshot.ResetTimeLocal != DateTime.MinValue)
            {
                tooltip += " | Reset " + snapshot.ResetTimeLocal.ToString("MM-dd HH:mm");
            }
            SetTooltip(notifyIcon, tooltip);
        }

        private void UpdateUnavailable(string message)
        {
            if (!weeklyNotifyIcon.Visible && !fiveHourNotifyIcon.Visible)
            {
                weeklyNotifyIcon.Visible = true;
                weeklyStatusItem.Visible = true;
            }

            if (weeklyNotifyIcon.Visible)
            {
                ReplaceIcon(weeklyNotifyIcon, TrayIconRenderer.RenderWeeklyUnavailable(), ref weeklyCurrentIcon);
                weeklyStatusItem.Text = "Codex weekly remaining: --";
                SetTooltip(weeklyNotifyIcon, "Codex rate-limit read failed: " + message);
            }

            if (fiveHourNotifyIcon.Visible)
            {
                ReplaceIcon(fiveHourNotifyIcon, TrayIconRenderer.RenderFiveHourUnavailable(), ref fiveHourCurrentIcon);
                fiveHourStatusItem.Text = "Codex 5-hour remaining: --";
                SetTooltip(fiveHourNotifyIcon, "Codex rate-limit read failed: " + message);
            }
        }

        private static void ReplaceIcon(NotifyIcon notifyIcon, Icon icon, ref Icon currentIcon)
        {
            Icon previous = currentIcon;
            currentIcon = icon;
            notifyIcon.Icon = icon;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        private static void SetTooltip(NotifyIcon notifyIcon, string text)
        {
            if (text.Length > 63)
            {
                text = text.Substring(0, 60) + "...";
            }
            notifyIcon.Text = text;
        }

        private ToolStripMenuItem CreateRefreshIntervalOption(int minutes)
        {
            string label = minutes == 1 ? "1 minute" : minutes + " minutes";
            ToolStripMenuItem option = new ToolStripMenuItem(label)
            {
                Tag = minutes,
                Checked = minutes == refreshIntervalMinutes,
                CheckOnClick = false
            };
            option.Click += delegate { SetRefreshInterval(minutes); };
            return option;
        }

        private int SafeGetRefreshInterval()
        {
            try
            {
                return RefreshSettings.LoadIntervalMinutes();
            }
            catch (Exception exception)
            {
                AppLog.Write("Read refresh interval failed: " + exception);
                return RefreshPolicy.DefaultIntervalMinutes;
            }
        }

        private void SetRefreshInterval(int minutes)
        {
            try
            {
                RefreshSettings.SaveIntervalMinutes(minutes);
                refreshIntervalMinutes = minutes;
                int index;
                for (index = 0; index < refreshIntervalOptions.Length; index++)
                {
                    refreshIntervalOptions[index].Checked =
                        Convert.ToInt32(refreshIntervalOptions[index].Tag) == minutes;
                }

                refreshTimer.Stop();
                refreshTimer.Interval = GetRefreshIntervalMilliseconds();
                refreshTimer.Start();
                BeginRefresh();
            }
            catch (Exception exception)
            {
                AppLog.Write("Update refresh interval failed: " + exception);
                MessageBox.Show(
                    "Unable to update the refresh interval: " + exception.Message,
                    "Codex Weekly Tray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private int GetRefreshIntervalMilliseconds()
        {
            return refreshIntervalMinutes * 60 * 1000;
        }

        private static void OpenUsagePage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(UsagePageUrl) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                AppLog.Write("Open usage page failed: " + exception);
            }
        }

        private bool SafeGetStartupState()
        {
            try
            {
                return StartupManager.IsEnabled();
            }
            catch (Exception exception)
            {
                AppLog.Write("Read startup state failed: " + exception);
                return false;
            }
        }

        private void ToggleStartup()
        {
            try
            {
                bool enabled = !StartupManager.IsEnabled();
                StartupManager.SetEnabled(enabled);
                startupItem.Checked = enabled;
            }
            catch (Exception exception)
            {
                AppLog.Write("Update startup state failed: " + exception);
                MessageBox.Show(
                    "Unable to update the startup setting: " + exception.Message,
                    "Codex Weekly Tray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        protected override void ExitThreadCore()
        {
            Dispose();
            base.ExitThreadCore();
        }

        public new void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            refreshTimer.Stop();
            refreshTimer.Dispose();
            weeklyNotifyIcon.Visible = false;
            fiveHourNotifyIcon.Visible = false;
            weeklyNotifyIcon.Dispose();
            fiveHourNotifyIcon.Dispose();
            if (weeklyCurrentIcon != null)
            {
                weeklyCurrentIcon.Dispose();
                weeklyCurrentIcon = null;
            }
            if (fiveHourCurrentIcon != null)
            {
                fiveHourCurrentIcon.Dispose();
                fiveHourCurrentIcon = null;
            }
            client.Dispose();
            base.Dispose();
        }
    }
}
