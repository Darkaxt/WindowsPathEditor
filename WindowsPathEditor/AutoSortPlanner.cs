using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public static class AutoSortPlanner
    {
        public static AutoSortPlan Build(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy)
        {
            return Build(systemPath, userPath, executableExtensions, policy, AutoSortPlannerMode.Conservative);
        }

        public static AutoSortPlan Build(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy,
            AutoSortPlannerMode mode)
        {
            var extensions = (executableExtensions ?? Enumerable.Empty<string>()).ToList();
            var simMode = ToSimulationMode(mode);

            // Snapshot the original paths so the Before stage always reflects the
            // true starting point regardless of how many iterations the loop runs.
            var originalSystem = SafePath(systemPath);
            var originalUser = SafePath(userPath);

            // Iterate until the output stabilises (AfterAutosort == current input).
            // Each iteration's output becomes the next iteration's input so that
            // promotions, demotions, and reorders from one pass are visible to the next.
            // A cap of 20 prevents infinite loops on pathological inputs.
            var currentSystem = originalSystem;
            var currentUser = originalUser;
            PathMigrationSimulationResult firstResult = null;
            PathMigrationSimulationResult lastResult = null;
            PathMigrationSimulationResult firstPass1 = null;
            HashSet<string> firstPromotionExclusionKeys = null;
            const int maxIterations = 20;

            for (var i = 0; i < maxIterations; i++)
            {
                // Pass 1: full simulation with no exclusions to reveal conflicts.
                var pass1 = PathMigrationSimulator.Simulate(currentSystem, currentUser, extensions, policy, simMode);

                var systemDemotionKeys = FindSystemDemotionCandidates(pass1, extensions);
                var promotionExclusionKeys = FindBadPromotionKeys(pass1, extensions);

                // Pass 2: re-simulate with demotion and promotion-exclusion sets applied.
                var result = PathMigrationSimulator.Simulate(
                    currentSystem,
                    currentUser,
                    extensions,
                    policy,
                    simMode,
                    promotionExclusionKeys,
                    systemDemotionKeys);

                // The first iteration is always from the original input — its entries describe
                // promotions/demotions/normalizations relative to what the user started with.
                // Later iterations refine the applied paths but their entries are not meaningful
                // for display because they compare against an already-transformed input.
                if (i == 0)
                {
                    firstResult = result;
                    firstPass1 = pass1;
                    firstPromotionExclusionKeys = promotionExclusionKeys;
                }

                lastResult = result;

                // Stable when the autosorted output is identical to the current input.
                if (PathListsEqual(result.AutosortedSystemPath, currentSystem) &&
                    PathListsEqual(result.AutosortedUserPath, currentUser))
                    break;

                currentSystem = SafePath(result.AutosortedSystemPath);
                currentUser = SafePath(result.AutosortedUserPath);
            }

            // Display tabs (Promotions/Demotions/Normalizations/Warnings) are driven by the
            // first iteration so they describe what changes relative to the original input.
            // The applied paths (AfterAutosort) come from the last (stable) iteration.
            var warnings = BuildWarnings(firstResult.Entries)
                .Concat(BuildBlockedPromotionWarnings(firstPass1, firstPromotionExclusionKeys, extensions))
                .ToList();

            var beforeConflicts = PathConflictMetrics.FromReport(
                PathConflictAnalyzer.BuildReport(
                    originalSystem.Concat(originalUser),
                    extensions,
                    originalSystem.Count));

            return new AutoSortPlan(
                new AutoSortPlanStage(AutoSortPlanStageKind.Before, originalSystem, originalUser, beforeConflicts),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, firstResult.SimulatedSystemPath, firstResult.SimulatedUserPath, firstResult.AfterMigrationConflicts),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, lastResult.AutosortedSystemPath, lastResult.AutosortedUserPath, lastResult.AfterAutosortConflicts),
                BuildPromotions(firstResult.Entries),
                BuildNormalizations(firstResult.Entries),
                BuildReorders(lastResult),
                BuildDemotions(firstResult.Entries, extensions),
                warnings);
        }

        private static bool PathListsEqual(IEnumerable<PathEntry> a, IEnumerable<PathEntry> b)
        {
            return SafePath(a).SequenceEqual(SafePath(b), PathEntryComparers.SymbolicPath);
        }

        private static PathMigrationSimulationMode ToSimulationMode(AutoSortPlannerMode mode)
        {
            return mode == AutoSortPlannerMode.AggressivePromotion
                ? PathMigrationSimulationMode.AggressiveAutosort
                : PathMigrationSimulationMode.Conservative;
        }

        private static IList<AutoSortPromotion> BuildPromotions(IEnumerable<PathMigrationSimulationEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .Where(_ => _.IsPromotedToSystem)
                .Select(_ => new AutoSortPromotion(
                    _.OriginalPath,
                    _.ProposedPath,
                    _.OriginalScope,
                    _.ProposedScope,
                    _.Ownership))
                .ToList();
        }

        private static IList<AutoSortNormalization> BuildNormalizations(IEnumerable<PathMigrationSimulationEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .Where(_ => _.IsNormalized)
                .Select(_ => new AutoSortNormalization(
                    _.OriginalPath,
                    _.ProposedPath,
                    _.ProposedScope,
                    _.Ownership))
                .ToList();
        }

        private static IList<AutoSortWarning> BuildWarnings(IEnumerable<PathMigrationSimulationEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .Where(_ => _.RequiresManualReview)
                .Select(_ => new AutoSortWarning(
                    GetWarningKind(_),
                    _.OriginalPath,
                    _.Notes.FirstOrDefault() ?? "Manual review required."))
                .ToList();
        }

        private static AutoSortWarningKind GetWarningKind(PathMigrationSimulationEntry entry)
        {
            if (entry == null || !entry.IsResolved)
            {
                return AutoSortWarningKind.UnresolvedPath;
            }

            if (entry.OriginalScope == PathScope.System && entry.Ownership == PathOwnership.User)
            {
                return AutoSortWarningKind.UserOwnedSystemPath;
            }

            if (entry.OriginalScope == PathScope.User && entry.Ownership == PathOwnership.Custom)
            {
                return AutoSortWarningKind.CustomUserPathRetained;
            }

            return AutoSortWarningKind.ManualReview;
        }

        /// <summary>
        /// Returns the set of original User PATH symbolic paths whose promotion (in pass 1)
        /// would cause <see cref="PathConflictWinnerState.ShadowedByHigherVersion"/> conflicts
        /// in the simulated system path.  These are excluded from promotion in pass 2.
        /// </summary>
        private static HashSet<string> FindBadPromotionKeys(
            PathMigrationSimulationResult pass1,
            IList<string> extensions)
        {
            var promotedByProposed = pass1.Entries
                .Where(e => e.IsPromotedToSystem && e.ProposedPath != null)
                .ToDictionary(
                    e => e.ProposedPath.SymbolicPath ?? "",
                    e => e.OriginalPath != null ? e.OriginalPath.SymbolicPath ?? "" : "",
                    StringComparer.OrdinalIgnoreCase);

            if (promotedByProposed.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var report = PathConflictAnalyzer.BuildReport(
                pass1.SimulatedSystemPath.Concat(pass1.SimulatedUserPath),
                extensions,
                pass1.SimulatedSystemPath.Count);

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in report.Groups)
            {
                var hasShadowed = group.Rows.Any(r => r.WinnerState == PathConflictWinnerState.ShadowedByHigherVersion);
                if (!hasShadowed) continue;

                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                var winnerKey = winner.Path != null ? winner.Path.SymbolicPath ?? "" : "";

                string originalKey;
                if (promotedByProposed.TryGetValue(winnerKey, out originalKey))
                    keys.Add(originalKey);
            }

            return keys;
        }

        /// <summary>
        /// Returns the set of post-simulation System PATH symbolic paths that are winning
        /// over higher-version files that remain in User PATH after pass 1.  These are
        /// demoted to User PATH in pass 2 so the higher-version user files can win.
        ///
        /// The analysis runs on the SIMULATED path (not the original) so that entries
        /// promoted in pass 1 (e.g. Zulu, Java) are already in System PATH and produce
        /// System-vs-System conflicts — which are a reorder problem, not a demotion problem.
        /// Only entries where the highest-version file lives in a genuinely User PATH column
        /// are flagged for demotion.
        /// </summary>
        private static HashSet<string> FindSystemDemotionCandidates(
            PathMigrationSimulationResult pass1,
            IList<string> extensions)
        {
            var report = PathConflictAnalyzer.BuildReport(
                pass1.SimulatedSystemPath.Concat(pass1.SimulatedUserPath),
                extensions,
                pass1.SimulatedSystemPath.Count);

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in report.Groups)
            {
                // Winner must be a pure System PATH entry.
                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                if (winner.Origin != PathConflictColumnOrigin.System) continue;

                var winnerColumnIndex = group.Columns.IndexOf(winner);

                // Only demote when the highest-version cell belongs to a User PATH column.
                // System-vs-System version differences are a reorder problem, not demotion.
                var hasShadowedByUser = group.Rows.Any(r =>
                {
                    if (r.WinnerState != PathConflictWinnerState.ShadowedByHigherVersion) return false;
                    for (var i = 0; i < r.Cells.Count && i < group.Columns.Count; i++)
                    {
                        if (i == winnerColumnIndex) continue;
                        if (r.Cells[i].IsHighestVersion &&
                            group.Columns[i].Origin != PathConflictColumnOrigin.System)
                            return true;
                    }
                    return false;
                });

                if (!hasShadowedByUser) continue;

                keys.Add(winner.Path != null ? winner.Path.SymbolicPath ?? "" : "");
            }

            return keys;
        }

        /// <summary>
        /// Builds warnings for User PATH entries that were blocked from promotion in pass 2
        /// because they would have caused version-inversion conflicts.
        /// </summary>
        private static IEnumerable<AutoSortWarning> BuildBlockedPromotionWarnings(
            PathMigrationSimulationResult pass1,
            HashSet<string> promotionExclusionKeys,
            IList<string> extensions)
        {
            if (promotionExclusionKeys.Count == 0)
                yield break;

            // Map proposed path → file list from pass 1 conflicts
            var report = PathConflictAnalyzer.BuildReport(
                pass1.SimulatedSystemPath.Concat(pass1.SimulatedUserPath),
                extensions,
                pass1.SimulatedSystemPath.Count);

            var promotedByProposed = pass1.Entries
                .Where(e => e.IsPromotedToSystem && e.ProposedPath != null)
                .ToDictionary(
                    e => e.ProposedPath.SymbolicPath ?? "",
                    e => e,
                    StringComparer.OrdinalIgnoreCase);

            var shadowedByWinner = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in report.Groups)
            {
                var shadowedFiles = group.Rows
                    .Where(r => r.WinnerState == PathConflictWinnerState.ShadowedByHigherVersion)
                    .Select(r => r.Filename)
                    .ToList();

                if (shadowedFiles.Count == 0) continue;

                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                var winnerKey = winner.Path != null ? winner.Path.SymbolicPath ?? "" : "";

                PathMigrationSimulationEntry entry;
                if (!promotedByProposed.TryGetValue(winnerKey, out entry)) continue;

                var originalKey = entry.OriginalPath != null ? entry.OriginalPath.SymbolicPath ?? "" : "";
                if (!promotionExclusionKeys.Contains(originalKey)) continue;

                List<string> acc;
                if (!shadowedByWinner.TryGetValue(winnerKey, out acc))
                {
                    acc = new List<string>();
                    shadowedByWinner[winnerKey] = acc;
                }

                acc.AddRange(shadowedFiles);
            }

            foreach (var kv in shadowedByWinner)
            {
                PathMigrationSimulationEntry entry;
                if (!promotedByProposed.TryGetValue(kv.Key, out entry)) continue;

                var files = kv.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var fileList = files.Count <= 3
                    ? string.Join(", ", files.ToArray())
                    : string.Join(", ", files.Take(3).ToArray()) +
                      string.Format(" (+{0} more)", files.Count - 3);

                yield return new AutoSortWarning(
                    AutoSortWarningKind.PromotionCausesVersionConflict,
                    entry.OriginalPath,
                    string.Format(
                        "Promotion to System PATH was blocked because it would shadow higher-version files " +
                        "in User PATH: {0}. This entry will remain in User PATH.",
                        fileList));
            }
        }

        private static IList<AutoSortDemotion> BuildDemotions(
            IEnumerable<PathMigrationSimulationEntry> entries,
            IList<string> extensions)
        {
            var demotedEntries = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .Where(e => e.IsDemotedToUser)
                .ToList();

            if (demotedEntries.Count == 0)
                return new List<AutoSortDemotion>();

            // For each demoted entry, find which files it was shadowing in User PATH
            // by checking conflicts in the ORIGINAL path.
            var allEntries = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>()).ToList();
            var originalSystem = allEntries.Where(e => e.OriginalScope == PathScope.System)
                .Select(e => e.OriginalPath).ToList();
            var originalUser = allEntries.Where(e => e.OriginalScope == PathScope.User)
                .Select(e => e.OriginalPath).ToList();

            var report = PathConflictAnalyzer.BuildReport(
                originalSystem.Concat(originalUser),
                extensions,
                originalSystem.Count);

            var demotionFilesByPath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in report.Groups)
            {
                var shadowedFiles = group.Rows
                    .Where(r => r.WinnerState == PathConflictWinnerState.ShadowedByHigherVersion)
                    .Select(r => r.Filename)
                    .ToList();
                if (shadowedFiles.Count == 0) continue;

                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                var winnerKey = winner.Path != null ? winner.Path.SymbolicPath ?? "" : "";

                List<string> acc;
                if (!demotionFilesByPath.TryGetValue(winnerKey, out acc))
                {
                    acc = new List<string>();
                    demotionFilesByPath[winnerKey] = acc;
                }

                acc.AddRange(shadowedFiles);
            }

            var result = new List<AutoSortDemotion>();
            foreach (var entry in demotedEntries)
            {
                var key = entry.OriginalPath != null ? entry.OriginalPath.SymbolicPath ?? "" : "";
                List<string> files;
                demotionFilesByPath.TryGetValue(key, out files);

                var distinctFiles = (files ?? Enumerable.Empty<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                result.Add(new AutoSortDemotion(entry.OriginalPath, distinctFiles));
            }

            return result;
        }

        private static IList<AutoSortReorder> BuildReorders(PathMigrationSimulationResult result)
        {
            var reorders = new List<AutoSortReorder>();
            if (result == null)
            {
                return reorders;
            }

            reorders.AddRange(BuildScopedReorders(
                PathScope.System,
                result.SimulatedSystemPath,
                result.AutosortedSystemPath));
            reorders.AddRange(BuildScopedReorders(
                PathScope.User,
                result.SimulatedUserPath,
                result.AutosortedUserPath));
            return reorders;
        }

        private static IList<AutoSortReorder> BuildScopedReorders(
            PathScope scope,
            IEnumerable<PathEntry> before,
            IEnumerable<PathEntry> after)
        {
            var original = SafePath(before);
            var reordered = SafePath(after);
            var originalIndexes = BuildIndexQueues(original);
            var reorders = new List<AutoSortReorder>();

            for (var i = 0; i < reordered.Count; i++)
            {
                var path = reordered[i];
                Queue<int> indexes;
                if (!originalIndexes.TryGetValue(GetPathKey(path), out indexes) || indexes.Count == 0)
                {
                    continue;
                }

                var originalIndex = indexes.Dequeue();
                if (originalIndex != i)
                {
                    reorders.Add(new AutoSortReorder(scope, path, originalIndex, i));
                }
            }

            return reorders;
        }

        private static IDictionary<string, Queue<int>> BuildIndexQueues(IEnumerable<PathEntry> path)
        {
            var indexes = new Dictionary<string, Queue<int>>(StringComparer.OrdinalIgnoreCase);
            var list = SafePath(path);
            for (var i = 0; i < list.Count; i++)
            {
                var key = GetPathKey(list[i]);
                Queue<int> queue;
                if (!indexes.TryGetValue(key, out queue))
                {
                    queue = new Queue<int>();
                    indexes[key] = queue;
                }

                queue.Enqueue(i);
            }

            return indexes;
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }

        private static string GetPathKey(PathEntry path)
        {
            return path == null ? "" : path.SymbolicPath ?? "";
        }
    }
}
