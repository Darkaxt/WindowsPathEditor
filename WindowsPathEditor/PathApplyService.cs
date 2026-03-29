using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    internal sealed class PathStateSnapshot
    {
        public PathStateSnapshot(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            SystemPath = SafePath(systemPath);
            UserPath = SafePath(userPath);
        }

        public IList<PathEntry> SystemPath { get; private set; }

        public IList<PathEntry> UserPath { get; private set; }

        internal static PathStateSnapshot ReadCurrent()
        {
            var registry = new PathRegistry();
            return new PathStateSnapshot(registry.SystemPath, registry.UserPath);
        }

        internal static bool PathsEqual(IEnumerable<PathEntry> left, IEnumerable<PathEntry> right)
        {
            return SafePath(left).SequenceEqual(SafePath(right), PathEntryComparers.SymbolicPath);
        }

        private static IList<PathEntry> SafePath(IEnumerable<PathEntry> path)
        {
            return (path ?? Enumerable.Empty<PathEntry>()).ToList();
        }
    }

    internal sealed class PathImportExecutionResult
    {
        public PathImportExecutionResult(bool started, int exitCode, string errorMessage)
        {
            Started = started;
            ExitCode = exitCode;
            ErrorMessage = errorMessage;
        }

        public bool Started { get; private set; }

        public int ExitCode { get; private set; }

        public string ErrorMessage { get; private set; }

        public bool Succeeded
        {
            get { return Started && ExitCode == 0 && string.IsNullOrEmpty(ErrorMessage); }
        }

        public static PathImportExecutionResult Success()
        {
            return new PathImportExecutionResult(true, 0, null);
        }

        public static PathImportExecutionResult Failure(string errorMessage)
        {
            return new PathImportExecutionResult(false, -1, errorMessage);
        }
    }

    internal sealed class PathApplyResult
    {
        public bool Succeeded { get; set; }

        public string BackupPath { get; set; }

        public string ApplyPath { get; set; }

        public string ErrorMessage { get; set; }

        public string WarningMessage { get; set; }

        public bool SystemMatches { get; set; }

        public bool UserMatches { get; set; }

        public PathStateSnapshot ActualState { get; set; }
    }

    internal sealed class PathApplyService
    {
        private readonly Func<PathStateSnapshot> readCurrentState;
        private readonly Func<IEnumerable<PathEntry>, IEnumerable<PathEntry>, DateTime, bool, bool, string> writeBackupFile;
        private readonly Func<IEnumerable<PathEntry>, IEnumerable<PathEntry>, DateTime, bool, bool, string> writeApplyFile;
        private readonly Func<string, bool, PathImportExecutionResult> importRegFile;
        private readonly Action notifyEnvironmentChanged;
        private readonly Func<DateTime> nowProvider;

        internal PathApplyService()
            : this(
                PathStateSnapshot.ReadCurrent,
                (systemPath, userPath, timestamp, includeSystemPath, includeUserPath) => PathBackupExporter.WriteBackup(systemPath, userPath, timestamp),
                PathBackupExporter.WriteApplyFile,
                RegistryImportRunner.Import,
                PathRegistry.NotifyEnvironmentChange,
                () => DateTime.Now)
        {
        }

        internal PathApplyService(
            Func<PathStateSnapshot> readCurrentState,
            Func<IEnumerable<PathEntry>, IEnumerable<PathEntry>, DateTime, bool, bool, string> writeBackupFile,
            Func<IEnumerable<PathEntry>, IEnumerable<PathEntry>, DateTime, bool, bool, string> writeApplyFile,
            Func<string, bool, PathImportExecutionResult> importRegFile,
            Action notifyEnvironmentChanged,
            Func<DateTime> nowProvider)
        {
            this.readCurrentState = readCurrentState ?? throw new ArgumentNullException("readCurrentState");
            this.writeBackupFile = writeBackupFile ?? throw new ArgumentNullException("writeBackupFile");
            this.writeApplyFile = writeApplyFile ?? throw new ArgumentNullException("writeApplyFile");
            this.importRegFile = importRegFile ?? throw new ArgumentNullException("importRegFile");
            this.notifyEnvironmentChanged = notifyEnvironmentChanged ?? (() => { });
            this.nowProvider = nowProvider ?? (() => DateTime.Now);
        }

        internal PathApplyResult Apply(PathStateSnapshot current, PathStateSnapshot expected, bool needsElevation)
        {
            if (current == null) throw new ArgumentNullException("current");
            if (expected == null) throw new ArgumentNullException("expected");

            var timestamp = nowProvider();
            var writeSystemPath = !PathStateSnapshot.PathsEqual(current.SystemPath, expected.SystemPath);
            var writeUserPath = !PathStateSnapshot.PathsEqual(current.UserPath, expected.UserPath);
            string backupPath = null;
            string applyPath = null;

            if (!writeSystemPath && !writeUserPath)
            {
                return new PathApplyResult
                {
                    Succeeded = true,
                    SystemMatches = true,
                    UserMatches = true,
                    ActualState = current
                };
            }

            try
            {
                backupPath = writeBackupFile(current.SystemPath, current.UserPath, timestamp, true, true);
                applyPath = writeApplyFile(expected.SystemPath, expected.UserPath, timestamp, writeSystemPath, writeUserPath);
                var importResult = importRegFile(applyPath, needsElevation);

                if (!importResult.Succeeded)
                {
                    return new PathApplyResult
                    {
                        BackupPath = backupPath,
                        ApplyPath = applyPath,
                        ErrorMessage = importResult.ErrorMessage ?? "The registry import did not complete successfully."
                    };
                }

                var actualState = readCurrentState();
                var systemMatches = !writeSystemPath || PathStateSnapshot.PathsEqual(expected.SystemPath, actualState.SystemPath);
                var userMatches = !writeUserPath || PathStateSnapshot.PathsEqual(expected.UserPath, actualState.UserPath);

                var result = new PathApplyResult
                {
                    BackupPath = backupPath,
                    ApplyPath = applyPath,
                    ActualState = actualState,
                    SystemMatches = systemMatches,
                    UserMatches = userMatches,
                    Succeeded = systemMatches && userMatches
                };

                if (!result.Succeeded)
                {
                    result.ErrorMessage = "The imported PATH values did not match the expected registry state.";
                    return result;
                }

                try
                {
                    notifyEnvironmentChanged();
                }
                catch (Exception ex)
                {
                    result.WarningMessage = "The PATH values were saved, but Explorer could not be notified: " + ex.Message;
                }

                return result;
            }
            catch (Exception ex)
            {
                return new PathApplyResult
                {
                    BackupPath = backupPath,
                    ApplyPath = applyPath,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
