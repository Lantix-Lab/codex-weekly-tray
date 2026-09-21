using System;
using System.Threading;
using System.Windows.Forms;

namespace CodexWeeklyTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\CodexWeeklyTray", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args)
                {
                    AppLog.Write("UI exception: " + args.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
                {
                    AppLog.Write("Unhandled exception: " + args.ExceptionObject);
                };

                AppLog.Write("Application started: " + Application.ExecutablePath);
                try
                {
                    if (StartupManager.MigrateLegacyRegistration())
                    {
                        AppLog.Write("Migrated the legacy Run startup entry to the current-user Startup folder.");
                    }
                }
                catch (Exception exception)
                {
                    AppLog.Write("Startup registration migration failed: " + exception);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (TrayApplicationContext context = new TrayApplicationContext())
                {
                    Application.Run(context);
                }
            }
        }
    }
}
