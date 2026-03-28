using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsPathEditor
{
    internal static class PathBackupExporter
    {
        private const string SystemEnvironmentKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        private const string UserEnvironmentKey = @"HKEY_CURRENT_USER\Environment";
        private const int BytesPerLine = 24;

        internal static string CreateBackupFileName(DateTime timestamp)
        {
            return string.Format("path_backup_{0:yyMMdd_HHmmss}.reg", timestamp);
        }

        internal static string BuildRegFileContents(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Windows Registry Editor Version 5.00");
            builder.AppendLine();
            AppendPathValue(builder, SystemEnvironmentKey, systemPath);
            builder.AppendLine();
            AppendPathValue(builder, UserEnvironmentKey, userPath);

            return builder.ToString();
        }

        internal static string WriteBackup(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath, DateTime timestamp)
        {
            var backupDirectory = ResolveBackupDirectory();
            Directory.CreateDirectory(backupDirectory);

            var backupPath = Path.Combine(backupDirectory, CreateBackupFileName(timestamp));
            File.WriteAllText(backupPath, BuildRegFileContents(systemPath, userPath), Encoding.Unicode);
            return backupPath;
        }

        private static void AppendPathValue(StringBuilder builder, string registryKey, IEnumerable<PathEntry> path)
        {
            builder.AppendLine("[" + registryKey + "]");
            builder.AppendLine("\"Path\"=" + FormatExpandStringValue(string.Join(";", SafePath(path).Select(_ => _.SymbolicPath).ToArray())));
        }

        private static string ResolveBackupDirectory()
        {
            var preferred = AppDomain.CurrentDomain.BaseDirectory;
            if (CanWriteToDirectory(preferred))
            {
                return preferred;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsPathEditor",
                "Backups");
        }

        private static bool CanWriteToDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var probePath = Path.Combine(directory, Path.GetRandomFileName());
                using (File.Create(probePath))
                {
                }
                File.Delete(probePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return path ?? Enumerable.Empty<PathEntry>();
        }

        private static string FormatExpandStringValue(string value)
        {
            var bytes = Encoding.Unicode.GetBytes((value ?? "") + "\0");
            var hexBytes = bytes.Select(_ => _.ToString("x2")).ToList();
            var lines = new List<string>();

            for (var i = 0; i < hexBytes.Count; i += BytesPerLine)
            {
                lines.Add(string.Join(",", hexBytes.Skip(i).Take(BytesPerLine).ToArray()));
            }

            return lines.Count == 1
                ? "hex(2):" + lines[0]
                : "hex(2):" + string.Join(",\\\r\n  ", lines.ToArray());
        }
    }
}
