using System;
using System.Collections.Generic;
using System.IO;

namespace CodexWeeklyTray
{
    internal static class CodexLocator
    {
        public static string FindExecutable()
        {
            string explicitPath = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
            if (!String.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            string[] pathEntries = pathValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            int index;
            for (index = 0; index < pathEntries.Length; index++)
            {
                string entry = pathEntries[index].Trim().Trim('"');
                string candidate = Path.Combine(entry, "codex.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string binRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
            if (Directory.Exists(binRoot))
            {
                List<FileInfo> candidates = new List<FileInfo>();
                string[] directories = Directory.GetDirectories(binRoot);
                for (index = 0; index < directories.Length; index++)
                {
                    string candidate = Path.Combine(directories[index], "codex.exe");
                    if (File.Exists(candidate))
                    {
                        candidates.Add(new FileInfo(candidate));
                    }
                }

                candidates.Sort(delegate(FileInfo left, FileInfo right)
                {
                    return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
                });
                if (candidates.Count > 0)
                {
                    return candidates[0].FullName;
                }
            }

            throw new FileNotFoundException("codex.exe was not found. Install Codex or set CODEX_CLI_PATH.");
        }
    }
}
