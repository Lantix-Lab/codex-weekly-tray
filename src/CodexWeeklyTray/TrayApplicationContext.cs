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
        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem refreshItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly Timer refreshTimer;
        private readonly CodexAppServerClient client;
        private Icon currentIcon;
        private bool refreshing;
        private bool disposed;

        public TrayApplicationContext()
        {
            client = new CodexAppServerClient(AppLog.Write);
            currentIcon = TrayIconRenderer.RenderUnavailable();

            statusItem = new ToolStripMenuItem("Codex weekly remaining: --") { Enabled = false };
            refreshItem = new ToolStripMenuItem("Refresh now");
            refreshItem.Click += delegate { BeginRefresh(); };

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
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refreshItem);
            menu.Items.Add(usagePageItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            notifyIcon = new NotifyIcon
            {
                Icon = currentIcon,
                Text = "Codex weekly limit: loading...",
                ContextMenuStrip = menu,
                Visible = true
            };
            notifyIcon.DoubleClick += delegate { BeginRefresh(); };

            refreshTimer = new Timer { Interval = 250 };
            refreshTimer.Tick += delegate
            {
                refreshTimer.Stop();
                refreshTimer.Interval = 60000;
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
                UsageSnapshot snapshot = await client.FetchWeeklyUsageAsync();
                UpdateFromSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                AppLog.Write("Refresh failed: " + exception);
                UpdateUnavailable(exception.Message);
            }
            finally
            {
                refreshing = false;
                refreshItem.Enabled = true;
            }
        }

        private void UpdateFromSnapshot(UsageSnapshot snapshot)
        {
            Icon icon = TrayIconRenderer.Render(snapshot);
            ReplaceIcon(icon);

            string actualRemaining = Math.Round(snapshot.RemainingPercent, 0, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture);
            statusItem.Text = "Codex weekly remaining: " + actualRemaining + "%";

            string tooltip = "Codex weekly remaining: " + actualRemaining + "%";
            if (snapshot.ResetTimeLocal != DateTime.MinValue)
            {
                tooltip += " | Reset " + snapshot.ResetTimeLocal.ToString("MM-dd HH:mm");
            }
            SetTooltip(tooltip);
        }

        private void UpdateUnavailable(string message)
        {
            ReplaceIcon(TrayIconRenderer.RenderUnavailable());
            statusItem.Text = "Codex weekly remaining: --";
            SetTooltip("Codex rate-limit read failed: " + message);
        }

        private void ReplaceIcon(Icon icon)
        {
            Icon previous = currentIcon;
            currentIcon = icon;
            notifyIcon.Icon = icon;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        private void SetTooltip(string text)
        {
            if (text.Length > 63)
            {
                text = text.Substring(0, 60) + "...";
            }
            notifyIcon.Text = text;
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
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            if (currentIcon != null)
            {
                currentIcon.Dispose();
                currentIcon = null;
            }
            client.Dispose();
            base.Dispose();
        }
    }
}
