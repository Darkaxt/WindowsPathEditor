using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsPathEditor
{
    internal static class CliTextFormatter
    {
        public static string FormatPaths(CliPathsPayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine();
            builder.AppendLine("Raw registry");
            builder.AppendLine("  System PATH: " + FormatScalar(payload.RawRegistry == null ? null : payload.RawRegistry.SystemPath));
            builder.AppendLine("  User PATH: " + FormatScalar(payload.RawRegistry == null ? null : payload.RawRegistry.UserPath));
            builder.AppendLine();
            builder.AppendLine("Parsed registry");
            AppendSection(builder, "System PATH", payload.Parsed.SystemPath);
            AppendSection(builder, "User PATH", payload.Parsed.UserPath);
            AppendSection(builder, "Merged registry PATH", payload.Parsed.EffectivePath);
            builder.AppendLine();
            builder.AppendLine("Process PATH");
            builder.AppendLine("  Raw: " + FormatScalar(payload.Process.Raw));
            AppendSection(builder, "Entries", payload.Process.Entries, "  ");
            builder.AppendLine();
            builder.AppendLine("Mismatches");
            AppendSection(builder, "Process-only entries", payload.Mismatches.ProcessOnlyEntries);
            AppendSection(builder, "Registry-only entries", payload.Mismatches.RegistryOnlyEntries);
            AppendSection(builder, "Cross-scope duplicates", payload.Mismatches.CrossScopeDuplicates);

            return builder.ToString();
        }

        public static string FormatCleanup(CliCleanupPayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine();
            builder.AppendLine("Before cleanup");
            AppendPathState(builder, payload.Before);
            builder.AppendLine();
            builder.AppendLine("After cleanup");
            AppendPathState(builder, payload.After);
            builder.AppendLine();
            builder.AppendLine("Removed entries");
            AppendRemovedEntries(builder, payload.Removed);
            builder.AppendLine();
            builder.AppendLine("Preserved unresolved entries");
            AppendRemovedEntries(builder, payload.PreservedUnresolved);

            return builder.ToString();
        }

        public static string FormatConflicts(CliConflictsPayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine("Executable extensions: " + FormatScalar(string.Join(", ", payload.Extensions ?? Enumerable.Empty<string>())));
            builder.AppendLine();
            builder.AppendLine("Conflict metrics");
            AppendConflictMetrics(builder, payload.Metrics);
            builder.AppendLine();
            builder.AppendLine("Conflict groups");

            if (payload.Groups == null || payload.Groups.Count == 0)
            {
                builder.AppendLine("  <none>");
                return builder.ToString();
            }

            AppendConflictGroups(builder, payload.Groups);

            return builder.ToString();
        }

        public static string FormatAutosort(CliAutosortPayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine("Note: after-metrics require a second analyzer pass on the suggested order.");
            builder.AppendLine();
            builder.AppendLine("Before autosort");
            AppendAnalyzedPathState(builder, payload.Before);
            builder.AppendLine();
            builder.AppendLine("After migration");
            AppendAnalyzedPathState(builder, payload.AfterMigration);
            builder.AppendLine();
            builder.AppendLine("After autosort");
            AppendAnalyzedPathState(builder, payload.After);
            builder.AppendLine();
            builder.AppendLine("Promotions");
            AppendAutosortPromotions(builder, payload.Promotions);
            builder.AppendLine();
            builder.AppendLine("Normalizations");
            AppendAutosortNormalizations(builder, payload.Normalizations);
            builder.AppendLine();
            builder.AppendLine("Warnings");
            AppendAutosortWarnings(builder, payload.Warnings);
            builder.AppendLine();
            builder.AppendLine("Moved entries");
            AppendMovedEntries(builder, payload.MovedEntries);

            return builder.ToString();
        }

        public static string FormatMigrate(CliMigratePayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine();
            builder.AppendLine("Summary");
            AppendMigrationSummary(builder, payload.Summary);
            builder.AppendLine();
            builder.AppendLine("Before migration");
            AppendAnalyzedPathState(builder, payload.Before);
            builder.AppendLine();
            builder.AppendLine("After migration");
            AppendAnalyzedPathState(builder, payload.AfterMigration);
            builder.AppendLine();
            builder.AppendLine("After migration + autosort");
            AppendAnalyzedPathState(builder, payload.AfterAutosort);
            builder.AppendLine();
            builder.AppendLine("Entries");
            AppendMigrationEntries(builder, payload.Entries);
            builder.AppendLine();
            builder.AppendLine("Duplicate groups after migration");
            AppendDuplicateGroups(builder, payload.DuplicateGroups);

            return builder.ToString();
        }

        public static string FormatScan(CliScanPayload payload)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Source: " + payload.Source);
            builder.AppendLine("Root: " + payload.Root);
            builder.AppendLine("Depth: " + payload.Depth);
            builder.AppendLine();
            builder.AppendLine("Current PATH");
            AppendSection(builder, "Entries", payload.CurrentPath, "  ");
            builder.AppendLine();
            builder.AppendLine("Results");
            AppendScanResults(builder, payload.Results);

            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string label, IEnumerable<string> values, string indent = "  ")
        {
            builder.AppendLine(indent + label + ":");
            var items = (values ?? Enumerable.Empty<string>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine(indent + "  <none>");
                return;
            }

            foreach (var value in items)
            {
                builder.AppendLine(indent + "  - " + value);
            }
        }

        private static string FormatScalar(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        private static void AppendPathState(StringBuilder builder, CliParsedPathPayload payload)
        {
            if (payload == null)
            {
                builder.AppendLine("  <none>");
                return;
            }

            AppendSection(builder, "System PATH", payload.SystemPath);
            AppendSection(builder, "User PATH", payload.UserPath);
            AppendSection(builder, "Effective PATH", payload.EffectivePath);
        }

        private static void AppendAnalyzedPathState(StringBuilder builder, CliAnalyzedPathPayload payload)
        {
            if (payload == null)
            {
                builder.AppendLine("  <none>");
                return;
            }

            AppendPathState(builder, payload);
            builder.AppendLine("  Conflict metrics:");
            AppendConflictMetrics(builder, payload.Conflicts, "    ");
        }

        private static void AppendRemovedEntries(StringBuilder builder, IEnumerable<CliCleanupRemovedEntryPayload> removed)
        {
            var items = (removed ?? Enumerable.Empty<CliCleanupRemovedEntryPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format("  - [{0}] {1} ({2})", item.Scope, item.Path, item.Reason));
            }
        }

        private static void AppendMovedEntries(StringBuilder builder, IEnumerable<CliAutosortMovedEntryPayload> movedEntries)
        {
            var items = (movedEntries ?? Enumerable.Empty<CliAutosortMovedEntryPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format("  - [{0}] {1} {2} -> {3}", item.Scope, item.Path, item.FromIndex, item.ToIndex));
            }
        }

        private static void AppendAutosortPromotions(StringBuilder builder, IEnumerable<CliAutosortPromotionPayload> promotions)
        {
            var items = (promotions ?? Enumerable.Empty<CliAutosortPromotionPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format(
                    "  - [{0}] {1} -> {2} ({3} -> {4})",
                    item.Ownership,
                    item.OriginalPath,
                    item.Path,
                    item.OriginalScope,
                    item.ProposedScope));
            }
        }

        private static void AppendAutosortNormalizations(StringBuilder builder, IEnumerable<CliAutosortNormalizationPayload> normalizations)
        {
            var items = (normalizations ?? Enumerable.Empty<CliAutosortNormalizationPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format(
                    "  - [{0}] {1} -> {2}",
                    item.Scope,
                    item.OriginalPath,
                    item.Path));
            }
        }

        private static void AppendAutosortWarnings(StringBuilder builder, IEnumerable<CliAutosortWarningPayload> warnings)
        {
            var items = (warnings ?? Enumerable.Empty<CliAutosortWarningPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format(
                    "  - [{0}] {1}: {2}",
                    item.Kind,
                    item.Path,
                    item.Message));
            }
        }

        private static void AppendConflictMetrics(StringBuilder builder, CliConflictMetricsPayload metrics, string indent = "  ")
        {
            if (metrics == null)
            {
                builder.AppendLine(indent + "<none>");
                return;
            }

            builder.AppendLine(indent + "Group count: " + metrics.GroupCount);
            builder.AppendLine(indent + "Row count: " + metrics.RowCount);
            builder.AppendLine(indent + "Mixed-scope groups: " + metrics.MixedScopeGroupCount);
            builder.AppendLine(indent + "Mixed-scope rows: " + metrics.MixedScopeRowCount);
            builder.AppendLine(indent + "Shadowed-by-higher-version rows: " + metrics.ShadowedByHigherVersionRowCount);
        }

        private static void AppendMigrationSummary(StringBuilder builder, CliMigrationSummaryPayload summary)
        {
            if (summary == null)
            {
                builder.AppendLine("  <none>");
                return;
            }

            builder.AppendLine("  Normalizations proposed: " + summary.NormalizationCount);
            builder.AppendLine("  User to System promotions proposed: " + summary.PromotionCount);
            builder.AppendLine("  Manual review entries: " + summary.ManualReviewCount);
            builder.AppendLine("  Duplicate locations after naive migration: " + summary.DuplicateGroupCount);
            builder.AppendLine("  System PATH write access available: " + (summary.CanWriteSystemPath ? "yes" : "no"));
            builder.AppendLine("  Elevation required to apply: " + (summary.RequiresElevationToApply ? "yes" : "no"));
        }

        private static void AppendMigrationEntries(StringBuilder builder, IEnumerable<CliMigrationEntryPayload> entries)
        {
            var items = (entries ?? Enumerable.Empty<CliMigrationEntryPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format(
                    "  - [{0}] {1} -> {2} ({3} -> {4})",
                    item.Action,
                    item.OriginalPath,
                    item.ProposedPath,
                    item.OriginalScope,
                    item.ProposedScope));
                foreach (var note in item.Notes ?? Enumerable.Empty<string>())
                {
                    builder.AppendLine("    - " + note);
                }
            }
        }

        private static void AppendDuplicateGroups(StringBuilder builder, IEnumerable<CliMigrationDuplicateGroupPayload> duplicateGroups)
        {
            var items = (duplicateGroups ?? Enumerable.Empty<CliMigrationDuplicateGroupPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var group in items)
            {
                builder.AppendLine("  - " + FormatScalar(group.Path));
                foreach (var entry in group.Entries ?? Enumerable.Empty<CliMigrationDuplicateEntryPayload>())
                {
                    builder.AppendLine(string.Format(
                        "    - [{0}] {1} -> {2}",
                        entry.Scope,
                        entry.OriginalPath,
                        entry.ProposedPath));
                }
            }
        }

        private static void AppendConflictGroups(StringBuilder builder, IEnumerable<CliConflictGroupPayload> groups)
        {
            var safeGroups = (groups ?? Enumerable.Empty<CliConflictGroupPayload>()).ToList();
            if (safeGroups.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            for (var i = 0; i < safeGroups.Count; i++)
            {
                var group = safeGroups[i];
                builder.AppendLine(string.Format("  Group {0}: {1}", i + 1, group.Title));
                builder.AppendLine("    Participants: " + FormatScalar(group.ParticipantSummary));
                AppendColumns(builder, group.Columns);
                AppendConflictRows(builder, group.Rows);
            }
        }

        private static void AppendColumns(StringBuilder builder, IEnumerable<CliConflictColumnPayload> columns)
        {
            builder.AppendLine("    Columns:");
            var safeColumns = (columns ?? Enumerable.Empty<CliConflictColumnPayload>()).ToList();
            if (safeColumns.Count == 0)
            {
                builder.AppendLine("      <none>");
                return;
            }

            foreach (var column in safeColumns)
            {
                builder.AppendLine(string.Format("      - [{0}] {1}", column.Origin, column.Path));
            }
        }

        private static void AppendConflictRows(StringBuilder builder, IEnumerable<CliConflictRowPayload> rows)
        {
            builder.AppendLine("    Rows:");
            var safeRows = (rows ?? Enumerable.Empty<CliConflictRowPayload>()).ToList();
            if (safeRows.Count == 0)
            {
                builder.AppendLine("      <none>");
                return;
            }

            foreach (var row in safeRows)
            {
                builder.AppendLine(string.Format("      - {0} [{1}]", row.Filename, row.WinnerState));
                builder.AppendLine("        " + FormatScalar(row.WinnerSummary));
                foreach (var cell in row.Cells ?? Enumerable.Empty<CliConflictCellPayload>())
                {
                    builder.AppendLine(string.Format(
                        "        - {0} (runtime={1}, highest={2})",
                        FormatScalar(cell.DisplayValue),
                        cell.IsRuntimeWinner ? "yes" : "no",
                        cell.IsHighestVersion ? "yes" : "no"));
                }
            }
        }

        private static void AppendScanResults(StringBuilder builder, IEnumerable<CliScanResultPayload> results)
        {
            var items = (results ?? Enumerable.Empty<CliScanResultPayload>()).ToList();
            if (items.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            foreach (var item in items)
            {
                builder.AppendLine(string.Format("  - [{0}] {1}", item.WouldAdd ? "add" : "skip", item.Path));
            }
        }
    }
}
