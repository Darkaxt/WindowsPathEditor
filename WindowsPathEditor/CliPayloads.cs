using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace WindowsPathEditor
{
    [DataContract]
    internal sealed class CliPathsPayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "rawRegistry", Order = 2)]
        public CliRawRegistryPayload RawRegistry { get; set; }

        [DataMember(Name = "parsed", Order = 3)]
        public CliParsedPathPayload Parsed { get; set; }

        [DataMember(Name = "process", Order = 4)]
        public CliProcessPathPayload Process { get; set; }

        [DataMember(Name = "mismatches", Order = 5)]
        public CliPathMismatchPayload Mismatches { get; set; }
    }

    [DataContract]
    internal sealed class CliRawRegistryPayload
    {
        [DataMember(Name = "systemPath", Order = 1)]
        public string SystemPath { get; set; }

        [DataMember(Name = "userPath", Order = 2)]
        public string UserPath { get; set; }
    }

    [DataContract]
    internal class CliParsedPathPayload
    {
        [DataMember(Name = "systemPath", Order = 1)]
        public IList<string> SystemPath { get; set; }

        [DataMember(Name = "userPath", Order = 2)]
        public IList<string> UserPath { get; set; }

        [DataMember(Name = "effectivePath", Order = 3)]
        public IList<string> EffectivePath { get; set; }
    }

    [DataContract]
    internal sealed class CliAnalyzedPathPayload : CliParsedPathPayload
    {
        [DataMember(Name = "conflicts", Order = 4)]
        public CliConflictMetricsPayload Conflicts { get; set; }
    }

    [DataContract]
    internal sealed class CliProcessPathPayload
    {
        [DataMember(Name = "raw", Order = 1)]
        public string Raw { get; set; }

        [DataMember(Name = "entries", Order = 2)]
        public IList<string> Entries { get; set; }
    }

    [DataContract]
    internal sealed class CliPathMismatchPayload
    {
        [DataMember(Name = "processOnlyEntries", Order = 1)]
        public IList<string> ProcessOnlyEntries { get; set; }

        [DataMember(Name = "registryOnlyEntries", Order = 2)]
        public IList<string> RegistryOnlyEntries { get; set; }

        [DataMember(Name = "crossScopeDuplicates", Order = 3)]
        public IList<string> CrossScopeDuplicates { get; set; }
    }

    [DataContract]
    internal sealed class CliCleanupPayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "before", Order = 2)]
        public CliParsedPathPayload Before { get; set; }

        [DataMember(Name = "after", Order = 3)]
        public CliParsedPathPayload After { get; set; }

        [DataMember(Name = "removed", Order = 4)]
        public IList<CliCleanupRemovedEntryPayload> Removed { get; set; }

        [DataMember(Name = "preservedUnresolved", Order = 5)]
        public IList<CliCleanupRemovedEntryPayload> PreservedUnresolved { get; set; }
    }

    [DataContract]
    internal sealed class CliCleanupRemovedEntryPayload
    {
        [DataMember(Name = "scope", Order = 1)]
        public string Scope { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "reason", Order = 3)]
        public string Reason { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictsPayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "extensions", Order = 2)]
        public IList<string> Extensions { get; set; }

        [DataMember(Name = "metrics", Order = 3)]
        public CliConflictMetricsPayload Metrics { get; set; }

        [DataMember(Name = "groups", Order = 4)]
        public IList<CliConflictGroupPayload> Groups { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictGroupPayload
    {
        [DataMember(Name = "title", Order = 1)]
        public string Title { get; set; }

        [DataMember(Name = "participantSummary", Order = 2)]
        public string ParticipantSummary { get; set; }

        [DataMember(Name = "columns", Order = 3)]
        public IList<CliConflictColumnPayload> Columns { get; set; }

        [DataMember(Name = "rows", Order = 4)]
        public IList<CliConflictRowPayload> Rows { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictColumnPayload
    {
        [DataMember(Name = "pathIndex", Order = 1)]
        public int PathIndex { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "origin", Order = 3)]
        public string Origin { get; set; }

        [DataMember(Name = "sourcePathIndexes", Order = 4)]
        public IList<int> SourcePathIndexes { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictRowPayload
    {
        [DataMember(Name = "filename", Order = 1)]
        public string Filename { get; set; }

        [DataMember(Name = "cells", Order = 2)]
        public IList<CliConflictCellPayload> Cells { get; set; }

        [DataMember(Name = "winnerState", Order = 3)]
        public string WinnerState { get; set; }

        [DataMember(Name = "winnerSummary", Order = 4)]
        public string WinnerSummary { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictCellPayload
    {
        [DataMember(Name = "displayValue", Order = 1)]
        public string DisplayValue { get; set; }

        [DataMember(Name = "hasComparableVersion", Order = 2)]
        public bool HasComparableVersion { get; set; }

        [DataMember(Name = "isRuntimeWinner", Order = 3)]
        public bool IsRuntimeWinner { get; set; }

        [DataMember(Name = "isHighestVersion", Order = 4)]
        public bool IsHighestVersion { get; set; }
    }

    [DataContract]
    internal sealed class CliConflictMetricsPayload
    {
        [DataMember(Name = "groupCount", Order = 1)]
        public int GroupCount { get; set; }

        [DataMember(Name = "rowCount", Order = 2)]
        public int RowCount { get; set; }

        [DataMember(Name = "mixedScopeGroupCount", Order = 3)]
        public int MixedScopeGroupCount { get; set; }

        [DataMember(Name = "mixedScopeRowCount", Order = 4)]
        public int MixedScopeRowCount { get; set; }

        [DataMember(Name = "shadowedByHigherVersionRowCount", Order = 5)]
        public int ShadowedByHigherVersionRowCount { get; set; }
    }

    [DataContract]
    internal sealed class CliAutosortMovedEntryPayload
    {
        [DataMember(Name = "scope", Order = 1)]
        public string Scope { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "fromIndex", Order = 3)]
        public int FromIndex { get; set; }

        [DataMember(Name = "toIndex", Order = 4)]
        public int ToIndex { get; set; }
    }

    [DataContract]
    internal sealed class CliAutosortPromotionPayload
    {
        [DataMember(Name = "originalPath", Order = 1)]
        public string OriginalPath { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "originalScope", Order = 3)]
        public string OriginalScope { get; set; }

        [DataMember(Name = "proposedScope", Order = 4)]
        public string ProposedScope { get; set; }

        [DataMember(Name = "ownership", Order = 5)]
        public string Ownership { get; set; }
    }

    [DataContract]
    internal sealed class CliAutosortNormalizationPayload
    {
        [DataMember(Name = "originalPath", Order = 1)]
        public string OriginalPath { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "scope", Order = 3)]
        public string Scope { get; set; }

        [DataMember(Name = "ownership", Order = 4)]
        public string Ownership { get; set; }
    }

    [DataContract]
    internal sealed class CliAutosortWarningPayload
    {
        [DataMember(Name = "kind", Order = 1)]
        public string Kind { get; set; }

        [DataMember(Name = "path", Order = 2)]
        public string Path { get; set; }

        [DataMember(Name = "message", Order = 3)]
        public string Message { get; set; }
    }

    [DataContract]
    internal sealed class CliAutosortPayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "before", Order = 2)]
        public CliAnalyzedPathPayload Before { get; set; }

        [DataMember(Name = "afterMigration", Order = 3)]
        public CliAnalyzedPathPayload AfterMigration { get; set; }

        [DataMember(Name = "after", Order = 4)]
        public CliAnalyzedPathPayload After { get; set; }

        [DataMember(Name = "promotions", Order = 5)]
        public IList<CliAutosortPromotionPayload> Promotions { get; set; }

        [DataMember(Name = "normalizations", Order = 6)]
        public IList<CliAutosortNormalizationPayload> Normalizations { get; set; }

        [DataMember(Name = "movedEntries", Order = 7)]
        public IList<CliAutosortMovedEntryPayload> MovedEntries { get; set; }

        [DataMember(Name = "warnings", Order = 8)]
        public IList<CliAutosortWarningPayload> Warnings { get; set; }
    }

    [DataContract]
    internal sealed class CliMigrationSummaryPayload
    {
        [DataMember(Name = "normalizationCount", Order = 1)]
        public int NormalizationCount { get; set; }

        [DataMember(Name = "promotionCount", Order = 2)]
        public int PromotionCount { get; set; }

        [DataMember(Name = "manualReviewCount", Order = 3)]
        public int ManualReviewCount { get; set; }

        [DataMember(Name = "duplicateGroupCount", Order = 4)]
        public int DuplicateGroupCount { get; set; }

        [DataMember(Name = "canWriteSystemPath", Order = 5)]
        public bool CanWriteSystemPath { get; set; }

        [DataMember(Name = "requiresElevationToApply", Order = 6)]
        public bool RequiresElevationToApply { get; set; }
    }

    [DataContract]
    internal sealed class CliMigrationEntryPayload
    {
        [DataMember(Name = "action", Order = 1)]
        public string Action { get; set; }

        [DataMember(Name = "originalScope", Order = 2)]
        public string OriginalScope { get; set; }

        [DataMember(Name = "proposedScope", Order = 3)]
        public string ProposedScope { get; set; }

        [DataMember(Name = "ownership", Order = 4)]
        public string Ownership { get; set; }

        [DataMember(Name = "originalPath", Order = 5)]
        public string OriginalPath { get; set; }

        [DataMember(Name = "proposedPath", Order = 6)]
        public string ProposedPath { get; set; }

        [DataMember(Name = "isResolved", Order = 7)]
        public bool IsResolved { get; set; }

        [DataMember(Name = "resolvedPath", Order = 8)]
        public string ResolvedPath { get; set; }

        [DataMember(Name = "isNormalized", Order = 9)]
        public bool IsNormalized { get; set; }

        [DataMember(Name = "isPromotedToSystem", Order = 10)]
        public bool IsPromotedToSystem { get; set; }

        [DataMember(Name = "requiresManualReview", Order = 11)]
        public bool RequiresManualReview { get; set; }

        [DataMember(Name = "notes", Order = 12)]
        public IList<string> Notes { get; set; }
    }

    [DataContract]
    internal sealed class CliMigrationDuplicateEntryPayload
    {
        [DataMember(Name = "scope", Order = 1)]
        public string Scope { get; set; }

        [DataMember(Name = "originalPath", Order = 2)]
        public string OriginalPath { get; set; }

        [DataMember(Name = "proposedPath", Order = 3)]
        public string ProposedPath { get; set; }
    }

    [DataContract]
    internal sealed class CliMigrationDuplicateGroupPayload
    {
        [DataMember(Name = "path", Order = 1)]
        public string Path { get; set; }

        [DataMember(Name = "entries", Order = 2)]
        public IList<CliMigrationDuplicateEntryPayload> Entries { get; set; }
    }

    [DataContract]
    internal sealed class CliMigratePayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "summary", Order = 2)]
        public CliMigrationSummaryPayload Summary { get; set; }

        [DataMember(Name = "before", Order = 3)]
        public CliAnalyzedPathPayload Before { get; set; }

        [DataMember(Name = "afterMigration", Order = 4)]
        public CliAnalyzedPathPayload AfterMigration { get; set; }

        [DataMember(Name = "afterAutosort", Order = 5)]
        public CliAnalyzedPathPayload AfterAutosort { get; set; }

        [DataMember(Name = "entries", Order = 6)]
        public IList<CliMigrationEntryPayload> Entries { get; set; }

        [DataMember(Name = "duplicateGroups", Order = 7)]
        public IList<CliMigrationDuplicateGroupPayload> DuplicateGroups { get; set; }
    }

    [DataContract]
    internal sealed class CliScanPayload
    {
        [DataMember(Name = "source", Order = 1)]
        public string Source { get; set; }

        [DataMember(Name = "root", Order = 2)]
        public string Root { get; set; }

        [DataMember(Name = "depth", Order = 3)]
        public int Depth { get; set; }

        [DataMember(Name = "currentPath", Order = 4)]
        public IList<string> CurrentPath { get; set; }

        [DataMember(Name = "results", Order = 5)]
        public IList<CliScanResultPayload> Results { get; set; }
    }

    [DataContract]
    internal sealed class CliScanResultPayload
    {
        [DataMember(Name = "path", Order = 1)]
        public string Path { get; set; }

        [DataMember(Name = "wouldAdd", Order = 2)]
        public bool WouldAdd { get; set; }
    }

    internal static class CliAnalysisPayloadFactory
    {
        private const string CleanupSource = "cleanup";
        private const string ConflictsSource = "conflicts";
        private const string AutosortSource = "autosort";
        private const string MigrateSource = "migrate";

        public static CliCleanupPayload CreateCleanup(CliPathsPayload paths)
        {
            if (paths == null)
            {
                throw new System.ArgumentNullException("paths");
            }

            var systemEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.SystemPath);
            var userEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.UserPath);
            var cleaned = PathCleanup.Clean(systemEntries, userEntries);

            return new CliCleanupPayload
            {
                Source = paths.Source ?? CleanupSource,
                Before = BuildParsedPayload(systemEntries, userEntries),
                After = BuildParsedPayload(cleaned.SystemPath, cleaned.UserPath),
                Removed = BuildRemovedEntries(systemEntries, userEntries),
                PreservedUnresolved = BuildPreservedUnresolvedEntries(systemEntries, userEntries)
            };
        }

        public static CliConflictsPayload CreateConflicts(
            CliPathsPayload paths,
            IEnumerable<string> executableExtensions)
        {
            if (paths == null)
            {
                throw new System.ArgumentNullException("paths");
            }

            var systemEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.SystemPath);
            var userEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.UserPath);
            var effectiveEntries = systemEntries.Concat(userEntries).ToList();
            var extensions = SafeStrings(executableExtensions);
            var report = PathConflictAnalyzer.BuildReport(
                effectiveEntries,
                extensions,
                systemEntries.Count);

            return new CliConflictsPayload
            {
                Source = paths.Source ?? ConflictsSource,
                Extensions = extensions,
                Metrics = BuildMetricsPayload(report),
                Groups = BuildGroupPayloads(report)
            };
        }

        public static CliAutosortPayload CreateAutosort(
            CliPathsPayload paths,
            IEnumerable<string> executableExtensions)
        {
            if (paths == null)
            {
                throw new System.ArgumentNullException("paths");
            }

            var systemEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.SystemPath);
            var userEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.UserPath);
            var extensions = SafeStrings(executableExtensions);
            var plan = AutoSortPlanner.Build(
                systemEntries,
                userEntries,
                extensions,
                PathMigrationPolicy.CreateDefault(),
                AutoSortPlannerMode.AggressivePromotion);

            return new CliAutosortPayload
            {
                Source = paths.Source ?? AutosortSource,
                Before = BuildAnalyzedPayload(plan.Before),
                AfterMigration = BuildAnalyzedPayload(plan.AfterMigration),
                After = BuildAnalyzedPayload(plan.AfterAutosort),
                Promotions = plan.Promotions.Select(promotion => new CliAutosortPromotionPayload
                {
                    OriginalPath = promotion.OriginalPath.SymbolicPath,
                    Path = promotion.Path.SymbolicPath,
                    OriginalScope = promotion.OriginalScope.ToString(),
                    ProposedScope = promotion.ProposedScope.ToString(),
                    Ownership = promotion.Ownership.ToString()
                }).ToList(),
                Normalizations = plan.Normalizations.Select(normalization => new CliAutosortNormalizationPayload
                {
                    OriginalPath = normalization.OriginalPath.SymbolicPath,
                    Path = normalization.Path.SymbolicPath,
                    Scope = normalization.Scope.ToString(),
                    Ownership = normalization.Ownership.ToString()
                }).ToList(),
                MovedEntries = plan.Reorders.Select(reorder => new CliAutosortMovedEntryPayload
                {
                    Scope = reorder.Scope.ToString().ToLowerInvariant(),
                    Path = reorder.Path.SymbolicPath,
                    FromIndex = reorder.FromIndex,
                    ToIndex = reorder.ToIndex
                }).ToList(),
                Warnings = plan.Warnings.Select(warning => new CliAutosortWarningPayload
                {
                    Kind = warning.Kind.ToString(),
                    Path = warning.Path == null ? "" : warning.Path.SymbolicPath,
                    Message = warning.Message
                }).ToList()
            };
        }

        public static CliMigratePayload CreateMigrate(
            CliPathsPayload paths,
            IEnumerable<string> executableExtensions,
            bool canWriteSystemPath)
        {
            if (paths == null)
            {
                throw new System.ArgumentNullException("paths");
            }

            var systemEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.SystemPath);
            var userEntries = ToPathEntries(paths.Parsed == null ? null : paths.Parsed.UserPath);
            var extensions = SafeStrings(executableExtensions);
            var result = PathMigrationSimulator.Simulate(
                systemEntries,
                userEntries,
                extensions,
                PathMigrationPolicy.CreateDefault());

            return new CliMigratePayload
            {
                Source = paths.Source ?? MigrateSource,
                Summary = new CliMigrationSummaryPayload
                {
                    NormalizationCount = result.NormalizationCount,
                    PromotionCount = result.PromotionCount,
                    ManualReviewCount = result.ManualReviewCount,
                    DuplicateGroupCount = result.DuplicateGroupsAfterMigration.Count,
                    CanWriteSystemPath = canWriteSystemPath,
                    RequiresElevationToApply = result.WouldChangeSystemPath && !canWriteSystemPath
                },
                Before = BuildAnalyzedPayload(result.OriginalSystemPath, result.OriginalUserPath, ToMetricsPayload(result.BeforeConflicts)),
                AfterMigration = BuildAnalyzedPayload(result.SimulatedSystemPath, result.SimulatedUserPath, ToMetricsPayload(result.AfterMigrationConflicts)),
                AfterAutosort = BuildAnalyzedPayload(result.AutosortedSystemPath, result.AutosortedUserPath, ToMetricsPayload(result.AfterAutosortConflicts)),
                Entries = result.Entries.Select(BuildMigrationEntryPayload).ToList(),
                DuplicateGroups = result.DuplicateGroupsAfterMigration.Select(BuildDuplicateGroupPayload).ToList()
            };
        }

        private static CliAnalyzedPathPayload BuildAnalyzedPayload(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            CliConflictMetricsPayload conflicts)
        {
            var payload = BuildParsedPayload(systemPath, userPath);
            return new CliAnalyzedPathPayload
            {
                SystemPath = payload.SystemPath,
                UserPath = payload.UserPath,
                EffectivePath = payload.EffectivePath,
                Conflicts = conflicts
            };
        }

        private static CliAnalyzedPathPayload BuildAnalyzedPayload(AutoSortPlanStage stage)
        {
            var safeStage = stage ?? new AutoSortPlanStage(AutoSortPlanStageKind.Before, null, null);
            return BuildAnalyzedPayload(safeStage.SystemPath, safeStage.UserPath, ToMetricsPayload(safeStage.Conflicts));
        }

        private static CliParsedPathPayload BuildParsedPayload(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            var systemEntries = (systemPath ?? Enumerable.Empty<PathEntry>()).ToList();
            var userEntries = (userPath ?? Enumerable.Empty<PathEntry>()).ToList();

            return new CliParsedPathPayload
            {
                SystemPath = ToSymbolicList(systemEntries),
                UserPath = ToSymbolicList(userEntries),
                EffectivePath = ToSymbolicList(systemEntries.Concat(userEntries))
            };
        }

        private static IList<CliCleanupRemovedEntryPayload> BuildRemovedEntries(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath)
        {
            var removed = new List<CliCleanupRemovedEntryPayload>();
            var seenResolvedPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            AppendCleanupRemovals(systemPath, "system", seenResolvedPaths, removed);
            AppendCleanupRemovals(userPath, "user", seenResolvedPaths, removed);

            return removed;
        }

        private static IList<CliCleanupRemovedEntryPayload> BuildPreservedUnresolvedEntries(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath)
        {
            var preserved = new List<CliCleanupRemovedEntryPayload>();

            AppendPreservedUnresolvedEntries(systemPath, "system", preserved);
            AppendPreservedUnresolvedEntries(userPath, "user", preserved);

            return preserved;
        }

        private static void AppendCleanupRemovals(
            IEnumerable<PathEntry> pathEntries,
            string scope,
            ISet<string> seenResolvedPaths,
            IList<CliCleanupRemovedEntryPayload> removed)
        {
            foreach (var path in pathEntries ?? Enumerable.Empty<PathEntry>())
            {
                PathResolution resolution;
                if (!path.TryResolve(out resolution))
                {
                    continue;
                }

                if (!path.Exists)
                {
                    removed.Add(new CliCleanupRemovedEntryPayload
                    {
                        Scope = scope,
                        Path = path.SymbolicPath,
                        Reason = "missing"
                    });
                    continue;
                }

                if (!seenResolvedPaths.Add(resolution.ActualPath))
                {
                    removed.Add(new CliCleanupRemovedEntryPayload
                    {
                        Scope = scope,
                        Path = path.SymbolicPath,
                        Reason = "duplicate"
                    });
                }
            }
        }

        private static void AppendPreservedUnresolvedEntries(
            IEnumerable<PathEntry> pathEntries,
            string scope,
            IList<CliCleanupRemovedEntryPayload> preserved)
        {
            foreach (var path in pathEntries ?? Enumerable.Empty<PathEntry>())
            {
                PathResolution resolution;
                if (path.TryResolve(out resolution))
                {
                    continue;
                }

                preserved.Add(new CliCleanupRemovedEntryPayload
                {
                    Scope = scope,
                    Path = path.SymbolicPath,
                    Reason = "unresolved"
                });
            }
        }

        private static CliConflictMetricsPayload BuildMetricsPayload(PathConflictReport report)
        {
            return ToMetricsPayload(PathConflictMetrics.FromReport(report));
        }

        private static IList<CliConflictGroupPayload> BuildGroupPayloads(PathConflictReport report)
        {
            var safeReport = report ?? PathConflictReport.Empty;
            return safeReport.Groups.Select(BuildGroupPayload).ToList();
        }

        private static CliConflictMetricsPayload ToMetricsPayload(PathConflictMetrics metrics)
        {
            var safeMetrics = metrics ?? PathConflictMetrics.FromReport(PathConflictReport.Empty);
            return new CliConflictMetricsPayload
            {
                GroupCount = safeMetrics.GroupCount,
                RowCount = safeMetrics.RowCount,
                MixedScopeGroupCount = safeMetrics.MixedScopeGroupCount,
                MixedScopeRowCount = safeMetrics.MixedScopeRowCount,
                ShadowedByHigherVersionRowCount = safeMetrics.ShadowedByHigherVersionRowCount
            };
        }

        private static IList<CliAutosortMovedEntryPayload> BuildMovedEntries(
            IList<PathEntry> before,
            IList<PathEntry> after,
            string scope)
        {
            var safeBefore = before ?? new List<PathEntry>();
            var safeAfter = after ?? new List<PathEntry>();
            var moved = new List<CliAutosortMovedEntryPayload>();
            var originalIndexes = BuildIndexQueues(safeBefore);

            for (var toIndex = 0; toIndex < safeAfter.Count; toIndex++)
            {
                var entry = safeAfter[toIndex];
                int fromIndex;
                if (!TryDequeueIndex(originalIndexes, entry, out fromIndex))
                {
                    continue;
                }

                if (fromIndex == toIndex)
                {
                    continue;
                }

                moved.Add(new CliAutosortMovedEntryPayload
                {
                    Scope = scope,
                    Path = entry.SymbolicPath,
                    FromIndex = fromIndex,
                    ToIndex = toIndex
                });
            }

            return moved;
        }

        private static IDictionary<PathEntry, Queue<int>> BuildIndexQueues(IList<PathEntry> entries)
        {
            var indexes = new Dictionary<PathEntry, Queue<int>>();
            for (var i = 0; i < entries.Count; i++)
            {
                Queue<int> queue;
                if (!indexes.TryGetValue(entries[i], out queue))
                {
                    queue = new Queue<int>();
                    indexes[entries[i]] = queue;
                }

                queue.Enqueue(i);
            }

            return indexes;
        }

        private static bool TryDequeueIndex(
            IDictionary<PathEntry, Queue<int>> indexes,
            PathEntry entry,
            out int index)
        {
            Queue<int> queue;
            if (!indexes.TryGetValue(entry, out queue) || queue.Count == 0)
            {
                index = -1;
                return false;
            }

            index = queue.Dequeue();
            return true;
        }

        private static CliMigrationEntryPayload BuildMigrationEntryPayload(PathMigrationSimulationEntry entry)
        {
            return new CliMigrationEntryPayload
            {
                Action = entry.ActionLabel,
                OriginalScope = entry.OriginalScope.ToString(),
                ProposedScope = entry.ProposedScope.ToString(),
                Ownership = entry.Ownership.ToString(),
                OriginalPath = entry.OriginalPath.SymbolicPath,
                ProposedPath = entry.ProposedPath.SymbolicPath,
                IsResolved = entry.IsResolved,
                ResolvedPath = entry.ResolvedPath,
                IsNormalized = entry.IsNormalized,
                IsPromotedToSystem = entry.IsPromotedToSystem,
                RequiresManualReview = entry.RequiresManualReview,
                Notes = entry.Notes.ToList()
            };
        }

        private static CliMigrationDuplicateGroupPayload BuildDuplicateGroupPayload(PathDuplicateGroup group)
        {
            return new CliMigrationDuplicateGroupPayload
            {
                Path = group.Path.SymbolicPath,
                Entries = group.Entries.Select(entry => new CliMigrationDuplicateEntryPayload
                {
                    Scope = entry.OriginalScope.ToString(),
                    OriginalPath = entry.OriginalPath.SymbolicPath,
                    ProposedPath = entry.ProposedPath.SymbolicPath
                }).ToList()
            };
        }

        private static CliConflictGroupPayload BuildGroupPayload(PathConflictGroup group)
        {
            return new CliConflictGroupPayload
            {
                Title = group.Title,
                ParticipantSummary = group.ParticipantSummary,
                Columns = group.Columns.Select(column => new CliConflictColumnPayload
                {
                    PathIndex = column.PathIndex,
                    Path = column.Path.SymbolicPath,
                    Origin = column.Origin.ToString(),
                    SourcePathIndexes = column.SourcePathIndexes.ToList()
                }).ToList(),
                Rows = group.Rows.Select(row => new CliConflictRowPayload
                {
                    Filename = row.Filename,
                    Cells = row.Cells.Select(cell => new CliConflictCellPayload
                    {
                        DisplayValue = cell.DisplayValue,
                        HasComparableVersion = cell.HasComparableVersion,
                        IsRuntimeWinner = cell.IsRuntimeWinner,
                        IsHighestVersion = cell.IsHighestVersion
                    }).ToList(),
                    WinnerState = row.WinnerState.ToString(),
                    WinnerSummary = row.WinnerSummary
                }).ToList()
            };
        }

        private static List<PathEntry> ToPathEntries(IEnumerable<string> symbolicPaths)
        {
            return (symbolicPaths ?? Enumerable.Empty<string>())
                .Select(path => new PathEntry(path))
                .Where(entry => !string.IsNullOrEmpty(entry.SymbolicPath))
                .ToList();
        }

        private static IList<string> ToSymbolicList(IEnumerable<PathEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathEntry>())
                .Select(entry => entry.SymbolicPath)
                .ToList();
        }

        private static IList<string> SafeStrings(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>()).ToList();
        }
    }
}
