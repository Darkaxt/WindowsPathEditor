using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    internal static class CliPathSource
    {
        private const string LiveSource = "live-registry";
        private const string SnapshotSource = "snapshot";

        public static CliPathsPayload LoadLive(PathRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            return LoadLive(
                registry.RawSystemPath,
                registry.RawUserPath,
                Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        }

        internal static CliPathsPayload LoadLive(string rawSystemPath, string rawUserPath, string rawProcessPath)
        {
            return BuildFromRawStrings(
                LiveSource,
                new CliRawRegistryPayload
                {
                    SystemPath = rawSystemPath,
                    UserPath = rawUserPath
                },
                rawSystemPath,
                rawUserPath,
                rawProcessPath);
        }

        public static CliPathsPayload LoadSnapshot(CliInputSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            return BuildFromRawStrings(
                SnapshotSource,
                null,
                ToSnapshotRawString(snapshot.SystemPath),
                ToSnapshotRawString(snapshot.UserPath),
                Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
        }

        private static CliPathsPayload BuildFromRawStrings(
            string source,
            CliRawRegistryPayload rawRegistry,
            string rawSystemPath,
            string rawUserPath,
            string rawProcessPath)
        {
            var systemEntries = ParsePathList(rawSystemPath);
            var userEntries = ParsePathList(rawUserPath);
            var mergedEntries = systemEntries.Concat(userEntries).ToList();
            var processEntries = ParsePathList(rawProcessPath);

            return new CliPathsPayload
            {
                Source = source,
                RawRegistry = rawRegistry,
                Parsed = new CliParsedPathPayload
                {
                    SystemPath = ToSymbolicList(systemEntries),
                    UserPath = ToSymbolicList(userEntries),
                    EffectivePath = ToSymbolicList(mergedEntries)
                },
                Process = new CliProcessPathPayload
                {
                    Raw = rawProcessPath ?? string.Empty,
                    Entries = ToSymbolicList(processEntries)
                },
                Mismatches = new CliPathMismatchPayload
                {
                    ProcessOnlyEntries = ToSymbolicList(ExceptWithMultiplicity(processEntries, mergedEntries)),
                    RegistryOnlyEntries = ToSymbolicList(ExceptWithMultiplicity(mergedEntries, processEntries)),
                    CrossScopeDuplicates = ToSymbolicList(IntersectStable(systemEntries, userEntries))
                }
            };
        }

        private static List<PathEntry> ParsePathList(string rawPath)
        {
            return SplitPathSegments(rawPath)
                .Select(path => new PathEntry(path))
                .Where(entry => !string.IsNullOrEmpty(entry.SymbolicPath))
                .ToList();
        }

        private static List<PathEntry> ExceptWithMultiplicity(IEnumerable<PathEntry> source, IEnumerable<PathEntry> excluded)
        {
            var remaining = new Dictionary<PathEntry, int>();
            foreach (var entry in excluded)
            {
                int count;
                remaining.TryGetValue(entry, out count);
                remaining[entry] = count + 1;
            }

            var result = new List<PathEntry>();

            foreach (var entry in source)
            {
                int count;
                if (!remaining.TryGetValue(entry, out count) || count == 0)
                {
                    result.Add(entry);
                    continue;
                }

                remaining[entry] = count - 1;
            }

            return result;
        }

        private static List<PathEntry> IntersectStable(IEnumerable<PathEntry> first, IEnumerable<PathEntry> second)
        {
            var secondSet = new HashSet<PathEntry>(second);
            var result = new List<PathEntry>();
            var seen = new HashSet<PathEntry>();

            foreach (var entry in first)
            {
                if (!secondSet.Contains(entry) || !seen.Add(entry))
                {
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        private static List<string> ToSymbolicList(IEnumerable<PathEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathEntry>()).Select(entry => entry.SymbolicPath).ToList();
        }

        private static string ToSnapshotRawString(IEnumerable<string> paths)
        {
            return string.Join(";", (paths ?? Enumerable.Empty<string>()).Where(path => !string.IsNullOrEmpty(path)));
        }

        private static IEnumerable<string> SplitPathSegments(string path)
        {
            return (path ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
