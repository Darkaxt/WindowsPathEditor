using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace WindowsPathEditor
{
    public class PathEntry
    {
        private static IEnumerable<KeyValuePair<string, string>> environment;

        public PathEntry(string symbolicPath)
        {
            SymbolicPath = (symbolicPath ?? "")
                .Replace('/', '\\');

            var invalidChars = new Regex("[" + Regex.Escape(string.Join("", Path.GetInvalidPathChars())) + "]");
            SymbolicPath = invalidChars.Replace(SymbolicPath, "");

            var stripLaterColons = new Regex("^(.*:.*):");
            while (stripLaterColons.IsMatch(SymbolicPath))
            {
                SymbolicPath = stripLaterColons.Replace(SymbolicPath, "$1");
            }
        }

        /// <summary>
        /// The path with placeholders (%WINDIR%, etc...)
        /// </summary>
        public string SymbolicPath { get; private set; }

        /// <summary>
        /// The actual path
        /// </summary>
        public string ActualPath
        {
            get
            {
                PathResolution resolution;
                return TryResolve(out resolution) ? resolution.ActualPath : SymbolicPath;
            }
        }

        /// <summary>
        /// Path used for file system access (handles WOW64 redirection for System32)
        /// </summary>
        public string ActualPathForAccess
        {
            get
            {
                PathResolution resolution;
                return TryResolve(out resolution) ? resolution.ActualPathForAccess : SymbolicPath;
            }
        }

        /// <summary>
        /// Whether the given directory actually exists
        /// </summary>
        public bool Exists
        {
            get
            {
                PathResolution resolution;
                return TryResolve(out resolution) && Directory.Exists(resolution.ActualPathForAccess);
            }
        }

        public IEnumerable<PathMatch> Find(string prefix)
        {
            PathResolution resolution;
            if (!TryResolve(out resolution)) return Enumerable.Empty<PathMatch>();

            try
            {
                return Directory.EnumerateFiles(resolution.ActualPathForAccess, prefix + "*")
                    .Select(file => new PathMatch(resolution.ActualPath, Path.GetFileName(file)));
            }
            catch (IOException)
            {
                return Enumerable.Empty<PathMatch>();
            }
            catch (ArgumentException)
            {
                return Enumerable.Empty<PathMatch>();
            }
            catch (NotSupportedException)
            {
                return Enumerable.Empty<PathMatch>();
            }
        }

        public PathResolution Resolve()
        {
            try
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(SymbolicPath);
                var actualPath = TrimTrailingBackslashes(Path.GetFullPath(expandedPath));
                return PathResolution.Resolved(actualPath, ResolvePathForAccess(actualPath));
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is NotSupportedException ||
                ex is PathTooLongException ||
                ex is SecurityException)
            {
                return PathResolution.Unresolved(ex.Message);
            }
        }

        public bool TryResolve(out PathResolution resolution)
        {
            resolution = Resolve();
            return resolution.IsResolved;
        }

        public override string ToString()
        {
            return SymbolicPath;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PathEntry)) return false;
            return string.Equals(GetComparisonKey(), ((PathEntry)obj).GetComparisonKey(), StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(GetComparisonKey());
        }

        /// <summary>
        /// Try to find an environment variable that matches part of the path and
        /// return that, otherwise return a normal path entry.
        /// </summary>
        /// <remarks>
        /// Be sure to return the longest possible entry, but don't include entries
        /// of fewer than 4 characters (this is to avoid returning %HOMEDRIVE% all the
        /// time, because that one is fairly uninteresting and confusing).
        /// </remarks>
        public static PathEntry FromFilePath(string path)
        {
            if (environment == null)
            {
                environment = GetEnvironment().ToList();
            }

            return FromFilePath(path, environment);
        }

        public static PathEntry FromFilePath(string path, IEnumerable<KeyValuePair<string, string>> environmentVariables)
        {
            foreach (var entry in OrderEnvironment(environmentVariables))
            {
                if (entry.Value != "" && Directory.Exists(entry.Value) && path.StartsWith(entry.Value, StringComparison.OrdinalIgnoreCase))
                {
                    var suffixIndex = entry.Value.Length;
                    if (entry.Value.EndsWith("\\", StringComparison.OrdinalIgnoreCase) &&
                        path.Length > entry.Value.Length)
                    {
                        suffixIndex--;
                    }

                    return new PathEntry("%" + entry.Key + "%" + path.Substring(suffixIndex));
                }
            }

            return new PathEntry(path);
        }

        internal static string ResolvePathForAccess(string actualPath)
        {
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                var system32 = Path.Combine(windowsDir, "System32");
                if (actualPath.StartsWith(system32, StringComparison.OrdinalIgnoreCase))
                {
                    var tail = actualPath.Substring(system32.Length).TrimStart('\\');
                    var sysnative = Path.Combine(Path.Combine(windowsDir, "Sysnative"), tail);
                    if (Directory.Exists(sysnative))
                    {
                        return sysnative;
                    }
                }
            }

            return actualPath;
        }

        private string GetComparisonKey()
        {
            PathResolution resolution;
            if (TryResolve(out resolution))
            {
                return "R:" + resolution.ActualPath;
            }

            return "S:" + SymbolicPath;
        }

        private static IEnumerable<KeyValuePair<string, string>> OrderEnvironment(IEnumerable<KeyValuePair<string, string>> environmentVariables)
        {
            return environmentVariables
                .Where(entry =>
                    !string.IsNullOrEmpty(entry.Value) &&
                    (entry.Value.Length > 3 || string.Equals(entry.Key, "SystemDrive", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(entry => entry.Value.Length)
                .ThenByDescending(entry => entry.Key.Length)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<KeyValuePair<string, string>> GetEnvironment()
        {
            var environmentVariables = new List<KeyValuePair<string, string>>();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                environmentVariables.Add(new KeyValuePair<string, string>((string)entry.Key, (string)entry.Value));
            }

            return environmentVariables;
        }

        private static string TrimTrailingBackslashes(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var root = Path.GetPathRoot(path);
            if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return path.TrimEnd('\\');
        }
    }
}
