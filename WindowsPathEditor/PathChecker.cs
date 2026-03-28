using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;

namespace WindowsPathEditor
{
    public class PathChecker : IDisposable
    {
        private sealed class PathCheckRequest
        {
            public PathCheckRequest(int id, IEnumerable<AnnotatedPathEntry> entries, int systemPathCount)
            {
                Id = id;
                Entries = entries.ToList();
                SystemPathCount = systemPathCount;
            }

            public int Id { get; private set; }
            public IList<AnnotatedPathEntry> Entries { get; private set; }
            public int SystemPathCount { get; private set; }
        }

        /// <summary>
        /// The single thread that will be used to schedule all disk lookups
        /// </summary>
        private Thread thread;

        /// <summary>
        /// The queue used to communicate with the background thread
        /// </summary>
        private BlockingCollection<PathCheckRequest> pathsToProcess = new BlockingCollection<PathCheckRequest>(new ConcurrentQueue<PathCheckRequest>());

        /// <summary>
        /// Cache for the listFiles operation
        /// </summary>
        private ConcurrentDictionary<string, IEnumerable<string>> fileCache = new ConcurrentDictionary<string, IEnumerable<string>>();
        private ConcurrentDictionary<string, Version> versionCache = new ConcurrentDictionary<string, Version>();

        /// <summary>
        /// The currently applicable path
        /// </summary>
        private IEnumerable<PathEntry> currentPath = Enumerable.Empty<PathEntry>();

        /// <summary>
        /// Extensions to check for conflicts
        /// </summary>
        private readonly IEnumerable<string> extensions;

        private bool running = true;
        private bool abortCurrentCheck = false;
        private volatile PathConflictReport latestConflictReport = PathConflictReport.Pending;
        private int latestRequestedCheckId = 0;
        private int latestCompletedCheckId = 0;

        public PathChecker(IEnumerable<string> extensions)
        {
            this.extensions = extensions.Concat(new[] { ".dll" }).Select(_ => _.ToLower());
            thread = new Thread(CheckerLoop);
            thread.Start();
        }

        /// <summary>
        /// Check all paths in the given set
        /// </summary>
        public void Check(IEnumerable<AnnotatedPathEntry> paths, int systemPathCount)
        {
            var snapshot = paths.ToList();
            currentPath = snapshot.Select(_ => _.Path).ToList();
            snapshot.Each(_ => _.BeginValidation());
            abortCurrentCheck = true;
            latestConflictReport = PathConflictReport.Pending;

            var requestId = Interlocked.Increment(ref latestRequestedCheckId);
            pathsToProcess.Add(new PathCheckRequest(requestId, snapshot, systemPathCount));
        }

        public PathConflictReport LatestConflictReport
        {
            get { return latestConflictReport; }
        }

        public bool IsCheckPending
        {
            get { return Interlocked.CompareExchange(ref latestCompletedCheckId, 0, 0) < Interlocked.CompareExchange(ref latestRequestedCheckId, 0, 0); }
        }

        /// <summary>
        /// Suggest a better order for the given path list, preferring directories that
        /// win DLL version conflicts.
        /// </summary>
        public IEnumerable<PathEntry> SuggestBestOrder(IEnumerable<PathEntry> orderedPath)
        {
            var pathList = orderedPath.ToList();
            if (pathList.Count <= 1) return pathList;

            var scores = Enumerable.Repeat(0, pathList.Count).ToArray();
            var owners = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < pathList.Count; i++)
            {
                var path = pathList[i];
                if (!path.Exists)
                {
                    scores[i] -= 1000;
                    continue;
                }

                foreach (var dll in ListFiles(path)
                    .Where(file => string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!owners.ContainsKey(dll))
                    {
                        owners[dll] = new List<int>();
                    }

                    owners[dll].Add(i);
                }
            }

            foreach (var owner in owners.Where(_ => _.Value.Count > 1))
            {
                var dll = owner.Key;
                var candidateData = owner.Value
                    .Select(index => new
                    {
                        Index = index,
                        Version = GetFileVersion(pathList[index], dll)
                    })
                    .ToList();

                var maxVersion = candidateData.Max(_ => _.Version);
                var preferred = candidateData
                    .Where(_ => _.Version == maxVersion)
                    .OrderBy(_ => _.Index)
                    .First();
                var runtimeWinner = candidateData.OrderBy(_ => _.Index).First();

                if (runtimeWinner.Index == preferred.Index)
                {
                    scores[runtimeWinner.Index] += 1;
                }
                else
                {
                    scores[runtimeWinner.Index] -= 2;
                    scores[preferred.Index] += 2;
                }
            }

            return pathList.Select((path, index) => new { Path = path, Index = index })
                .OrderByDescending(_ => scores[_.Index])
                .ThenBy(_ => _.Index)
                .Select(_ => _.Path)
                .ToList();
        }

        /// <summary>
        /// Method to do the actual checking (call from thread)
        /// </summary>
        private void DoCheck(PathCheckRequest request)
        {
            var pathList = request.Entries.ToList();
            var report = PathConflictAnalyzer.BuildReport(pathList.Select(_ => _.Path), extensions, request.SystemPathCount);
            if (!IsLatestRequest(request.Id) || abortCurrentCheck) return;

            latestConflictReport = report;

            for (var i = 0; i < pathList.Count; i++)
            {
                if (!IsLatestRequest(request.Id) || abortCurrentCheck) return;

                ApplyStatus(pathList[i], i, report, request.Id);
            }

            if (IsLatestRequest(request.Id))
            {
                Interlocked.Exchange(ref latestCompletedCheckId, request.Id);
            }
        }

        private void ApplyStatus(AnnotatedPathEntry path, int index, PathConflictReport report, int requestId)
        {
            var validationIssues = new List<string>();
            var seriousError = false;

            PathResolution resolution;
            if (!path.Path.TryResolve(out resolution))
            {
                validationIssues.Add("Path could not be resolved: " + resolution.ErrorMessage);
                seriousError = true;
            }
            else if (!Directory.Exists(resolution.ActualPathForAccess))
            {
                validationIssues.Add("Does not exist");
                seriousError = true;
            }

            IList<string> conflictingFiles;
            if (!report.ConflictFilesByPathIndex.TryGetValue(index, out conflictingFiles))
            {
                conflictingFiles = new List<string>();
            }

            if (!IsLatestRequest(requestId) || abortCurrentCheck) return;

            path.SetStatus(validationIssues, seriousError, conflictingFiles);
        }

        /// <summary>
        /// The background thread that will do the checking of the current path
        /// </summary>
        private void CheckerLoop()
        {
            while (running)
            {
                var subject = pathsToProcess.Take();
                if (subject != null)
                {
                    abortCurrentCheck = false;
                    DoCheck(subject);
                }
            }
        }

        /// <summary>
        /// Search the current path set for all files starting with the given prefix
        /// </summary>
        /// <remarks>
        /// Excludes files with the same name later on in the path, and only files with
        /// applicable extensions.
        /// </remarks>
        public IEnumerable<PathMatch> Search(string prefix)
        {
            var xs = new List<PathMatch>();
            if (prefix == "") return xs;

            var seen = new HashSet<string>();
            foreach (var p in currentPath)
            {
                var newFound = p.Find(prefix)
                    .Where(match => extensions.Contains(Path.GetExtension(match.Filename)))
                    .Where(match => !seen.Contains(match.Filename));

                xs.AddRange(newFound);
                newFound.Select(match => match.Filename)
                    .Each(filename => seen.Add(filename));
            }

            return xs;
        }

        /// <summary>
        /// List all files in a directory, returning them from cache if available to speed up subsequent searches
        /// </summary>
        private IEnumerable<string> ListFiles(PathEntry path)
        {
            PathResolution resolution;
            if (!path.TryResolve(out resolution))
            {
                yield break;
            }

            var pathForAccess = resolution.ActualPathForAccess;

            IEnumerable<string> fromCache;
            if (fileCache.TryGetValue(pathForAccess, out fromCache))
            {
                foreach (var s in fromCache) yield return s;
                yield break;
            }

            List<string> enumeratedFiles;
            try
            {
                enumeratedFiles = Directory.EnumerateFiles(pathForAccess)
                    .Where(_ => extensions.Contains(Path.GetExtension(_).ToLower()))
                    .Select(Path.GetFileName)
                    .ToList();
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is SecurityException ||
                ex is PathTooLongException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                Debug.Print("Error enumerating files for {0}: {1}", path.SymbolicPath, ex.Message);
                yield break;
            }

            fileCache[pathForAccess] = enumeratedFiles;
            foreach (var file in enumeratedFiles)
            {
                yield return file;
            }
        }

        private Version GetFileVersion(PathEntry path, string filename)
        {
            PathResolution resolution;
            if (!path.TryResolve(out resolution))
            {
                return new Version(0, 0, 0, 0);
            }

            var fullPath = Path.Combine(resolution.ActualPathForAccess, filename);
            return versionCache.GetOrAdd(fullPath, _ =>
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(fullPath);
                    if (info.FileMajorPart >= 0)
                    {
                        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
                    }
                }
                catch
                {
                }

                return new Version(0, 0, 0, 0);
            });
        }

        public void Dispose()
        {
            running = false;
            pathsToProcess.Add(null);
            thread.Join();
        }

        private bool IsLatestRequest(int requestId)
        {
            return requestId == Interlocked.CompareExchange(ref latestRequestedCheckId, 0, 0);
        }
    }
}
