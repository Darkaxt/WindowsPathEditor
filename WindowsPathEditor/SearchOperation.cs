using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Security;
using System.Diagnostics;

namespace WindowsPathEditor
{
    /// <summary>
    /// Class to search for 'bin' directories
    /// </summary>
    class SearchOperation
    {
        private readonly String root;
        private readonly List<String> result = new List<String>();
        private readonly IReportProgress progressSink;
        private readonly int maxDepth;

        public SearchOperation(string root, int maxDepth, IReportProgress progressSink)
        {
            this.root         = root;
            this.progressSink = progressSink;
            this.maxDepth     = maxDepth;
        }

        public List<string> Result { get { return result; }}

        public IEnumerable<string> Run()
        {
            progressSink.Begin();
            try
            {
                Search(root, 0);
                return result;
            }
            catch (Exception ex)
            {
                Debug.Print("Error while scanning for bin directories: {0}", ex);
                return result;
            }
            finally
            {
                progressSink.Done();
            }
        }

        private void Search(String dir, int level)
        {
            progressSink.ReportProgress(dir);
            if (progressSink.Cancelled) return;

            if (string.Equals(Path.GetFileName(dir), "bin", StringComparison.OrdinalIgnoreCase))
            {
                progressSink.FoundCandidate(dir);
                result.Add(dir);
                return; // No need to descend further 
            }

            // Skip the Windows directory. It's huge and the chance of bounty is small.
            if (string.Equals(Path.GetFullPath(dir), Environment.GetEnvironmentVariable("windir"), StringComparison.OrdinalIgnoreCase))
                return;

            // If this is not a 'bin' directory, search its children
            if (level < maxDepth)
            {
                try {
                    foreach (var subdir in Directory.EnumerateDirectories(dir))
                    {
                        Search(subdir, level + 1);
                        if (progressSink.Cancelled) return;
                    }
                } catch (SecurityException) {
                    // Ignore
                } catch (UnauthorizedAccessException) {
                    // Ignore
                } catch (IOException) {
                    // Ignore
                } catch (ArgumentException) {
                    // Ignore
                } catch (NotSupportedException) {
                    // Ignore
                }
            }
        }
    }
}
