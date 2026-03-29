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

        internal static string CreateApplyFileName(DateTime timestamp)
        {
            return string.Format("apply_path_{0:yyMMdd_HHmmss}.reg", timestamp);
        }

        internal static string BuildRegFileContents(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            return BuildRegFileContents(systemPath, userPath, true, true);
        }

        internal static string BuildRegFileContents(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            bool includeSystemPath,
            bool includeUserPath)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Windows Registry Editor Version 5.00");
            builder.AppendLine();

            if (includeSystemPath)
            {
                AppendPathValue(builder, SystemEnvironmentKey, systemPath);
            }

            if (includeSystemPath && includeUserPath)
            {
                builder.AppendLine();
            }

            if (includeUserPath)
            {
                AppendPathValue(builder, UserEnvironmentKey, userPath);
            }

            return builder.ToString();
        }

        internal static string WriteBackup(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath, DateTime timestamp)
        {
            return WriteRegFile(
                CreateBackupFileName(timestamp),
                systemPath,
                userPath,
                timestamp,
                true,
                true);
        }

        internal static string WriteApplyFile(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            DateTime timestamp,
            bool includeSystemPath,
            bool includeUserPath)
        {
            return WriteRegFile(
                CreateApplyFileName(timestamp),
                systemPath,
                userPath,
                timestamp,
                includeSystemPath,
                includeUserPath);
        }

        private static void AppendPathValue(StringBuilder builder, string registryKey, IEnumerable<PathEntry> path)
        {
            builder.AppendLine("[" + registryKey + "]");
            builder.AppendLine("\"Path\"=" + FormatExpandStringValue(string.Join(";", SafePath(path).Select(_ => _.SymbolicPath).ToArray())));
        }

        internal static string ResolveOutputDirectory()
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

        private static string WriteRegFile(
            string fileName,
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            DateTime timestamp,
            bool includeSystemPath,
            bool includeUserPath)
        {
            var outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            var outputPath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(
                outputPath,
                BuildRegFileContents(systemPath, userPath, includeSystemPath, includeUserPath),
                Encoding.Unicode);
            return outputPath;
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
