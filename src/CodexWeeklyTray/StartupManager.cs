using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexWeeklyTray
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CodexWeeklyTray";
        private const string ShortcutFileName = "CodexWeeklyTray.lnk";

        public static bool IsEnabled()
        {
            string shortcutPath = GetShortcutPath();
            if (!File.Exists(shortcutPath))
            {
                return false;
            }

            string targetPath = ReadShortcutTarget(shortcutPath);
            return PathsEqual(targetPath, Application.ExecutablePath);
        }

        public static bool MigrateLegacyRegistration()
        {
            if (!HasLegacyRunEntry())
            {
                return false;
            }

            if (!IsEnabled())
            {
                CreateAndVerifyShortcut();
            }

            RemoveLegacyRunEntry();
            return true;
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                CreateAndVerifyShortcut();
                RemoveLegacyRunEntry();
                return;
            }

            string shortcutPath = GetShortcutPath();
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
            RemoveLegacyRunEntry();
        }

        private static void CreateAndVerifyShortcut()
        {
            string shortcutPath = GetShortcutPath();
            string startupDirectory = Path.GetDirectoryName(shortcutPath);
            if (String.IsNullOrWhiteSpace(startupDirectory))
            {
                throw new InvalidOperationException("The current-user Startup folder is unavailable.");
            }

            Directory.CreateDirectory(startupDirectory);
            CreateShortcut(shortcutPath, Application.ExecutablePath);

            if (!File.Exists(shortcutPath) || !PathsEqual(ReadShortcutTarget(shortcutPath), Application.ExecutablePath))
            {
                throw new InvalidOperationException("The Windows startup shortcut could not be verified.");
            }
        }

        private static string GetShortcutPath()
        {
            string startupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (String.IsNullOrWhiteSpace(startupDirectory))
            {
                throw new InvalidOperationException("The current-user Startup folder is unavailable.");
            }
            return Path.Combine(startupDirectory, ShortcutFileName);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    throw new InvalidOperationException("Windows Script Host is unavailable.");
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember(
                    "TargetPath",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { targetPath });
                shortcutType.InvokeMember(
                    "WorkingDirectory",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { Path.GetDirectoryName(targetPath) });
                shortcutType.InvokeMember(
                    "Description",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { "Codex usage limit tray indicator" });
                shortcutType.InvokeMember(
                    "Save",
                    BindingFlags.InvokeMethod,
                    null,
                    shortcut,
                    null);
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static string ReadShortcutTarget(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    throw new InvalidOperationException("Windows Script Host is unavailable.");
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });
                object target = shortcut.GetType().InvokeMember(
                    "TargetPath",
                    BindingFlags.GetProperty,
                    null,
                    shortcut,
                    null);
                return target as string;
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static bool HasLegacyRunEntry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        private static void RemoveLegacyRunEntry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            if (String.IsNullOrWhiteSpace(firstPath) || String.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            return String.Equals(
                Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }
}
