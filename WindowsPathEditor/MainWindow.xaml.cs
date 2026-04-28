using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Windows.Interop;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WindowsPathEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static RoutedCommand CleanUp = new RoutedCommand();

        private readonly PathRegistry reg = new PathRegistry();
        private readonly PathChecker checker;
        private readonly object stateLock = new object();
        private bool pathsDirty = false;

        public MainWindow()
        {
            checker = new PathChecker(reg.ExecutableExtensions);

            InitializeComponent();
            ShieldIcon = UAC.GetShieldIcon();
            searchBox.SetCompleteProvider(checker.Search);

            Read();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            checker.Dispose();
        }

        private void Read()
        {
            SetPaths(reg.SystemPath, reg.UserPath);
        }

        private void SetPaths(IEnumerable<PathEntry> systemPath, IEnumerable<PathEntry> userPath)
        {
            lock (stateLock)
            {
                SystemPath = new ObservableCollectionEx<AnnotatedPathEntry>(systemPath.Select(AnnotatedPathEntry.FromPath));
                UserPath = new ObservableCollectionEx<AnnotatedPathEntry>(userPath.Select(AnnotatedPathEntry.FromPath));

                DirtyPaths();

                SystemPath.CollectionChanged += (a, b) => DirtyPaths();
                UserPath.CollectionChanged += (a, b) => DirtyPaths();
            }
        }

        private IEnumerable<PathEntry> CurrentPath
        {
            get
            {
                lock (stateLock)
                {
                    return SystemPath.Concat(UserPath).Select(_ => _.Path);
                }
            }
        }

        /// <summary>
        /// Mark the paths as dirty and schedule a check operation
        /// </summary>
        /// <remarks>
        /// (Done like this to prevent duplicate checks scheduled in the same event handler)
        /// </remarks>
        private void DirtyPaths()
        {
            pathsDirty = true;
            CompletePath.Each(_ => _.BeginValidation());
            InvalidateDependentProperties();
            Dispatcher.BeginInvoke((Action)RecheckPath);
        }

        private void RecheckPath()
        {
            if (pathsDirty)
            {
                pathsDirty = false;
                checker.Check(CompletePath, SystemPath.Count);
            }
        }

        private void Write()
        {
            var current = new PathStateSnapshot(reg.SystemPath.ToList(), reg.UserPath.ToList());
            var expected = new PathStateSnapshot(
                SystemPath.Select(_ => _.Path).ToList(),
                UserPath.Select(_ => _.Path).ToList());
            var result = new PathApplyService().Apply(current, expected, NeedsElevation);

            if (!result.Succeeded)
            {
                var details = new StringBuilder();
                details.AppendLine("The changes were NOT saved.");

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    details.AppendLine();
                    details.AppendLine(result.ErrorMessage);
                }

                if (!string.IsNullOrEmpty(result.BackupPath))
                {
                    details.AppendLine();
                    details.AppendLine("Rollback backup:");
                    details.AppendLine(result.BackupPath);
                }

                MessageBox.Show(
                    details.ToString(),
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            Read();

            if (!string.IsNullOrEmpty(result.WarningMessage))
            {
                MessageBox.Show(
                    result.WarningMessage,
                    "PATH Saved With Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// The complete path as it would be searched by Windows
        /// </summary>
        /// <remarks>
        /// First the SYSTEM entries are searched, then the USER entries.
        /// </remarks>
        private IEnumerable<AnnotatedPathEntry> CompletePath
        {
            get { return SystemPath.Concat(UserPath); }
        }

        #region Dependency Properties
        public ObservableCollectionEx<AnnotatedPathEntry> SystemPath
        {
            get { return (ObservableCollectionEx<AnnotatedPathEntry>)GetValue(SystemPathProperty); }
            set { SetValue(SystemPathProperty, value); }
        }

        public static readonly DependencyProperty SystemPathProperty =
            DependencyProperty.Register("SystemPath", typeof(ObservableCollectionEx<AnnotatedPathEntry>),
            typeof(MainWindow), new UIPropertyMetadata(new ObservableCollectionEx<AnnotatedPathEntry>(),
                (obj, e) => { ((MainWindow)obj).InvalidateDependentProperties(); }));

        public ObservableCollectionEx<AnnotatedPathEntry> UserPath
        {
            get { return (ObservableCollectionEx<AnnotatedPathEntry>)GetValue(UserPathProperty); }
            set { SetValue(UserPathProperty, value); }
        }

        public static readonly DependencyProperty UserPathProperty =
            DependencyProperty.Register("UserPath", typeof(ObservableCollectionEx<AnnotatedPathEntry>),
            typeof(MainWindow), new UIPropertyMetadata(new ObservableCollectionEx<AnnotatedPathEntry>(),
                (obj, e) => { ((MainWindow)obj).InvalidateDependentProperties(); }));

        public BitmapSource ShieldIcon
        {
            get { return (BitmapSource)GetValue(ShieldIconProperty); }
            set { SetValue(ShieldIconProperty, value); }
        }

        public static readonly DependencyProperty ShieldIconProperty =
            DependencyProperty.Register("ShieldIcon", typeof(BitmapSource), typeof(MainWindow), new UIPropertyMetadata(null));

        public bool NeedsElevation
        {
            get { return !reg.IsSystemPathWritable && SystemPathChanged; }
        }

        #endregion

        /// <summary>
        /// Called when the user has changed the path lists, to force WPF to reevaluate properties that depend on the lists
        /// </summary>
        private void InvalidateDependentProperties()
        {
            var changed = PropertyChanged;
            if (changed == null) return;

            changed(this, new PropertyChangedEventArgs("SystemPathChanged"));
            changed(this, new PropertyChangedEventArgs("UserPathChanged"));
            changed(this, new PropertyChangedEventArgs("NeedsElevation"));
        }

        private bool SystemPathChanged
        {
            get { return !PathListEqual(reg.SystemPath, SystemPath); }
        }

        private bool UserPathChanged
        {
            get { return !PathListEqual(reg.UserPath, UserPath); }
        }

        /// <summary>
        /// Compare two path lists
        /// </summary>
        private bool PathListEqual(IEnumerable<PathEntry> original, ObservableCollectionEx<AnnotatedPathEntry> edited)
        {
            return PathListEqual(original, edited.Select(_ => _.Path));
        }

        private static bool PathListEqual(IEnumerable<PathEntry> left, IEnumerable<PathEntry> right)
        {
            return left.SequenceEqual(right, PathEntryComparers.SymbolicPath);
        }

        /// <summary>
        /// Remove paths that don't exist or are listed multiple times
        /// </summary>
        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            lock (stateLock)
            {
                var cleaned = PathCleanup.Clean(SystemPath.Select(_ => _.Path), UserPath.Select(_ => _.Path));
                SetPaths(cleaned.SystemPath, cleaned.UserPath);
            }
        }

        public Func<IDataObject, object> FileDropConverter
        {
            get
            {
                return data =>
                {
                    string path = "...";
                    try
                    {
                        var d = data as System.Windows.DataObject;
                        if (d == null || !d.ContainsFileDropList() || d.GetFileDropList().Count == 0) return null;

                        path = d.GetFileDropList()[0];
                        if (File.Exists(path)) path = System.IO.Path.GetDirectoryName(path);

                        return new AnnotatedPathEntry(PathEntry.FromFilePath(path));
                    }
                    catch (Exception ex)
                    {
                        return new AnnotatedPathEntry(new PathEntry(string.Format("error dragging in {0}: {1}", path, ex.Message)));
                    }
                };
            }
        }

        private AnnotatedPathEntry GetSelectedEntry(RoutedEventArgs e)
        {
            if (systemList.IsFocused || e.Source == systemList) return systemList.SelectedItem as AnnotatedPathEntry;
            if (userList.IsFocused || e.Source == userList) return userList.SelectedItem as AnnotatedPathEntry;
            return null;
        }

        private void DoExplore(object sender, ExecutedRoutedEventArgs e)
        {
            PathResolution resolution;
            if (!GetSelectedEntry(e).Path.TryResolve(out resolution)) return;
            Process.Start("explorer.exe", "/e," + resolution.ActualPath);
        }

        private void CanExplore(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            try
            {
                var selected = GetSelectedEntry(e);
                PathResolution resolution;
                e.CanExecute = selected != null && selected.Path.TryResolve(out resolution) && Directory.Exists(resolution.ActualPathForAccess);
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in CanExplore: {0}", ex);
            }
        }

        private void DoDelete(object sender, ExecutedRoutedEventArgs e)
        {
            if (systemList.IsFocused || e.Source == systemList) SystemPath.Remove(GetSelectedEntry(e));
            if (userList.IsFocused || e.Source == userList) UserPath.Remove(GetSelectedEntry(e));
        }

        private void CanDelete(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GetSelectedEntry(e) != null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void DoSave(object sender, ExecutedRoutedEventArgs e)
        {
            lock (stateLock)
            {
                var window = new DiffWindow();
                List<PathEntry> newPaths = new List<PathEntry>(SystemPath.Concat(UserPath).Select(_ => _.Path));
                List<PathEntry> oldPaths = new List<PathEntry>(reg.SystemPath.Concat(reg.UserPath));
                foreach (var x in new ObservableCollection<DiffPath>(newPaths.Except(oldPaths, PathEntryComparers.SymbolicPath).Select(p => new DiffPath(p, true)).Concat(
                                oldPaths.Except(newPaths, PathEntryComparers.SymbolicPath).Select(p => new DiffPath(p, false)))))
                {
                    window.Changes.Add(x);
                }

                if (window.ShowDialog() == true)
                {
                    Write();
                }
            }
        }

        private void CanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = UserPathChanged || SystemPathChanged;
        }

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            var currentPaths = CompletePath.Select(_ => _.Path);

            var window = new ScanningWindow();
            var search = new SearchOperation("C:\\", 4, window);

            Task<IEnumerable<string>>.Factory.StartNew(search.Run);
            if (window.ShowDialog() == true)
            {
                UserPath.SupressNotification = true;
                ScanImportPlanner.SelectPathsToImport(window.Paths, currentPaths)
                    .Each(path => UserPath.Add(new AnnotatedPathEntry(path)));
                UserPath.SupressNotification = false;
            }
        }

        private void AutoSort_Click(object sender, RoutedEventArgs e)
        {
            lock (stateLock)
            {
                var previousSystem = SystemPath.Select(_ => _.Path).ToList();
                var previousUser = UserPath.Select(_ => _.Path).ToList();

                var sortedSystem = checker.SuggestBestOrder(previousSystem).ToList();
                var sortedUser = checker.SuggestBestOrder(previousUser).ToList();

                if (PathListEqual(previousSystem, sortedSystem) && PathListEqual(previousUser, sortedUser))
                {
                    MessageBox.Show(
                        "The current order already appears optimal for DLL conflict resolution.",
                        "Auto Sort",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SetPaths(sortedSystem, sortedUser);
                MessageBox.Show(
                    "Applied suggested order based on DLL version winners.",
                    "Auto Sort",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void AutoSort_Click(object sender, RoutedEventArgs e)
        {
            List<PathEntry> previousSystem;
            List<PathEntry> previousUser;

            lock (stateLock)
            {
                previousSystem = SystemPath.Select(_ => _.Path).ToList();
                previousUser = UserPath.Select(_ => _.Path).ToList();
            }

            // Capture off-thread inputs before leaving the UI thread
            var extensions = reg.ExecutableExtensions.ToList();
            var policy = PathMigrationPolicy.CreateDefault();
            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            Mouse.OverrideCursor = Cursors.Wait;

            Task.Factory.StartNew(() =>
                AutoSortPlanner.Build(
                    previousSystem,
                    previousUser,
                    extensions,
                    policy,
                    AutoSortPlannerMode.AggressivePromotion)
            ).ContinueWith(task =>
            {
                Mouse.OverrideCursor = null;

                if (task.IsFaulted)
                {
                    var message = task.Exception != null && task.Exception.InnerException != null
                        ? task.Exception.InnerException.Message
                        : "An unexpected error occurred.";
                    MessageBox.Show(
                        "Auto Sort failed: " + message,
                        "Auto Sort",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var plan = task.Result;

                if (!plan.HasPreviewContent)
                {
                    MessageBox.Show(
                        "No migration-aware Auto Sort changes or warnings were detected.",
                        "Auto Sort",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var applyResult = AutoSortPreviewService.ApplyIfConfirmed(
                    plan,
                    previewPlan =>
                    {
                        var window = new AutoSortPreviewWindow
                        {
                            Owner = this,
                            Plan = previewPlan
                        };
                        return window.ShowDialog() == true;
                    });

                if (!applyResult.Applied)
                    return;

                lock (stateLock)
                {
                    SetPaths(applyResult.SystemPath, applyResult.UserPath);
                }

                MessageBox.Show(
                    "Applied migration-aware Auto Sort changes to the editor. Use Save to write them to the registry.",
                    "Auto Sort",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }, uiScheduler);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialogEx())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var entry = new AnnotatedPathEntry(PathEntry.FromFilePath(dialog.SelectedPath));
                    UserPath.Add(entry);
                }
            }
        }

        private void Conflicts_Click(object sender, RoutedEventArgs e)
        {
            var report = checker.LatestConflictReport;
            if (report.IsPending)
            {
                MessageBox.Show(
                    "Conflict analysis is still running. Please try again in a moment.",
                    "Conflicts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!report.HasActionableConflicts)
            {
                MessageBox.Show(
                    report.HasConflicts
                        ? "No version-inversion conflicts were detected.\n\nShared DLL and EXE files exist across multiple PATH locations, but in every case the highest available version already wins."
                        : "No conflicting DLL or EXE files were detected.",
                    "Conflicts",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var window = new ConflictWindow();
            window.Report = report;
            window.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
