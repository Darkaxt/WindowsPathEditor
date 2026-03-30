using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public enum PathCleanupRemovalKind
    {
        MissingResolvedPath = 0,
        DuplicateResolvedPath = 1
    }

    public sealed class PathCleanupRemovedEntry
    {
        public PathCleanupRemovedEntry(PathEntry path, PathScope scope, PathCleanupRemovalKind kind)
        {
            Path = path;
            Scope = scope;
            Kind = kind;
        }

        public PathEntry Path { get; private set; }
        public PathScope Scope { get; private set; }
        public PathCleanupRemovalKind Kind { get; private set; }

        public string Reason
        {
            get
            {
                return Kind == PathCleanupRemovalKind.MissingResolvedPath
                    ? "Path does not exist"
                    : "Duplicate of earlier resolved path";
            }
        }
    }

    public sealed class PathCleanupResult
    {
        public PathCleanupResult(
            IEnumerable<PathEntry> systemPath,
            IEnumerable<PathEntry> userPath,
            IEnumerable<PathCleanupRemovedEntry> removedEntries)
        {
            SystemPath = systemPath.ToList();
            UserPath = userPath.ToList();
            RemovedEntries = (removedEntries ?? Enumerable.Empty<PathCleanupRemovedEntry>()).ToList();
        }

        public IList<PathEntry> SystemPath { get; private set; }
        public IList<PathEntry> UserPath { get; private set; }
        public IList<PathCleanupRemovedEntry> RemovedEntries { get; private set; }
    }

    public static class PathCleanup
    {
        public static PathCleanupResult Clean(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            var seenResolvedPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var removedEntries = new List<PathCleanupRemovedEntry>();
            var cleanedSystem = CleanPathList(systemPath, PathScope.System, seenResolvedPaths, removedEntries);
            var cleanedUser = CleanPathList(userPath, PathScope.User, seenResolvedPaths, removedEntries);
            return new PathCleanupResult(cleanedSystem, cleanedUser, removedEntries);
        }

        private static IEnumerable<PathEntry> CleanPathList(
            IEnumerable<PathEntry> pathList,
            PathScope scope,
            ISet<string> seenResolvedPaths,
            IList<PathCleanupRemovedEntry> removedEntries)
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
                    removedEntries.Add(new PathCleanupRemovedEntry(path, scope, PathCleanupRemovalKind.MissingResolvedPath));
                    continue;
                }

                if (!seenResolvedPaths.Add(resolution.ActualPath))
                {
                    removedEntries.Add(new PathCleanupRemovedEntry(path, scope, PathCleanupRemovalKind.DuplicateResolvedPath));
                    continue;
                }

                yield return path;
            }
        }
    }
}
