using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;

namespace WindowsPathEditor
{
    public static class PathConflictAnalyzer
    {
        private sealed class CollapsedPathParticipant
        {
            private readonly List<int> sourcePathIndexes = new List<int>();

            public CollapsedPathParticipant(PathEntry path, int pathIndex, PathConflictColumnOrigin origin)
            {
                Path = path;
                PathIndex = pathIndex;
                Origin = origin;
            }

            public PathEntry Path { get; private set; }
            public int PathIndex { get; private set; }
            public PathConflictColumnOrigin Origin { get; private set; }
            public IList<int> SourcePathIndexes { get { return sourcePathIndexes; } }

            public void AddSourcePathIndex(int pathIndex, PathConflictColumnOrigin origin)
            {
                sourcePathIndexes.Add(pathIndex);
                if (Origin != origin)
                {
                    Origin = PathConflictColumnOrigin.Mixed;
                }
            }

            public PathConflictColumn ToColumn()
            {
                return new PathConflictColumn(Path, PathIndex, SourcePathIndexes, Origin);
            }
        }

        private sealed class PathConflictVersionInfo
        {
            public PathConflictVersionInfo(string displayValue, bool hasComparableVersion, Version comparableVersion)
            {
                DisplayValue = displayValue ?? "";
                HasComparableVersion = hasComparableVersion;
                ComparableVersion = comparableVersion;
            }

            public string DisplayValue { get; private set; }
            public bool HasComparableVersion { get; private set; }
            public Version ComparableVersion { get; private set; }
        }

        public static PathConflictReport BuildReport(
            IEnumerable<PathEntry> orderedPath,
            IEnumerable<string> executableExtensions,
            int systemPathCount = 0)
        {
            var pathList = orderedPath.ToList();
            if (pathList.Count == 0)
            {
                return PathConflictReport.Empty;
            }

            var trackedExtensions = new HashSet<string>(new[] { ".dll", ".exe" }, StringComparer.OrdinalIgnoreCase);
            var owners = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < pathList.Count; i++)
            {
                foreach (var file in ListFiles(pathList[i], trackedExtensions))
                {
                    List<int> pathIndexes;
                    if (!owners.TryGetValue(file, out pathIndexes))
                    {
                        pathIndexes = new List<int>();
                        owners[file] = pathIndexes;
                    }

                    pathIndexes.Add(i);
                }
            }

            var conflictingFiles = owners
                .Where(_ => _.Value.Count > 1)
                .OrderBy(_ => _.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (conflictingFiles.Count == 0)
            {
                return PathConflictReport.Empty;
            }

            var conflictsByPathIndex = new Dictionary<int, IList<string>>();
            var groupsByKey = new Dictionary<string, List<PathConflictRow>>(StringComparer.Ordinal);
            var columnsByGroupKey = new Dictionary<string, List<PathConflictColumn>>(StringComparer.Ordinal);
            var graphEdges = new Dictionary<Tuple<int, int>, HashSet<string>>();
            var boundedSystemPathCount = Math.Max(0, Math.Min(systemPathCount, pathList.Count));

            foreach (var conflict in conflictingFiles)
            {
                var participantIndexes = conflict.Value.OrderBy(_ => _).ToList();
                var columns = CollapseParticipants(pathList, participantIndexes, boundedSystemPathCount);
                if (columns.Count < 2)
                {
                    continue;
                }

                var row = BuildRow(columns, conflict.Key);
                if (row == null)
                {
                    continue;
                }

                var groupKey = string.Join("|", columns.Select(_ => _.PathIndex.ToString()).ToArray());

                List<PathConflictRow> groupedFiles;
                if (!groupsByKey.TryGetValue(groupKey, out groupedFiles))
                {
                    groupedFiles = new List<PathConflictRow>();
                    groupsByKey[groupKey] = groupedFiles;
                    columnsByGroupKey[groupKey] = columns.ToList();
                }

                groupedFiles.Add(row);

                foreach (var column in columns)
                {
                    foreach (var participantIndex in column.SourcePathIndexes)
                    {
                        IList<string> pathConflicts;
                        if (!conflictsByPathIndex.TryGetValue(participantIndex, out pathConflicts))
                        {
                            pathConflicts = new List<string>();
                            conflictsByPathIndex[participantIndex] = pathConflicts;
                        }

                        pathConflicts.Add(row.Filename);
                    }
                }

                for (var i = 0; i < columns.Count; i++)
                {
                    for (var j = i + 1; j < columns.Count; j++)
                    {
                        var edgeKey = Tuple.Create(columns[i].PathIndex, columns[j].PathIndex);
                        HashSet<string> edgeFiles;
                        if (!graphEdges.TryGetValue(edgeKey, out edgeFiles))
                        {
                            edgeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            graphEdges[edgeKey] = edgeFiles;
                        }

                        edgeFiles.Add(row.Filename);
                    }
                }
            }

            foreach (var key in conflictsByPathIndex.Keys.ToList())
            {
                conflictsByPathIndex[key] = conflictsByPathIndex[key]
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(_ => _, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var groups = groupsByKey
                .Select(entry =>
                {
                    var columns = columnsByGroupKey[entry.Key];
                    var rows = entry.Value
                        .OrderBy(_ => _.Filename, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new PathConflictGroup(columns, rows);
                })
                .OrderBy(group => group.Columns.Min(_ => _.PathIndex))
                .ThenBy(group => group.Columns.Count)
                .ThenBy(group => group.ParticipantSummary, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.Count == 0)
            {
                return PathConflictReport.Empty;
            }

            var edges = graphEdges
                .OrderBy(_ => _.Key.Item1)
                .ThenBy(_ => _.Key.Item2)
                .Select(_ => new PathConflictGraphEdge(
                    _.Key.Item1,
                    _.Key.Item2,
                    _.Value.OrderBy(filename => filename, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();

            var graph = new PathConflictGraph(
                groups.SelectMany(_ => _.Columns).Select(_ => _.PathIndex).Distinct().OrderBy(_ => _).ToList(),
                edges);

            return new PathConflictReport(groups, conflictsByPathIndex, graph, false);
        }

        private static IList<PathConflictColumn> CollapseParticipants(
            IList<PathEntry> pathList,
            IList<int> participantIndexes,
            int systemPathCount)
        {
            var participantsByKey = new Dictionary<string, CollapsedPathParticipant>(StringComparer.OrdinalIgnoreCase);
            var orderedParticipants = new List<CollapsedPathParticipant>();

            foreach (var participantIndex in participantIndexes.OrderBy(_ => _))
            {
                var key = GetCollapsedParticipantKey(pathList[participantIndex]);
                CollapsedPathParticipant participant;
                if (!participantsByKey.TryGetValue(key, out participant))
                {
                    participant = new CollapsedPathParticipant(
                        pathList[participantIndex],
                        participantIndex,
                        GetOrigin(participantIndex, systemPathCount));
                    participantsByKey[key] = participant;
                    orderedParticipants.Add(participant);
                }

                participant.AddSourcePathIndex(participantIndex, GetOrigin(participantIndex, systemPathCount));
            }

            return orderedParticipants
                .Select(_ => _.ToColumn())
                .ToList();
        }

        private static PathConflictRow BuildRow(IList<PathConflictColumn> columns, string filename)
        {
            var versionInfos = columns
                .Select(column => GetVersionInfo(column.Path, filename))
                .ToList();
            if (AllComparableVersionsAreIdentical(versionInfos))
            {
                return null;
            }

            var highestVersion = versionInfos
                .Where(_ => _.HasComparableVersion)
                .Select(_ => _.ComparableVersion)
                .OrderByDescending(_ => _)
                .FirstOrDefault();
            var hasComparableVersions = highestVersion != null;

            var cells = versionInfos
                .Select((info, position) => new PathConflictCell(
                    info.DisplayValue,
                    info.HasComparableVersion,
                    position == 0,
                    hasComparableVersions &&
                        info.HasComparableVersion &&
                        info.ComparableVersion != null &&
                        info.ComparableVersion.Equals(highestVersion)))
                .ToList();

            PathConflictWinnerState winnerState;
            string winnerSummary;
            if (!hasComparableVersions)
            {
                winnerState = PathConflictWinnerState.Unknown;
                winnerSummary = "No comparable file version metadata is available for this conflict.";
            }
            else if (cells[0].IsHighestVersion)
            {
                winnerState = PathConflictWinnerState.Preferred;
                winnerSummary = "The current PATH winner already has the highest known file version.";
            }
            else
            {
                winnerState = PathConflictWinnerState.ShadowedByHigherVersion;
                winnerSummary = "A later PATH entry contains a higher known file version than the current winner.";
            }

            return new PathConflictRow(filename, cells, winnerState, winnerSummary);
        }

        private static string GetCollapsedParticipantKey(PathEntry path)
        {
            PathResolution resolution;
            if (path.TryResolve(out resolution))
            {
                return "R:" + resolution.ActualPath;
            }

            return "S:" + path.SymbolicPath;
        }

        private static bool AllComparableVersionsAreIdentical(IEnumerable<PathConflictVersionInfo> versionInfos)
        {
            var comparableVersions = versionInfos.ToList();
            if (comparableVersions.Count == 0 || comparableVersions.Any(_ => !_.HasComparableVersion || _.ComparableVersion == null))
            {
                return false;
            }

            return comparableVersions
                .Select(_ => _.ComparableVersion)
                .Distinct()
                .Count() == 1;
        }

        private static PathConflictColumnOrigin GetOrigin(int pathIndex, int systemPathCount)
        {
            return pathIndex < systemPathCount
                ? PathConflictColumnOrigin.System
                : PathConflictColumnOrigin.User;
        }

        private static IEnumerable<string> ListFiles(PathEntry path, ISet<string> trackedExtensions)
        {
            PathResolution resolution;
            if (!path.TryResolve(out resolution) || !Directory.Exists(resolution.ActualPathForAccess))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                return Directory.EnumerateFiles(resolution.ActualPathForAccess)
                    .Where(file => trackedExtensions.Contains(Path.GetExtension(file)))
                    .Select(Path.GetFileName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException ||
                ex is PathTooLongException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                Debug.Print("Error enumerating conflict files for {0}: {1}", path.SymbolicPath, ex.Message);
                return Enumerable.Empty<string>();
            }
        }

        private static PathConflictVersionInfo GetVersionInfo(PathEntry path, string filename)
        {
            PathResolution resolution;
            if (!path.TryResolve(out resolution))
            {
                return new PathConflictVersionInfo("", false, null);
            }

            var fullPath = Path.Combine(resolution.ActualPathForAccess, filename);
            if (!File.Exists(fullPath))
            {
                return new PathConflictVersionInfo("", false, null);
            }

            try
            {
                var info = FileVersionInfo.GetVersionInfo(fullPath);
                var hasNumericVersion = info.FileMajorPart != 0 || info.FileMinorPart != 0 || info.FileBuildPart != 0 || info.FilePrivatePart != 0;
                var numericVersion = hasNumericVersion
                    ? new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart)
                    : null;

                if (!string.IsNullOrEmpty(info.FileVersion))
                {
                    return new PathConflictVersionInfo(info.FileVersion, numericVersion != null, numericVersion);
                }

                if (numericVersion != null)
                {
                    return new PathConflictVersionInfo(numericVersion.ToString(), true, numericVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Error reading file version for {0}: {1}", fullPath, ex.Message);
            }

            return new PathConflictVersionInfo("n/a", false, null);
        }
    }
}
