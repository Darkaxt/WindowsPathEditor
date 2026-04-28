using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public static class ScanImportPlanner
    {
        public static IEnumerable<PathEntry> SelectPathsToImport(IEnumerable<SelectablePath> candidates, IEnumerable<PathEntry> currentPath)
        {
            var seenPaths = new HashSet<PathEntry>(currentPath);

            foreach (var candidate in candidates.Where(_ => _.IsSelected))
            {
                var entry = PathEntry.FromFilePath(candidate.Path);
                if (seenPaths.Add(entry))
                {
                    yield return entry;
                }
            }
        }
    }
}
