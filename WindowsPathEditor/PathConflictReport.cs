using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public enum PathConflictWinnerState
    {
        Unknown = 0,
        Preferred = 1,
        ShadowedByHigherVersion = 2
    }

    public enum PathConflictColumnOrigin
    {
        System = 0,
        User = 1,
        Mixed = 2
    }

    public sealed class PathConflictColumn
    {
        public PathConflictColumn(
            PathEntry path,
            int pathIndex,
            IEnumerable<int> sourcePathIndexes,
            PathConflictColumnOrigin origin)
        {
            Path = path;
            PathIndex = pathIndex;
            SourcePathIndexes = sourcePathIndexes.ToList();
            Origin = origin;
        }

        public PathEntry Path { get; private set; }
        public int PathIndex { get; private set; }
        public IList<int> SourcePathIndexes { get; private set; }
        public PathConflictColumnOrigin Origin { get; private set; }
        public string Header { get { return Path.SymbolicPath; } }

        public string OriginLabel
        {
            get
            {
                switch (Origin)
                {
                    case PathConflictColumnOrigin.System:
                        return "SYSTEM";
                    case PathConflictColumnOrigin.User:
                        return "USER";
                    default:
                        return "MIXED";
                }
            }
        }

        public string OriginTooltip
        {
            get
            {
                var originDescription =
                    Origin == PathConflictColumnOrigin.System ? "System PATH" :
                    Origin == PathConflictColumnOrigin.User ? "User PATH" :
                    "Both System and User PATH";

                if (SourcePathIndexes.Count <= 1)
                {
                    return originDescription;
                }

                return string.Format(
                    "{0}. Collapsed {1} matching PATH entries into this column.",
                    originDescription,
                    SourcePathIndexes.Count);
            }
        }
    }

    public sealed class PathConflictCell
    {
        public PathConflictCell(string displayValue, bool hasComparableVersion, bool isRuntimeWinner, bool isHighestVersion)
        {
            DisplayValue = displayValue ?? "";
            HasComparableVersion = hasComparableVersion;
            IsRuntimeWinner = isRuntimeWinner;
            IsHighestVersion = isHighestVersion;
        }

        public string DisplayValue { get; private set; }
        public bool HasComparableVersion { get; private set; }
        public bool IsRuntimeWinner { get; private set; }
        public bool IsHighestVersion { get; private set; }
    }

    public sealed class PathConflictRow
    {
        public PathConflictRow(
            string filename,
            IEnumerable<PathConflictCell> cells,
            PathConflictWinnerState winnerState,
            string winnerSummary)
        {
            Filename = filename;
            Cells = cells.ToList();
            WinnerState = winnerState;
            WinnerSummary = winnerSummary ?? "";
        }

        public string Filename { get; private set; }
        public IList<PathConflictCell> Cells { get; private set; }
        public PathConflictWinnerState WinnerState { get; private set; }
        public string WinnerSummary { get; private set; }
    }

    public sealed class PathConflictGroup
    {
        public PathConflictGroup(IEnumerable<PathConflictColumn> columns, IEnumerable<PathConflictRow> rows)
        {
            Columns = columns.ToList();
            Rows = rows.ToList();
        }

        public IList<PathConflictColumn> Columns { get; private set; }
        public IList<PathConflictRow> Rows { get; private set; }

        public string Title
        {
            get
            {
                return string.Format(
                    "{0} shared file{1} across {2} PATH location{3}",
                    Rows.Count,
                    Rows.Count == 1 ? "" : "s",
                    Columns.Count,
                    Columns.Count == 1 ? "" : "s");
            }
        }

        public string ParticipantSummary
        {
            get { return string.Join(" \u2192 ", Columns.Select(_ => _.Header).ToArray()); }
        }
    }

    public sealed class PathConflictGraphEdge
    {
        public PathConflictGraphEdge(int leftPathIndex, int rightPathIndex, IEnumerable<string> filenames)
        {
            LeftPathIndex = leftPathIndex;
            RightPathIndex = rightPathIndex;
            Filenames = filenames.ToList();
        }

        public int LeftPathIndex { get; private set; }
        public int RightPathIndex { get; private set; }
        public IList<string> Filenames { get; private set; }
    }

    public sealed class PathConflictGraph
    {
        public static readonly PathConflictGraph Empty =
            new PathConflictGraph(Enumerable.Empty<int>(), Enumerable.Empty<PathConflictGraphEdge>());

        public PathConflictGraph(IEnumerable<int> pathIndexes, IEnumerable<PathConflictGraphEdge> edges)
        {
            PathIndexes = pathIndexes.ToList();
            Edges = edges.ToList();
        }

        public IList<int> PathIndexes { get; private set; }
        public IList<PathConflictGraphEdge> Edges { get; private set; }
    }

    public sealed class PathConflictReport
    {
        public static readonly PathConflictReport Empty =
            new PathConflictReport(
                Enumerable.Empty<PathConflictGroup>(),
                new Dictionary<int, IList<string>>(),
                PathConflictGraph.Empty,
                false);

        public static readonly PathConflictReport Pending =
            new PathConflictReport(
                Enumerable.Empty<PathConflictGroup>(),
                new Dictionary<int, IList<string>>(),
                PathConflictGraph.Empty,
                true);

        public PathConflictReport(
            IEnumerable<PathConflictGroup> groups,
            IDictionary<int, IList<string>> conflictFilesByPathIndex,
            PathConflictGraph graph,
            bool isPending)
        {
            Groups = groups.ToList();
            ConflictFilesByPathIndex = conflictFilesByPathIndex;
            Graph = graph;
            IsPending = isPending;
        }

        public IList<PathConflictGroup> Groups { get; private set; }
        public IDictionary<int, IList<string>> ConflictFilesByPathIndex { get; private set; }
        public PathConflictGraph Graph { get; private set; }
        public bool IsPending { get; private set; }
        public bool HasConflicts { get { return Groups.Count > 0; } }
    }
}
