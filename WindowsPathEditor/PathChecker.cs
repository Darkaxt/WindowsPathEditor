using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.IO;
using System.Collections;
using System.Diagnostics;

namespace WindowsPathEditor
{
    public class PathChecker : IDisposable
    {
        /// <summary>
        /// The single thread that will be used to schedule all disk lookups
        /// </summary>
        private Thread thread;

        /// <summary>
        /// The queue used to communicate with the background thread
        /// </summary>
        private BlockingCollection<IEnumerable<AnnotatedPathEntry>> pathsToProcess = new BlockingCollection<IEnumerable<AnnotatedPathEntry>>(new ConcurrentQueue<IEnumerable<AnnotatedPathEntry>>());
         
        /// <summary>
        /// Cache for the listFiles operation
        /// </summary>
        private ConcurrentDictionary<string, IEnumerable<string>> fileCache = new ConcurrentDictionary<string,IEnumerable<string>>();
        private ConcurrentDictionary<string, Version> versionCache = new ConcurrentDictionary<string, Version>();

        /// <summary>
        /// The currently applicable path
        /// </summary>
        private IEnumerable<PathEntry> currentPath = Enumerable.Empty<PathEntry>();

        /// <summary>
        /// Extensions to check for conflicts
        /// </summary>
        private readonly IEnumerable<string> extensions;

        private bool running           = true;
        private bool abortCurrentCheck = false;

        public PathChecker(IEnumerable<string> extensions)
        {
            this.extensions = extensions.Concat(new[]{ ".dll" }).Select(_ => _.ToLower());
            thread = new Thread(CheckerLoop);
            thread.Start();
        }

        /// <summary>
        /// Check all paths in the given set
        /// </summary>
        public void Check(IEnumerable<AnnotatedPathEntry> paths)
        {
            currentPath = paths.Select(_ => _.Path).ToList();
            abortCurrentCheck = true;
            pathsToProcess.Add(paths.ToList());
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

                foreach (var dll in listFiles(path.ActualPath)
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
        /// <param name="paths"></param>
        private void DoCheck(IEnumerable<AnnotatedPathEntry> paths)
        {
            foreach (var path in paths)
            {
                if (abortCurrentCheck) return;

                CheckPath(path);
            }
        }

        private void CheckPath(AnnotatedPathEntry path)
        {
            path.ClearIssues();
            path.SeriousError = false;
            try
            {
                if (!path.Path.Exists)
                {
                    path.AddIssue("Does not exist");
                    path.SeriousError = true;
                    return;
                }
    
                listFiles(path.Path.ActualPath)
                    .Select(file => new { file=file, hit=FirstDir(file)})
                    .Where(fh => !string.Equals(fh.hit.Directory, path.Path.ActualPath, StringComparison.OrdinalIgnoreCase))
                    .Each(fh => AddIssueIfNeeded(path, fh.file, fh.hit));
            }
            catch (Exception ex)
            {
                Debug.Print("Error checking path: {0}", ex);
                path.SeriousError = true;
                path.AddIssue(string.Format("Error checking this path: {0}", ex.Message));
            }
        }

        /// <summary>
        /// The background thread that will do the checking of the current path
        /// </summary>
        private void CheckerLoop()
        {
            while (running) 
            {
                IEnumerable<AnnotatedPathEntry> subject = pathsToProcess.Take();
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
        private IEnumerable<string> listFiles(string path)
        {
            var pathForAccess = ResolvePathForAccess(path);

            IEnumerable<string> fromCache;
            if (fileCache.TryGetValue(pathForAccess, out fromCache))
            {
                foreach (var s in fromCache) yield return s;
                yield break;
            }

            var files = new List<string>();
            foreach (var f in Directory.EnumerateFiles(pathForAccess)
                .Where(_ => extensions.Contains(Path.GetExtension(_).ToLower()))
                .Select(Path.GetFileName))
            {
                files.Add(f);
                yield return f;
            }

            fileCache[pathForAccess] = files;
        }

        private static string ResolvePathForAccess(string path)
        {
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                var system32 = Path.Combine(windowsDir, "System32");
                if (path.StartsWith(system32, StringComparison.OrdinalIgnoreCase))
                {
                    var tail = path.Substring(system32.Length).TrimStart('\\');
                    var sysnative = Path.Combine(Path.Combine(windowsDir, "Sysnative"), tail);
                    if (Directory.Exists(sysnative))
                    {
                        return sysnative;
                    }
                }
            }

            return path;
        }

        /// <summary>
        /// Find the file on the given paths and return the first match
        /// </summary>
        private PathMatch FirstDir(string filename)
        {
            return currentPath
                .Where(path => File.Exists(Path.Combine(path.ActualPathForAccess, filename)))
                .Select(path => new PathMatch(path.ActualPath, filename))
                .FirstOrDefault() ?? new PathMatch("", "");
        }

        private void AddIssueIfNeeded(AnnotatedPathEntry path, string filename, PathMatch firstHit)
        {
            if (!string.Equals(Path.GetExtension(filename), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                path.AddIssue(string.Format("{0} shadowed by {1}", filename, firstHit.FullPath));
                return;
            }

            var candidatePaths = currentPath
                .Where(entry => File.Exists(Path.Combine(entry.ActualPathForAccess, filename)))
                .ToList();

            if (candidatePaths.Count <= 1)
            {
                return;
            }

            var winningPath = candidatePaths
                .Select(entry => new { Entry = entry, Version = GetFileVersion(entry, filename) })
                .OrderByDescending(_ => _.Version)
                .ThenBy(_ => candidatePaths.IndexOf(_.Entry))
                .First();

            var firstPath = candidatePaths.First();
            if (firstPath.Equals(winningPath.Entry))
            {
                return;
            }

            path.AddIssue(string.Format(
                "{0} v{1} shadowed by {2} (v{3})",
                filename,
                FormatVersion(winningPath.Version),
                firstHit.FullPath,
                FormatVersion(GetFileVersion(firstPath, filename))));
        }

        private Version GetFileVersion(PathEntry path, string filename)
        {
            var fullPath = Path.Combine(path.ActualPathForAccess, filename);
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

        private static string FormatVersion(Version version)
        {
            return version == null ? "0.0.0.0" : version.ToString();
        }

        public void Dispose()
        {
            running = false;
            pathsToProcess.Add(null);
            thread.Join();
        }
    }

}
