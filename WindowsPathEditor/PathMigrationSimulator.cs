using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsPathEditor
{
    public enum PathScope
    {
        System = 0,
        User = 1
    }

    public enum PathOwnership
    {
        Unresolved = 0,
        User = 1,
        Machine = 2,
        Custom = 3
    }

    public enum PathMigrationSimulationMode
    {
        Conservative = 0,
        AggressiveAutosort = 1
    }

    public sealed class PathMigrationPolicy
    {
        public PathMigrationPolicy(
            IEnumerable<KeyValuePair<string, string>> normalizationVariables,
            IEnumerable<string> userOwnedRoots,
            IEnumerable<string> machineOwnedRoots)
        {
            NormalizationVariables = NormalizeVariables(normalizationVariables);
            UserOwnedRoots = NormalizeRoots(userOwnedRoots);
            MachineOwnedRoots = NormalizeRoots(machineOwnedRoots);
        }

        public IList<KeyValuePair<string, string>> NormalizationVariables { get; private set; }
        public IList<string> UserOwnedRoots { get; private set; }
        public IList<string> MachineOwnedRoots { get; private set; }

        public static KeyValuePair<string, string> Variable(string name, string value)
        {
            return new KeyValuePair<string, string>(name ?? "", value ?? "");
        }

        public static PathMigrationPolicy CreateDefault()
        {
            var variables = new List<KeyValuePair<string, string>>();
            AddVariable(variables, "LocalAppData", Environment.GetEnvironmentVariable("LocalAppData"));
            AddVariable(variables, "AppData", Environment.GetEnvironmentVariable("AppData"));
            AddVariable(variables, "UserProfile", Environment.GetEnvironmentVariable("UserProfile"));
            AddVariable(variables, "ProgramData", Environment.GetEnvironmentVariable("ProgramData"));
            AddVariable(variables, "ProgramW6432", Environment.GetEnvironmentVariable("ProgramW6432"));
            AddVariable(variables, "ProgramFiles(x86)", Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            AddVariable(variables, "ProgramFiles", Environment.GetEnvironmentVariable("ProgramFiles"));
            AddVariable(variables, "SystemRoot", Environment.GetEnvironmentVariable("SystemRoot"));
            AddVariable(variables, "windir", Environment.GetEnvironmentVariable("windir"));

            var userRoots = new List<string>();
            AddRoot(userRoots, Environment.GetEnvironmentVariable("UserProfile"));
            AddRoot(userRoots, Environment.GetEnvironmentVariable("LocalAppData"));
            AddRoot(userRoots, Environment.GetEnvironmentVariable("AppData"));

            var machineRoots = new List<string>();
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("ProgramW6432"));
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("ProgramFiles"));
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("ProgramData"));
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("SystemRoot"));
            AddRoot(machineRoots, Environment.GetEnvironmentVariable("windir"));

            return new PathMigrationPolicy(variables, userRoots, machineRoots);
        }

        internal static string NormalizeRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            var entry = new PathEntry(path);
            PathResolution resolution;
            if (entry.TryResolve(out resolution))
            {
                return resolution.ActualPath;
            }

            return path.TrimEnd('\\');
        }

        private static IList<KeyValuePair<string, string>> NormalizeVariables(IEnumerable<KeyValuePair<string, string>> variables)
        {
            return SafeVariables(variables)
                .Where(_ => !string.IsNullOrEmpty(_.Key) && !string.IsNullOrEmpty(_.Value))
                .Select(_ => new KeyValuePair<string, string>(_.Key, NormalizeRoot(_.Value)))
                .Distinct(new VariableComparer())
                .ToList();
        }

        private static IList<string> NormalizeRoots(IEnumerable<string> roots)
        {
            return SafeRoots(roots)
                .Where(_ => !string.IsNullOrEmpty(_))
                .Select(NormalizeRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(_ => _.Length)
                .ToList();
        }

        private static IEnumerable<KeyValuePair<string, string>> SafeVariables(IEnumerable<KeyValuePair<string, string>> variables)
        {
            return variables ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private static IEnumerable<string> SafeRoots(IEnumerable<string> roots)
        {
            return roots ?? Enumerable.Empty<string>();
        }

        private static void AddVariable(ICollection<KeyValuePair<string, string>> variables, string name, string value)
        {
            if (variables == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value)) return;
            variables.Add(Variable(name, value));
        }

        private static void AddRoot(ICollection<string> roots, string value)
        {
            if (roots == null || string.IsNullOrEmpty(value)) return;
            roots.Add(value);
        }

        private sealed class VariableComparer : IEqualityComparer<KeyValuePair<string, string>>
        {
            public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
            {
                return string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(KeyValuePair<string, string> obj)
            {
                unchecked
                {
                    return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key ?? "") * 397 ^
                        StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value ?? "");
                }
            }
        }
    }

    public sealed class PathMigrationSimulationEntry
    {
        public PathMigrationSimulationEntry(
            PathEntry originalPath,
            PathScope originalScope,
            PathEntry proposedPath,
            PathScope proposedScope,
            PathOwnership ownership,
            bool isResolved,
            string resolvedPath,
            bool isNormalized,
            bool isPromotedToSystem,
            bool isDemotedToUser,
            bool requiresManualReview,
            IEnumerable<string> notes)
        {
            OriginalPath = originalPath;
            OriginalScope = originalScope;
            ProposedPath = proposedPath;
            ProposedScope = proposedScope;
            Ownership = ownership;
            IsResolved = isResolved;
            ResolvedPath = resolvedPath ?? "";
            IsNormalized = isNormalized;
            IsPromotedToSystem = isPromotedToSystem;
            IsDemotedToUser = isDemotedToUser;
            RequiresManualReview = requiresManualReview;
            Notes = (notes ?? Enumerable.Empty<string>()).ToList();
        }

        public PathEntry OriginalPath { get; private set; }
        public PathScope OriginalScope { get; private set; }
        public PathEntry ProposedPath { get; private set; }
        public PathScope ProposedScope { get; private set; }
        public PathOwnership Ownership { get; private set; }
        public bool IsResolved { get; private set; }
        public string ResolvedPath { get; private set; }
        public bool IsNormalized { get; private set; }
        public bool IsPromotedToSystem { get; private set; }
        public bool IsDemotedToUser { get; private set; }
        public bool RequiresManualReview { get; private set; }
        public IList<string> Notes { get; private set; }

        public string ActionLabel
        {
            get
            {
                if (IsPromotedToSystem && IsNormalized) return "Normalize + Promote";
                if (IsPromotedToSystem) return "Promote";
                if (IsDemotedToUser) return "Demote";
                if (IsNormalized) return "Normalize";
                if (RequiresManualReview) return "Review";
                return "Keep";
            }
        }
    }

    public sealed class PathConflictMetrics
    {
        public PathConflictMetrics(
            int groupCount,
            int rowCount,
            int mixedScopeGroupCount,
            int mixedScopeRowCount,
            int shadowedByHigherVersionRowCount)
        {
            GroupCount = groupCount;
            RowCount = rowCount;
            MixedScopeGroupCount = mixedScopeGroupCount;
            MixedScopeRowCount = mixedScopeRowCount;
            ShadowedByHigherVersionRowCount = shadowedByHigherVersionRowCount;
        }

        public int GroupCount { get; private set; }
        public int RowCount { get; private set; }
        public int MixedScopeGroupCount { get; private set; }
        public int MixedScopeRowCount { get; private set; }
        public int ShadowedByHigherVersionRowCount { get; private set; }

        public static PathConflictMetrics FromReport(PathConflictReport report)
        {
            var safeReport = report ?? PathConflictReport.Empty;
            var mixedGroups = safeReport.Groups.Count(IsMixedScopeGroup);
            var mixedRows = safeReport.Groups.Where(IsMixedScopeGroup).Sum(_ => _.Rows.Count);
            var shadowedRows = safeReport.Groups.SelectMany(_ => _.Rows)
                .Count(_ => _.WinnerState == PathConflictWinnerState.ShadowedByHigherVersion);

            return new PathConflictMetrics(
                safeReport.Groups.Count,
                safeReport.Groups.Sum(_ => _.Rows.Count),
                mixedGroups,
                mixedRows,
                shadowedRows);
        }

        private static bool IsMixedScopeGroup(PathConflictGroup group)
        {
            if (group == null) return false;

            var hasSystem = group.Columns.Any(_ => _.Origin == PathConflictColumnOrigin.System || _.Origin == PathConflictColumnOrigin.Mixed);
            var hasUser = group.Columns.Any(_ => _.Origin == PathConflictColumnOrigin.User || _.Origin == PathConflictColumnOrigin.Mixed);
            return hasSystem && hasUser;
        }
    }

    public sealed class PathDuplicateGroup
    {
        public PathDuplicateGroup(PathEntry path, IEnumerable<PathMigrationSimulationEntry> entries)
        {
            Path = path;
            Entries = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>()).ToList();
        }

        public PathEntry Path { get; private set; }
        public IList<PathMigrationSimulationEntry> Entries { get; private set; }
    }

    public sealed class PathMigrationSimulationResult
    {
        public PathMigrationSimulationResult(
            IEnumerable<PathMigrationSimulationEntry> entries,
            IEnumerable<PathEntry> originalSystemPath,
            IEnumerable<PathEntry> originalUserPath,
            IEnumerable<PathEntry> simulatedSystemPath,
            IEnumerable<PathEntry> simulatedUserPath,
            IEnumerable<PathEntry> autosortedSystemPath,
            IEnumerable<PathEntry> autosortedUserPath,
            PathConflictMetrics beforeConflicts,
            PathConflictMetrics afterMigrationConflicts,
            PathConflictMetrics afterAutosortConflicts,
            IEnumerable<PathDuplicateGroup> duplicateGroupsAfterMigration,
            bool canWriteSystemPath)
        {
            Entries = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>()).ToList();
            OriginalSystemPath = SafePath(originalSystemPath);
            OriginalUserPath = SafePath(originalUserPath);
            SimulatedSystemPath = SafePath(simulatedSystemPath);
            SimulatedUserPath = SafePath(simulatedUserPath);
            AutosortedSystemPath = SafePath(autosortedSystemPath);
            AutosortedUserPath = SafePath(autosortedUserPath);
            BeforeConflicts = beforeConflicts ?? PathConflictMetrics.FromReport(PathConflictReport.Empty);
            AfterMigrationConflicts = afterMigrationConflicts ?? PathConflictMetrics.FromReport(PathConflictReport.Empty);
            AfterAutosortConflicts = afterAutosortConflicts ?? PathConflictMetrics.FromReport(PathConflictReport.Empty);
            DuplicateGroupsAfterMigration = (duplicateGroupsAfterMigration ?? Enumerable.Empty<PathDuplicateGroup>()).ToList();
            CanWriteSystemPath = canWriteSystemPath;
        }

        public IList<PathMigrationSimulationEntry> Entries { get; private set; }
        public IList<PathEntry> OriginalSystemPath { get; private set; }
        public IList<PathEntry> OriginalUserPath { get; private set; }
        public IList<PathEntry> SimulatedSystemPath { get; private set; }
        public IList<PathEntry> SimulatedUserPath { get; private set; }
        public IList<PathEntry> AutosortedSystemPath { get; private set; }
        public IList<PathEntry> AutosortedUserPath { get; private set; }
        public PathConflictMetrics BeforeConflicts { get; private set; }
        public PathConflictMetrics AfterMigrationConflicts { get; private set; }
        public PathConflictMetrics AfterAutosortConflicts { get; private set; }
        public IList<PathDuplicateGroup> DuplicateGroupsAfterMigration { get; private set; }
        public bool CanWriteSystemPath { get; private set; }

        public int NormalizationCount { get { return Entries.Count(_ => _.IsNormalized); } }
        public int PromotionCount { get { return Entries.Count(_ => _.IsPromotedToSystem); } }
        public int ManualReviewCount { get { return Entries.Count(_ => _.RequiresManualReview); } }
        public int UserOwnedSystemPathCount
        {
            get { return Entries.Count(_ => _.OriginalScope == PathScope.System && _.Ownership == PathOwnership.User); }
        }

        public bool WouldChangeSystemPath
        {
            get { return !OriginalSystemPath.SequenceEqual(SimulatedSystemPath, PathEntryComparers.SymbolicPath); }
        }

        public bool WouldChangeUserPath
        {
            get { return !OriginalUserPath.SequenceEqual(SimulatedUserPath, PathEntryComparers.SymbolicPath); }
        }

        public bool RequiresElevationToApply
        {
            get { return WouldChangeSystemPath && !CanWriteSystemPath; }
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }
    }

    public static class PathMigrationSimulator
    {
        public static PathMigrationSimulationResult SimulateCurrentMachine()
        {
            var registry = new PathRegistry();
            return Simulate(
                registry.SystemPath,
                registry.UserPath,
                registry.ExecutableExtensions,
                PathMigrationPolicy.CreateDefault(),
                registry.IsSystemPathWritable,
                PathMigrationSimulationMode.Conservative,
                null,
                null);
        }

        public static PathMigrationSimulationResult Simulate(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy)
        {
            return Simulate(systemPath, userPath, executableExtensions, policy, PathMigrationSimulationMode.Conservative);
        }

        public static PathMigrationSimulationResult Simulate(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy,
            PathMigrationSimulationMode mode)
        {
            return Simulate(systemPath, userPath, executableExtensions, policy, true, mode, null, null);
        }

        /// <summary>
        /// Simulates with optional sets of entries to exclude from promotion and System PATH
        /// entries to demote to User PATH.  <paramref name="promotionExclusions"/> prevents
        /// specific User PATH entries from being promoted; <paramref name="systemDemotions"/>
        /// moves specific System PATH entries down to User PATH so higher-version User PATH
        /// files can win their DLL conflicts.
        /// </summary>
        public static PathMigrationSimulationResult Simulate(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy,
            PathMigrationSimulationMode mode,
            IEnumerable<string> promotionExclusions,
            IEnumerable<string> systemDemotions)
        {
            return Simulate(systemPath, userPath, executableExtensions, policy, true, mode,
                promotionExclusions == null ? null
                    : new HashSet<string>(promotionExclusions, StringComparer.OrdinalIgnoreCase),
                systemDemotions == null ? null
                    : new HashSet<string>(systemDemotions, StringComparer.OrdinalIgnoreCase));
        }

        private static PathMigrationSimulationResult Simulate(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<string> executableExtensions,
            PathMigrationPolicy policy,
            bool canWriteSystemPath,
            PathMigrationSimulationMode mode,
            ISet<string> promotionExclusions,
            ISet<string> systemDemotions)
        {
            var safePolicy = policy ?? PathMigrationPolicy.CreateDefault();
            var originalSystemPath = SafePath(systemPath);
            var originalUserPath = SafePath(userPath);
            var extensions = SafeExtensions(executableExtensions);

            var entries = new List<PathMigrationSimulationEntry>();
            entries.AddRange(originalSystemPath.Select(_ => SimulateEntry(_, PathScope.System, safePolicy, mode, null, systemDemotions)));
            entries.AddRange(originalUserPath.Select(_ => SimulateEntry(_, PathScope.User, safePolicy, mode, promotionExclusions, null)));

            var simulatedSystemPath = BuildSimulatedSystemPath(entries);
            var simulatedUserPath = BuildSimulatedUserPath(entries, simulatedSystemPath);

            var beforeReport = PathConflictAnalyzer.BuildReport(
                originalSystemPath.Concat(originalUserPath),
                extensions,
                originalSystemPath.Count);
            var afterMigrationReport = PathConflictAnalyzer.BuildReport(
                simulatedSystemPath.Concat(simulatedUserPath),
                extensions,
                simulatedSystemPath.Count);

            IList<PathEntry> autosortedSystemPath;
            IList<PathEntry> autosortedUserPath;
            using (var checker = new PathChecker(extensions))
            {
                autosortedSystemPath = checker.SuggestBestOrder(simulatedSystemPath).ToList();
                autosortedUserPath = checker.SuggestBestOrder(simulatedUserPath).ToList();
            }

            var afterAutosortReport = PathConflictAnalyzer.BuildReport(
                autosortedSystemPath.Concat(autosortedUserPath),
                extensions,
                autosortedSystemPath.Count);

            return new PathMigrationSimulationResult(
                entries,
                originalSystemPath,
                originalUserPath,
                simulatedSystemPath,
                simulatedUserPath,
                autosortedSystemPath,
                autosortedUserPath,
                PathConflictMetrics.FromReport(beforeReport),
                PathConflictMetrics.FromReport(afterMigrationReport),
                PathConflictMetrics.FromReport(afterAutosortReport),
                FindDuplicateGroups(entries),
                canWriteSystemPath);
        }

        private static PathMigrationSimulationEntry SimulateEntry(
            PathEntry path,
            PathScope scope,
            PathMigrationPolicy policy,
            PathMigrationSimulationMode mode,
            ISet<string> promotionExclusions,
            ISet<string> systemDemotions)
        {
            var safePath = path ?? new PathEntry("");
            var notes = new List<string>();
            var proposedPath = safePath;
            var proposedScope = scope;
            var ownership = PathOwnership.Unresolved;
            var resolvedPath = "";
            var isResolved = false;
            var isPromoted = false;
            var isDemoted = false;
            var requiresManualReview = false;

            // An entry is excluded from promotion when a prior conflict-impact pass determined
            // that promoting it would shadow higher-version files in User PATH.
            var isExcludedFromPromotion = promotionExclusions != null &&
                promotionExclusions.Contains(safePath.SymbolicPath ?? "");

            // A System PATH entry is flagged for demotion when it has been identified as
            // shadowing higher-version files that reside in User PATH.
            var isDemotionCandidate = scope == PathScope.System &&
                systemDemotions != null &&
                systemDemotions.Contains(safePath.SymbolicPath ?? "");

            PathResolution resolution;
            if (safePath.TryResolve(out resolution))
            {
                isResolved = true;
                resolvedPath = resolution.ActualPath;
                ownership = ClassifyOwnership(resolvedPath, policy);
                proposedPath = NormalizePath(safePath, resolvedPath, policy);

                if (!PathEntryComparers.SymbolicPath.Equals(safePath, proposedPath))
                {
                    notes.Add("Would normalize to " + proposedPath.SymbolicPath + ".");
                }

                if (isDemotionCandidate)
                {
                    proposedScope = PathScope.User;
                    isDemoted = true;
                    notes.Add("System PATH entry demoted to User PATH because it shadows higher-version files in User PATH.");
                }
                else if (scope == PathScope.User && ownership == PathOwnership.Machine && !isExcludedFromPromotion)
                {
                    proposedScope = PathScope.System;
                    isPromoted = true;
                    notes.Add("Machine-owned path currently in User PATH; would promote to System PATH.");
                }
                else if (scope == PathScope.User && ownership == PathOwnership.Custom)
                {
                    if (mode == PathMigrationSimulationMode.AggressiveAutosort && !isExcludedFromPromotion)
                    {
                        proposedScope = PathScope.System;
                        isPromoted = true;
                        notes.Add("Resolved custom rooted path would be promoted to System PATH for aggressive autosort planning.");
                    }
                    else
                    {
                        requiresManualReview = true;
                        notes.Add("Custom rooted path outside the known user and machine roots; keep in User PATH unless reviewed manually.");
                    }
                }
                else if (scope == PathScope.System && ownership == PathOwnership.User)
                {
                    requiresManualReview = true;
                    notes.Add("User-owned directory is already in System PATH; changing it automatically could alter per-user tooling unexpectedly.");
                }
            }
            else
            {
                requiresManualReview = true;
                notes.Add("Path could not be resolved; left unchanged.");
            }

            return new PathMigrationSimulationEntry(
                safePath,
                scope,
                proposedPath,
                proposedScope,
                ownership,
                isResolved,
                resolvedPath,
                !PathEntryComparers.SymbolicPath.Equals(safePath, proposedPath),
                isPromoted,
                isDemoted,
                requiresManualReview,
                notes);
        }

        private static PathEntry NormalizePath(PathEntry originalPath, string actualPath, PathMigrationPolicy policy)
        {
            if (string.IsNullOrEmpty(actualPath))
            {
                return originalPath;
            }

            var normalized = PathEntry.FromFilePath(actualPath, policy.NormalizationVariables);
            return PathEntryComparers.SymbolicPath.Equals(originalPath, normalized)
                ? originalPath
                : normalized;
        }

        private static PathOwnership ClassifyOwnership(string actualPath, PathMigrationPolicy policy)
        {
            if (string.IsNullOrEmpty(actualPath))
            {
                return PathOwnership.Unresolved;
            }

            if (StartsWithRoot(actualPath, policy.UserOwnedRoots))
            {
                return PathOwnership.User;
            }

            if (StartsWithRoot(actualPath, policy.MachineOwnedRoots))
            {
                return PathOwnership.Machine;
            }

            return PathOwnership.Custom;
        }

        private static bool StartsWithRoot(string actualPath, IEnumerable<string> roots)
        {
            var normalizedPath = PathMigrationPolicy.NormalizeRoot(actualPath);
            foreach (var root in roots ?? Enumerable.Empty<string>())
            {
                if (IsPathUnderRoot(normalizedPath, root))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPathUnderRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
            {
                return false;
            }

            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        // Original system entries keep their existing order (demoted entries are excluded).
        // Promoted user entries are sorted alphabetically and appended after them.
        // Any path that already appears earlier in the list is silently dropped,
        // so a path that lived in both scopes never ends up duplicated in system.
        private static IList<PathEntry> BuildSimulatedSystemPath(IEnumerable<PathMigrationSimulationEntry> entries)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<PathEntry>();

            var originalSystem = entries.Where(_ => _.OriginalScope == PathScope.System && !_.IsDemotedToUser);
            var promoted = entries
                .Where(_ => _.IsPromotedToSystem)
                .OrderBy(_ => _.ProposedPath != null ? _.ProposedPath.SymbolicPath ?? "" : "",
                    StringComparer.OrdinalIgnoreCase);

            foreach (var entry in originalSystem.Concat(promoted))
            {
                var key = entry.ProposedPath != null ? entry.ProposedPath.SymbolicPath ?? "" : "";
                if (seen.Add(key))
                {
                    result.Add(entry.ProposedPath);
                }
            }

            return result;
        }

        // User entries that ended up in the simulated system path are dropped here
        // so they do not appear in both scopes after the migration.
        // Demoted System PATH entries are appended at the end of User PATH so that
        // existing User PATH entries (which have the higher-version files) take precedence.
        private static IList<PathEntry> BuildSimulatedUserPath(
            IEnumerable<PathMigrationSimulationEntry> entries,
            IEnumerable<PathEntry> simulatedSystemPath)
        {
            var systemKeys = new HashSet<string>(
                (simulatedSystemPath ?? Enumerable.Empty<PathEntry>()).Select(_ => _.SymbolicPath ?? ""),
                StringComparer.OrdinalIgnoreCase);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<PathEntry>();

            var entryList = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>()).ToList();
            var userEntries = entryList.Where(_ => _.ProposedScope == PathScope.User && !_.IsDemotedToUser);
            var demotedEntries = entryList.Where(_ => _.IsDemotedToUser);

            foreach (var entry in userEntries.Concat(demotedEntries))
            {
                var key = entry.ProposedPath != null ? entry.ProposedPath.SymbolicPath ?? "" : "";
                if (!systemKeys.Contains(key) && seen.Add(key))
                {
                    result.Add(entry.ProposedPath);
                }
            }

            return result;
        }

        private static IList<PathDuplicateGroup> FindDuplicateGroups(IEnumerable<PathMigrationSimulationEntry> entries)
        {
            return (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>())
                .GroupBy(_ => _.ProposedPath, PathEntryComparers.SymbolicPath)
                .Where(_ => _.Count() > 1)
                .Select(_ => new PathDuplicateGroup(_.First().ProposedPath, _.ToList()))
                .OrderBy(_ => _.Path.SymbolicPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }

        private static IList<string> SafeExtensions(IEnumerable<string> executableExtensions)
        {
            return (executableExtensions ?? Enumerable.Empty<string>())
                .Where(_ => !string.IsNullOrEmpty(_))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static class PathMigrationReportBuilder
    {
        public static string BuildMarkdownReport(PathMigrationSimulationResult result)
        {
            var safeResult = result ?? PathMigrationSimulator.SimulateCurrentMachine();
            var builder = new StringBuilder();

            builder.AppendLine("# PATH Migration Simulation Report");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine("- Normalizations proposed: " + safeResult.NormalizationCount);
            builder.AppendLine("- User to System promotions proposed: " + safeResult.PromotionCount);
            builder.AppendLine("- Manual review entries: " + safeResult.ManualReviewCount);
            builder.AppendLine("- Duplicate locations after naive migration: " + safeResult.DuplicateGroupsAfterMigration.Count);
            builder.AppendLine("- System PATH write access available: " + (safeResult.CanWriteSystemPath ? "yes" : "no"));
            builder.AppendLine("- Elevation required to apply: " + (safeResult.RequiresElevationToApply ? "yes" : "no"));
            builder.AppendLine();
            builder.AppendLine("## Conflict Metrics");
            builder.AppendLine();
            builder.AppendLine("| Stage | Groups | Rows | Mixed-scope groups | Mixed-scope rows | Shadowed-by-higher-version rows |");
            builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
            AppendConflictRow(builder, "Current", safeResult.BeforeConflicts);
            AppendConflictRow(builder, "After migration", safeResult.AfterMigrationConflicts);
            AppendConflictRow(builder, "After migration + autosort", safeResult.AfterAutosortConflicts);
            builder.AppendLine();

            AppendEntrySection(builder, "Promotions", safeResult.Entries.Where(_ => _.IsPromotedToSystem));
            AppendEntrySection(builder, "Normalizations", safeResult.Entries.Where(_ => _.IsNormalized && !_.IsPromotedToSystem));
            AppendEntrySection(builder, "Manual Review", safeResult.Entries.Where(_ => _.RequiresManualReview));

            builder.AppendLine("## Side Effects");
            builder.AppendLine();
            foreach (var note in BuildVerdictNotes(safeResult))
            {
                builder.AppendLine("- " + note);
            }
            builder.AppendLine();
            builder.AppendLine("## Verdict");
            builder.AppendLine();
            builder.AppendLine(BuildVerdictSummary(safeResult));

            return builder.ToString();
        }

        private static void AppendConflictRow(StringBuilder builder, string stage, PathConflictMetrics metrics)
        {
            builder.AppendLine(string.Format(
                "| {0} | {1} | {2} | {3} | {4} | {5} |",
                stage,
                metrics.GroupCount,
                metrics.RowCount,
                metrics.MixedScopeGroupCount,
                metrics.MixedScopeRowCount,
                metrics.ShadowedByHigherVersionRowCount));
        }

        private static void AppendEntrySection(StringBuilder builder, string title, IEnumerable<PathMigrationSimulationEntry> entries)
        {
            var list = (entries ?? Enumerable.Empty<PathMigrationSimulationEntry>()).ToList();

            builder.AppendLine("## " + title);
            builder.AppendLine();
            if (list.Count == 0)
            {
                builder.AppendLine("- None.");
                builder.AppendLine();
                return;
            }

            foreach (var entry in list)
            {
                builder.AppendLine(string.Format(
                    "- [{0}] {1} -> {2} ({3} -> {4})",
                    entry.ActionLabel,
                    entry.OriginalPath.SymbolicPath,
                    entry.ProposedPath.SymbolicPath,
                    entry.OriginalScope,
                    entry.ProposedScope));
                foreach (var note in entry.Notes)
                {
                    builder.AppendLine("  - " + note);
                }
            }

            builder.AppendLine();
        }

        private static IEnumerable<string> BuildVerdictNotes(PathMigrationSimulationResult result)
        {
            var notes = new List<string>();

            if (result.BeforeConflicts.MixedScopeGroupCount > result.AfterMigrationConflicts.MixedScopeGroupCount)
            {
                notes.Add(string.Format(
                    "The migration reduces mixed-scope conflict groups from {0} to {1}.",
                    result.BeforeConflicts.MixedScopeGroupCount,
                    result.AfterMigrationConflicts.MixedScopeGroupCount));
            }
            else
            {
                notes.Add("The migration does not reduce mixed-scope conflict groups on this machine.");
            }

            if (result.AfterMigrationConflicts.ShadowedByHigherVersionRowCount > result.AfterAutosortConflicts.ShadowedByHigherVersionRowCount)
            {
                notes.Add(string.Format(
                    "Autosort improves DLL winner quality after migration ({0} down to {1} shadowed rows).",
                    result.AfterMigrationConflicts.ShadowedByHigherVersionRowCount,
                    result.AfterAutosortConflicts.ShadowedByHigherVersionRowCount));
            }
            else
            {
                notes.Add("Autosort does not materially improve the remaining higher-version DLL conflicts after migration.");
            }

            if (result.DuplicateGroupsAfterMigration.Count > 0)
            {
                notes.Add(string.Format(
                    "A naive apply would leave {0} duplicate path location group(s); dedupe or Clean Up should be part of any real migration.",
                    result.DuplicateGroupsAfterMigration.Count));
            }

            if (result.UserOwnedSystemPathCount > 0)
            {
                notes.Add(string.Format(
                    "There are already {0} user-owned path(s) in System PATH, which should be reviewed before any automated scope changes.",
                    result.UserOwnedSystemPathCount));
            }

            if (result.ManualReviewCount > 0)
            {
                notes.Add(string.Format(
                    "{0} path(s) remain ambiguous or suspicious and should not be moved automatically.",
                    result.ManualReviewCount));
            }

            if (result.RequiresElevationToApply)
            {
                notes.Add("Applying the proposed System PATH changes would require elevation on this machine.");
            }

            if (notes.Count == 0)
            {
                notes.Add("No obvious side effects were detected in the dry run.");
            }

            return notes;
        }

        private static string BuildVerdictSummary(PathMigrationSimulationResult result)
        {
            if (result.ManualReviewCount == 0 &&
                result.DuplicateGroupsAfterMigration.Count == 0 &&
                result.AfterMigrationConflicts.MixedScopeGroupCount < result.BeforeConflicts.MixedScopeGroupCount)
            {
                return "The policy looks viable on this machine. Normalizing known variables and promoting clearly machine-owned User PATH entries should improve search-order semantics with low risk.";
            }

            return "The policy is promising, but it is not safe to apply blindly. The dry run found entries that still need manual review and/or cleanup before a real migration should be attempted.";
        }
    }
}
