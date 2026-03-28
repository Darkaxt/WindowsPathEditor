using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace WindowsPathEditor
{
    /// <summary>
    /// Mutable wrapper for a PathEntry that can expose validation and conflict status.
    /// </summary>
    public class AnnotatedPathEntry : INotifyPropertyChanged
    {
        private readonly object stateLock = new object();
        private List<string> validationIssues = new List<string>();
        private List<string> conflictingFiles = new List<string>();
        private bool seriousError;
        private bool validationPending = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public AnnotatedPathEntry(PathEntry path)
        {
            Path = path;
        }

        public PathEntry Path { get; private set; }

        /// <summary>
        /// Return the alert level (0, 1 or 2) depending on whether everything is ok,
        /// there are conflicts/warnings, or the path is missing/unresolvable.
        /// </summary>
        public int AlertLevel
        {
            get
            {
                lock (stateLock)
                {
                    if (validationPending) return -1;
                    if (seriousError) return 2;
                    if (validationIssues.Count > 0 || conflictingFiles.Count > 0) return 1;
                    return 0;
                }
            }
        }

        public bool SeriousError
        {
            get
            {
                lock (stateLock)
                {
                    return seriousError;
                }
            }
        }

        public bool Exists { get { return Path.Exists; } }

        public string SymbolicPath { get { return Path.SymbolicPath; } }

        public bool HasConflicts
        {
            get
            {
                lock (stateLock)
                {
                    return conflictingFiles.Count > 0;
                }
            }
        }

        public string StatusSummary
        {
            get
            {
                lock (stateLock)
                {
                    if (validationPending)
                    {
                        return "Validation pending";
                    }

                    var parts = new List<string>();
                    if (validationIssues.Count > 0)
                    {
                        parts.Add(string.Join("; ", validationIssues));
                    }

                    if (conflictingFiles.Count > 0)
                    {
                        parts.Add(string.Format("{0} conflicting file(s)", conflictingFiles.Count));
                    }

                    return string.Join(" | ", parts);
                }
            }
        }

        public void BeginValidation()
        {
            lock (stateLock)
            {
                validationPending = true;
                validationIssues = new List<string>();
                conflictingFiles = new List<string>();
                seriousError = false;
            }

            NotifyStatusChanged();
        }

        public void SetStatus(IEnumerable<string> issues, bool isSeriousError, IEnumerable<string> files)
        {
            lock (stateLock)
            {
                validationPending = false;
                validationIssues = issues.ToList();
                seriousError = isSeriousError;
                conflictingFiles = files
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .OrderBy(_ => _, System.StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            NotifyStatusChanged();
        }

        public override string ToString()
        {
            return Path.ToString();
        }

        public static AnnotatedPathEntry FromPath(PathEntry p)
        {
            return new AnnotatedPathEntry(p);
        }

        private void NotifyStatusChanged()
        {
            PropertyChanged.Notify(() => StatusSummary);
            PropertyChanged.Notify(() => AlertLevel);
            PropertyChanged.Notify(() => HasConflicts);
        }
    }
}
