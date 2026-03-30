using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public enum AutoSortPlannerMode
    {
        Conservative = 0,
        AggressivePromotion = 1
    }

    public enum AutoSortPlanStageKind
    {
        Before = 0,
        AfterMigration = 1,
        AfterAutosort = 2
    }

    public enum AutoSortWarningKind
    {
        UnresolvedPath = 0,
        CustomUserPathRetained = 1,
        UserOwnedSystemPath = 2,
        ManualReview = 3,
        PromotionCausesVersionConflict = 4
    }

    public sealed class AutoSortPlanStage
    {
        public AutoSortPlanStage(
            AutoSortPlanStageKind kind,
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath)
            : this(kind, systemPath, userPath, null)
        {
        }

        public AutoSortPlanStage(
            AutoSortPlanStageKind kind,
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            PathConflictMetrics conflicts)
        {
            Kind = kind;
            SystemPath = SafePath(systemPath);
            UserPath = SafePath(userPath);
            Conflicts = conflicts ?? PathConflictMetrics.FromReport(PathConflictReport.Empty);
        }

        public AutoSortPlanStageKind Kind { get; private set; }
        public IList<PathEntry> SystemPath { get; private set; }
        public IList<PathEntry> UserPath { get; private set; }
        public PathConflictMetrics Conflicts { get; private set; }

        public IList<PathEntry> EffectivePath
        {
            get { return SystemPath.Concat(UserPath).ToList(); }
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }
    }

    public sealed class AutoSortPromotion
    {
        public AutoSortPromotion(
            PathEntry originalPath,
            PathEntry path,
            PathScope originalScope,
            PathScope proposedScope,
            PathOwnership ownership)
        {
            OriginalPath = originalPath;
            Path = path;
            OriginalScope = originalScope;
            ProposedScope = proposedScope;
            Ownership = ownership;
        }

        public PathEntry OriginalPath { get; private set; }
        public PathEntry Path { get; private set; }
        public PathScope OriginalScope { get; private set; }
        public PathScope ProposedScope { get; private set; }
        public PathOwnership Ownership { get; private set; }
    }

    public sealed class AutoSortNormalization
    {
        public AutoSortNormalization(
            PathEntry originalPath,
            PathEntry path,
            PathScope scope,
            PathOwnership ownership)
        {
            OriginalPath = originalPath;
            Path = path;
            Scope = scope;
            Ownership = ownership;
        }

        public PathEntry OriginalPath { get; private set; }
        public PathEntry Path { get; private set; }
        public PathScope Scope { get; private set; }
        public PathOwnership Ownership { get; private set; }
    }

    public sealed class AutoSortReorder
    {
        public AutoSortReorder(PathScope scope, PathEntry path, int fromIndex, int toIndex)
        {
            Scope = scope;
            Path = path;
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public PathScope Scope { get; private set; }
        public PathEntry Path { get; private set; }
        public int FromIndex { get; private set; }
        public int ToIndex { get; private set; }
    }

    public sealed class AutoSortWarning
    {
        public AutoSortWarning(AutoSortWarningKind kind, PathEntry path, string message)
        {
            Kind = kind;
            Path = path;
            Message = message ?? "";
        }

        public AutoSortWarningKind Kind { get; private set; }
        public PathEntry Path { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class AutoSortDemotion
    {
        public AutoSortDemotion(PathEntry path, IEnumerable<string> conflictingFiles)
        {
            Path = path;
            ConflictingFiles = (conflictingFiles ?? Enumerable.Empty<string>()).ToList();
        }

        public PathEntry Path { get; private set; }
        public IList<string> ConflictingFiles { get; private set; }

        public string ConflictSummary
        {
            get
            {
                if (ConflictingFiles.Count == 0) return "";
                if (ConflictingFiles.Count <= 3)
                    return string.Join(", ", ConflictingFiles.ToArray());
                return string.Join(", ", ConflictingFiles.Take(3).ToArray()) +
                    string.Format(" (+{0} more)", ConflictingFiles.Count - 3);
            }
        }
    }

    public sealed class AutoSortPlan
    {
        public AutoSortPlan(
            AutoSortPlanStage before,
            AutoSortPlanStage afterMigration,
            AutoSortPlanStage afterAutosort,
            IEnumerable<AutoSortPromotion> promotions,
            IEnumerable<AutoSortNormalization> normalizations,
            IEnumerable<AutoSortReorder> reorders,
            IEnumerable<AutoSortDemotion> demotions,
            IEnumerable<AutoSortWarning> warnings)
        {
            Before = before ?? new AutoSortPlanStage(AutoSortPlanStageKind.Before, null, null);
            AfterMigration = afterMigration ?? new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, null, null);
            AfterAutosort = afterAutosort ?? new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, null, null);
            Promotions = (promotions ?? Enumerable.Empty<AutoSortPromotion>()).ToList();
            Normalizations = (normalizations ?? Enumerable.Empty<AutoSortNormalization>()).ToList();
            Reorders = (reorders ?? Enumerable.Empty<AutoSortReorder>()).ToList();
            Demotions = (demotions ?? Enumerable.Empty<AutoSortDemotion>()).ToList();
            Warnings = (warnings ?? Enumerable.Empty<AutoSortWarning>()).ToList();
        }

        public AutoSortPlanStage Before { get; private set; }
        public AutoSortPlanStage AfterMigration { get; private set; }
        public AutoSortPlanStage AfterAutosort { get; private set; }
        public IList<AutoSortPromotion> Promotions { get; private set; }
        public IList<AutoSortNormalization> Normalizations { get; private set; }
        public IList<AutoSortReorder> Reorders { get; private set; }
        public IList<AutoSortDemotion> Demotions { get; private set; }
        public IList<AutoSortWarning> Warnings { get; private set; }

        public bool HasChanges
        {
            get
            {
                return StagesDiffer(Before, AfterMigration) || StagesDiffer(AfterMigration, AfterAutosort);
            }
        }

        public bool HasPreviewContent
        {
            get
            {
                return HasChanges ||
                    Promotions.Count > 0 ||
                    Normalizations.Count > 0 ||
                    Reorders.Count > 0 ||
                    Demotions.Count > 0 ||
                    Warnings.Count > 0;
            }
        }

        private static bool StagesDiffer(AutoSortPlanStage left, AutoSortPlanStage right)
        {
            return !PathsEqual(left == null ? null : left.SystemPath, right == null ? null : right.SystemPath) ||
                !PathsEqual(left == null ? null : left.UserPath, right == null ? null : right.UserPath);
        }

        private static bool PathsEqual(IEnumerable<PathEntry> left, IEnumerable<PathEntry> right)
        {
            return SafePath(left).SequenceEqual(SafePath(right), PathEntryComparers.SymbolicPath);
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }
    }
}
