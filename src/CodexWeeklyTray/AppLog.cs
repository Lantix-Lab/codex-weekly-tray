using System;
using System.IO;
using System.Text;

namespace CodexWeeklyTray
{
    internal static class AppLog
    {
        private static readonly object SyncRoot = new object();

        public static void Write(string message)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexWeeklyTray");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "app.log");

                lock (SyncRoot)
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                    {
                        File.Delete(path);
                    }
                    File.AppendAllText(
                        path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never terminate the tray application.
            }
        }
    }
}
