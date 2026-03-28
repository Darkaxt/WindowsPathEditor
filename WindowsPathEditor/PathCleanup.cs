using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public sealed class PathCleanupResult
    {
        public PathCleanupResult(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            SystemPath = systemPath.ToList();
            UserPath = userPath.ToList();
        }

        public IList<PathEntry> SystemPath { get; private set; }
        public IList<PathEntry> UserPath { get; private set; }
    }

    public static class PathCleanup
    {
        public static PathCleanupResult Clean(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            var seenResolvedPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var cleanedSystem = CleanPathList(systemPath, seenResolvedPaths);
            var cleanedUser = CleanPathList(userPath, seenResolvedPaths);
            return new PathCleanupResult(cleanedSystem, cleanedUser);
        }

        private static IEnumerable<PathEntry> CleanPathList(IEnumerable<PathEntry> pathList, ISet<string> seenResolvedPaths)
        {
            foreach (var path in pathList)
            {
                PathResolution resolution;
                if (!path.TryResolve(out resolution))
                {
                    yield return path;
                    continue;
                }

                if (!path.Exists)
                {
                    continue;
                }

                if (!seenResolvedPaths.Add(resolution.ActualPath))
                {
                    continue;
                }

                yield return path;
            }
        }
    }
}
