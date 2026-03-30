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
            var cleanup = PathCleanup.Clean(originalSystem, originalUser);
            var cleanupRecords = BuildCleanup(cleanup.RemovedEntries);

            // Iterate until the output stabilises (AfterAutosort == current input).
            // Each iteration's output becomes the next iteration's input so that
            // promotions, normalizations, and reorders from one pass are visible to the next.
            // A cap of 20 prevents infinite loops on pathological inputs.
            var currentSystem = SafePath(cleanup.SystemPath);
            var currentUser = SafePath(cleanup.UserPath);
            PathMigrationSimulationResult firstResult = null;
            PathMigrationSimulationResult lastResult = null;
            PathMigrationSimulationResult firstPass1 = null;
            HashSet<string> firstPromotionExclusionKeys = null;
            HashSet<string> firstSystemDemotionKeys = null;
            const int maxIterations = 20;

            for (var i = 0; i < maxIterations; i++)
            {
                // Pass 1: full simulation with no exclusions to reveal conflicts.
                var pass1 = PathMigrationSimulator.Simulate(currentSystem, currentUser, extensions, policy, simMode);

                var promotionExclusionKeys = FindBadPromotionKeys(pass1, extensions);
                var systemDemotionKeys = FindSystemDemotionKeys(pass1, extensions, policy);

                // Pass 2: re-simulate with promotion exclusions and selective demotions applied.
                // Only user-owned System PATH entries and non-SystemRoot system winners that
                // shadow higher-version user files are demoted automatically.
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
                    firstSystemDemotionKeys = systemDemotionKeys;
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
                .Concat(BuildBlockedSystemDemotionWarnings(firstPass1, firstSystemDemotionKeys, extensions))
                .ToList();

            var beforeConflicts = PathConflictMetrics.FromReport(
                PathConflictAnalyzer.BuildReport(
                    originalSystem.Concat(originalUser),
                    extensions,
                    originalSystem.Count));

            IList<PathEntry> finalSystemPath;
            IList<PathEntry> finalUserPath;
            PathConflictMetrics finalConflicts;
            AlphabetizeConflictFreePaths(
                lastResult.AutosortedSystemPath,
                lastResult.AutosortedUserPath,
                extensions,
                out finalSystemPath,
                out finalUserPath,
                out finalConflicts);

            return new AutoSortPlan(
                new AutoSortPlanStage(AutoSortPlanStageKind.Before, originalSystem, originalUser, beforeConflicts),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, firstResult.SimulatedSystemPath, firstResult.SimulatedUserPath, firstResult.AfterMigrationConflicts),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, finalSystemPath, finalUserPath, finalConflicts),
                BuildPromotions(firstResult.Entries),
                BuildNormalizations(firstResult.Entries),
                BuildReorders(
                    firstResult.SimulatedSystemPath,
                    firstResult.SimulatedUserPath,
                    finalSystemPath,
                    finalUserPath),
                BuildDemotions(firstResult.Entries, extensions),
                warnings,
                cleanupRecords);
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

        private static IList<AutoSortCleanup> BuildCleanup(IEnumerable<PathCleanupRemovedEntry> removedEntries)
        {
            return (removedEntries ?? Enumerable.Empty<PathCleanupRemovedEntry>())
                .Select(entry => new AutoSortCleanup(entry.Path, entry.Scope, entry.Kind))
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
            var promotedByProposed = GroupEntriesByProposedPath(
                (pass1.Entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                    .Where(e => e.IsPromotedToSystem && e.ProposedPath != null));

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
                var winnerKey = GetPathKey(winner.Path);

                List<PathMigrationSimulationEntry> promotedEntries;
                if (!promotedByProposed.TryGetValue(winnerKey, out promotedEntries))
                    continue;

                foreach (var entry in promotedEntries)
                {
                    keys.Add(GetPathKey(entry.OriginalPath));
                }
            }

            return keys;
        }

        /// <summary>
        /// Returns the set of original System PATH symbolic paths that should be demoted in
        /// pass 2. User-owned System PATH entries are always demoted. Machine/custom entries
        /// are demoted only when they shadow higher-version user files and are not inside the
        /// protected SystemRoot branch.
        /// </summary>
        private static HashSet<string> FindSystemDemotionKeys(
            PathMigrationSimulationResult pass1,
            IList<string> extensions,
            PathMigrationPolicy policy)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var systemEntries = (pass1 == null
                    ? Enumerable.Empty<PathMigrationSimulationEntry>()
                    : pass1.Entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .Where(e => e.OriginalScope == PathScope.System)
                .ToList();

            foreach (var entry in DistinctByOriginalPath(systemEntries.Where(e =>
                e.Ownership == PathOwnership.User &&
                !IsProtectedSystemPath(e, policy))))
            {
                keys.Add(GetPathKey(entry.OriginalPath));
            }

            if (pass1 == null)
                return keys;

            var report = PathConflictAnalyzer.BuildReport(
                pass1.SimulatedSystemPath.Concat(pass1.SimulatedUserPath),
                extensions,
                pass1.SimulatedSystemPath.Count);

            var systemEntriesByProposed = GroupEntriesByProposedPath(
                systemEntries.Where(e => e.ProposedPath != null));

            foreach (var group in report.Groups)
            {
                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                if (winner.Origin != PathConflictColumnOrigin.System) continue;

                var winnerColumnIndex = group.Columns.IndexOf(winner);
                var shadowedByUserFiles = group.Rows.Any(r => IsShadowedByUser(r, group.Columns, winnerColumnIndex));
                if (!shadowedByUserFiles) continue;

                List<PathMigrationSimulationEntry> matchingEntries;
                if (!systemEntriesByProposed.TryGetValue(GetPathKey(winner.Path), out matchingEntries)) continue;

                foreach (var entry in DistinctByOriginalPath(matchingEntries.Where(e =>
                    e.Ownership != PathOwnership.User &&
                    !IsProtectedSystemPath(e, policy))))
                {
                    keys.Add(GetPathKey(entry.OriginalPath));
                }
            }

            return keys;
        }

        /// <summary>
        /// Builds warnings for System PATH entries that currently win over higher-version
        /// files in User PATH but remain protected from automatic demotion.
        /// </summary>
        private static IEnumerable<AutoSortWarning> BuildBlockedSystemDemotionWarnings(
            PathMigrationSimulationResult pass1,
            HashSet<string> systemDemotionKeys,
            IList<string> extensions)
        {
            if (pass1 == null)
                yield break;

            var report = PathConflictAnalyzer.BuildReport(
                pass1.SimulatedSystemPath.Concat(pass1.SimulatedUserPath),
                extensions,
                pass1.SimulatedSystemPath.Count);

            var systemEntriesByProposed = GroupEntriesByProposedPath(
                (pass1.Entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                    .Where(e => e.OriginalScope == PathScope.System && e.ProposedPath != null));

            foreach (var group in report.Groups)
            {
                // Winner must be a pure System PATH entry.
                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                if (winner.Origin != PathConflictColumnOrigin.System) continue;

                var winnerKey = GetPathKey(winner.Path);
                List<PathMigrationSimulationEntry> matchingEntries;
                if (!systemEntriesByProposed.TryGetValue(winnerKey, out matchingEntries)) continue;

                var winnerColumnIndex = group.Columns.IndexOf(winner);

                var shadowedByUserFiles = group.Rows
                    .Where(r => IsShadowedByUser(r, group.Columns, winnerColumnIndex))
                    .Select(r => r.Filename)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (shadowedByUserFiles.Count == 0) continue;

                var fileList = shadowedByUserFiles.Count <= 3
                    ? string.Join(", ", shadowedByUserFiles.ToArray())
                    : string.Join(", ", shadowedByUserFiles.Take(3).ToArray()) +
                      string.Format(" (+{0} more)", shadowedByUserFiles.Count - 3);

                foreach (var entry in DistinctByOriginalPath(matchingEntries.Where(e =>
                    e.Ownership != PathOwnership.User &&
                    !systemDemotionKeys.Contains(GetPathKey(e.OriginalPath)))))
                {
                    yield return new AutoSortWarning(
                        AutoSortWarningKind.SystemDemotionRequiresManualReview,
                        entry.OriginalPath,
                        string.Format(
                            "Higher-version files were found later in User PATH ({0}), but automatic demotion of System PATH entries is disabled because it can create broader conflicts. Review manually.",
                            fileList));
                }
            }
        }

        /// <summary>
        /// Returns true when a row is shadowed by a higher-version file that lives in a
        /// non-System PATH column.
        /// </summary>
        private static bool IsShadowedByUser(
            PathConflictRow row,
            IList<PathConflictColumn> columns,
            int winnerColumnIndex)
        {
            if (row == null || row.WinnerState != PathConflictWinnerState.ShadowedByHigherVersion)
                return false;

            for (var i = 0; i < row.Cells.Count && i < columns.Count; i++)
            {
                if (i == winnerColumnIndex) continue;
                if (row.Cells[i].IsHighestVersion &&
                    columns[i].Origin != PathConflictColumnOrigin.System)
                {
                    return true;
                }
            }

            return false;
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

            var promotedByProposed = GroupEntriesByProposedPath(
                (pass1.Entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                    .Where(e => e.IsPromotedToSystem && e.ProposedPath != null));

            var shadowedFilesByOriginalKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var entriesByOriginalKey = new Dictionary<string, PathMigrationSimulationEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in report.Groups)
            {
                var shadowedFiles = group.Rows
                    .Where(r => r.WinnerState == PathConflictWinnerState.ShadowedByHigherVersion)
                    .Select(r => r.Filename)
                    .ToList();

                if (shadowedFiles.Count == 0) continue;

                var winner = group.Columns.OrderBy(c => c.PathIndex).First();
                var winnerKey = GetPathKey(winner.Path);

                List<PathMigrationSimulationEntry> promotedEntries;
                if (!promotedByProposed.TryGetValue(winnerKey, out promotedEntries)) continue;

                foreach (var entry in DistinctByOriginalPath(promotedEntries))
                {
                    var originalKey = GetPathKey(entry.OriginalPath);
                    if (!promotionExclusionKeys.Contains(originalKey)) continue;

                    if (!entriesByOriginalKey.ContainsKey(originalKey))
                    {
                        entriesByOriginalKey[originalKey] = entry;
                    }

                    List<string> acc;
                    if (!shadowedFilesByOriginalKey.TryGetValue(originalKey, out acc))
                    {
                        acc = new List<string>();
                        shadowedFilesByOriginalKey[originalKey] = acc;
                    }

                    acc.AddRange(shadowedFiles);
                }
            }

            foreach (var kv in shadowedFilesByOriginalKey)
            {
                PathMigrationSimulationEntry entry;
                if (!entriesByOriginalKey.TryGetValue(kv.Key, out entry)) continue;

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

        private static Dictionary<string, List<PathMigrationSimulationEntry>> GroupEntriesByProposedPath(
            IEnumerable<PathMigrationSimulationEntry> entries)
        {
            var grouped = new Dictionary<string, List<PathMigrationSimulationEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
            {
                List<PathMigrationSimulationEntry> matches;
                var key = GetPathKey(entry.ProposedPath);
                if (!grouped.TryGetValue(key, out matches))
                {
                    matches = new List<PathMigrationSimulationEntry>();
                    grouped[key] = matches;
                }

                matches.Add(entry);
            }

            return grouped;
        }

        private static IEnumerable<PathMigrationSimulationEntry> DistinctByOriginalPath(
            IEnumerable<PathMigrationSimulationEntry> entries)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
            {
                if (seen.Add(GetPathKey(entry.OriginalPath)))
                {
                    yield return entry;
                }
            }
        }

        private static bool IsProtectedSystemPath(
            PathMigrationSimulationEntry entry,
            PathMigrationPolicy policy)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ResolvedPath))
                return false;

            return GetProtectedSystemRoots(policy)
                .Any(root => IsPathUnderRoot(entry.ResolvedPath, root));
        }

        private static IList<string> GetProtectedSystemRoots(PathMigrationPolicy policy)
        {
            return (policy == null
                    ? Enumerable.Empty<KeyValuePair<string, string>>()
                    : policy.NormalizationVariables ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Where(variable => IsProtectedSystemRootVariableName(variable.Key))
                .Select(variable => PathMigrationPolicy.NormalizeRoot(variable.Value))
                .Where(root => !string.IsNullOrEmpty(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(root => root.Length)
                .ToList();
        }

        private static bool IsProtectedSystemRootVariableName(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return false;

            return variableName.Equals("SystemRoot", StringComparison.OrdinalIgnoreCase) ||
                variableName.Equals("windir", StringComparison.OrdinalIgnoreCase) ||
                variableName.EndsWith("SystemRoot", StringComparison.OrdinalIgnoreCase) ||
                variableName.EndsWith("windir", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathUnderRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
                return false;

            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return true;

            return path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
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

        private static IList<AutoSortReorder> BuildReorders(
            IEnumerable<PathEntry> beforeSystem,
            IEnumerable<PathEntry> beforeUser,
            IEnumerable<PathEntry> afterSystem,
            IEnumerable<PathEntry> afterUser)
        {
            var reorders = new List<AutoSortReorder>();
            reorders.AddRange(BuildScopedReorders(
                PathScope.System,
                beforeSystem,
                afterSystem));
            reorders.AddRange(BuildScopedReorders(
                PathScope.User,
                beforeUser,
                afterUser));
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

        private static void AlphabetizeConflictFreePaths(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IList<string> extensions,
            out IList<PathEntry> finalSystemPath,
            out IList<PathEntry> finalUserPath,
            out PathConflictMetrics finalConflicts)
        {
            var autosortedSystem = SafePath(systemPath);
            var autosortedUser = SafePath(userPath);
            var report = PathConflictAnalyzer.BuildReport(
                autosortedSystem.Concat(autosortedUser),
                extensions,
                autosortedSystem.Count);
            var conflictIndexes = new HashSet<int>(
                report.ConflictFilesByPathIndex.Keys,
                EqualityComparer<int>.Default);

            finalSystemPath = AlphabetizeScopedConflictFreePaths(
                autosortedSystem,
                0,
                conflictIndexes);
            finalUserPath = AlphabetizeScopedConflictFreePaths(
                autosortedUser,
                autosortedSystem.Count,
                conflictIndexes);

            finalConflicts = PathConflictMetrics.FromReport(
                PathConflictAnalyzer.BuildReport(
                    finalSystemPath.Concat(finalUserPath),
                    extensions,
                    finalSystemPath.Count));
        }

        private static IList<PathEntry> AlphabetizeScopedConflictFreePaths(
            IList<PathEntry> path,
            int globalIndexOffset,
            ISet<int> conflictIndexes)
        {
            var scopedPath = SafePath(path);
            var alphabetizedEntries = scopedPath
                .Where((entry, index) => !conflictIndexes.Contains(globalIndexOffset + index))
                .OrderBy(GetAlphabetizeKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (alphabetizedEntries.Count < 2)
            {
                return scopedPath;
            }

            var result = new List<PathEntry>(scopedPath.Count);
            var alphabetizedIndex = 0;
            for (var i = 0; i < scopedPath.Count; i++)
            {
                if (conflictIndexes.Contains(globalIndexOffset + i))
                {
                    result.Add(scopedPath[i]);
                    continue;
                }

                result.Add(alphabetizedEntries[alphabetizedIndex++]);
            }

            return result;
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

        private static string GetAlphabetizeKey(PathEntry path)
        {
            return path == null ? "" : path.SymbolicPath ?? "";
        }
    }
}
