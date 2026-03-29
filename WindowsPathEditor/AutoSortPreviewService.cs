using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public sealed class AutoSortApplyResult
    {
        public AutoSortApplyResult(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath, bool applied)
        {
            SystemPath = (systemPath ?? Enumerable.Empty<PathEntry>()).ToList();
            UserPath = (userPath ?? Enumerable.Empty<PathEntry>()).ToList();
            Applied = applied;
        }

        public IList<PathEntry> SystemPath { get; private set; }
        public IList<PathEntry> UserPath { get; private set; }
        public bool Applied { get; private set; }
    }

    public static class AutoSortPreviewService
    {
        public static AutoSortApplyResult ApplyIfConfirmed(AutoSortPlan plan, System.Func<AutoSortPlan, bool> confirm)
        {
            var safePlan = plan ?? new AutoSortPlan(null, null, null, null, null, null, null, null);

            // Always call confirm so the preview window is shown even for warnings-only plans
            // (HasChanges=false). The window's Apply button is disabled in that case, so the
            // user can review the warnings and dismiss via Cancel without applying anything.
            if (confirm == null || !confirm(safePlan))
            {
                return new AutoSortApplyResult(safePlan.Before.SystemPath, safePlan.Before.UserPath, false);
            }

            return new AutoSortApplyResult(safePlan.AfterAutosort.SystemPath, safePlan.AfterAutosort.UserPath, true);
        }
    }
}
