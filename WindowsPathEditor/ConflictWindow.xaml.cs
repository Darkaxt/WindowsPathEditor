using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsPathEditor
{
    public partial class ConflictWindow : Window, INotifyPropertyChanged
    {
        private PathConflictGroup selectedGroup;

        public ConflictWindow()
        {
            InitializeComponent();
            Report = PathConflictReport.Empty;
            groupGrid.RowHeaderStyle = BuildRowHeaderStyle();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PathConflictReport Report
        {
            get { return (PathConflictReport)GetValue(ReportProperty); }
            set { SetValue(ReportProperty, value); }
        }

        public static readonly DependencyProperty ReportProperty =
            DependencyProperty.Register("Report", typeof(PathConflictReport), typeof(ConflictWindow),
                new PropertyMetadata(PathConflictReport.Empty, (obj, args) => ((ConflictWindow)obj).OnReportChanged()));

        public PathConflictGroup SelectedGroup
        {
            get { return selectedGroup; }
            set
            {
                if (ReferenceEquals(selectedGroup, value))
                {
                    return;
                }

                selectedGroup = value;
                NotifyPropertyChanged("SelectedGroup");
                RebuildGridColumns();
            }
        }

        public string GroupSummary
        {
            get
            {
                var count = Report == null ? 0 : Report.Groups.Count;
                return string.Format(
                    "{0} conflict group{1}",
                    count,
                    count == 1 ? "" : "s");
            }
        }

        private void OnReportChanged()
        {
            NotifyPropertyChanged("GroupSummary");
            SelectedGroup = Report.Groups.FirstOrDefault();
        }

        private void RebuildGridColumns()
        {
            if (groupGrid == null)
            {
                return;
            }

            groupGrid.Columns.Clear();

            var hasGroup = SelectedGroup != null;
            noGroupPlaceholder.Visibility = hasGroup ? Visibility.Collapsed : Visibility.Visible;
            groupGrid.Visibility = hasGroup ? Visibility.Visible : Visibility.Collapsed;

            if (!hasGroup)
            {
                return;
            }

            groupGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "File",
                Binding = new Binding("Filename"),
                MinWidth = 220,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            for (var i = 0; i < SelectedGroup.Columns.Count; i++)
            {
                groupGrid.Columns.Add(BuildVersionColumn(i));
            }
        }

        private DataGridTextColumn BuildVersionColumn(int index)
        {
            var column = SelectedGroup.Columns[index];

            return new DataGridTextColumn
            {
                Header = BuildColumnHeader(column),
                Binding = new Binding(string.Format("Cells[{0}].DisplayValue", index)),
                MinWidth = 200,
                Width = DataGridLength.Auto,
                CellStyle = BuildVersionCellStyle(index)
            };
        }

        private FrameworkElement BuildColumnHeader(PathConflictColumn column)
        {
            var originTag = new Border
            {
                Background = GetOriginBackground(column.Origin),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = column.OriginLabel,
                    Foreground = GetOriginForeground(column.Origin),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                }
            };

            return new StackPanel
            {
                Margin = new Thickness(0, 4, 0, 4),
                ToolTip = column.OriginTooltip,
                Children =
                {
                    originTag,
                    new TextBlock
                    {
                        Text = column.Header,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 240
                    }
                }
            };
        }

        private Style BuildRowHeaderStyle()
        {
            var style = new Style(typeof(DataGridRowHeader));
            style.Setters.Add(new Setter(Control.WidthProperty, 14.0));
            style.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("SectionHeaderBrush")));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("BorderBrush")));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            style.Setters.Add(new Setter(ContentControl.ContentProperty, ""));

            AddStateTrigger(style, PathConflictWinnerState.Preferred, (Brush)FindResource("AddedRowBrush"));
            AddStateTrigger(style, PathConflictWinnerState.ShadowedByHigherVersion, (Brush)FindResource("WarningRowBrush"));

            return style;
        }

        private Style BuildVersionCellStyle(int index)
        {
            var style = new Style(typeof(DataGridCell));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 8, 0)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("BorderBrush")));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));

            var runtimeWinnerTrigger = new DataTrigger
            {
                Binding = new Binding(string.Format("Cells[{0}].IsRuntimeWinner", index)),
                Value = true
            };
            runtimeWinnerTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("PrimaryBrush")));
            runtimeWinnerTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Triggers.Add(runtimeWinnerTrigger);

            var highestVersionTrigger = new DataTrigger
            {
                Binding = new Binding(string.Format("Cells[{0}].IsHighestVersion", index)),
                Value = true
            };
            highestVersionTrigger.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("AddedRowBrush")));
            highestVersionTrigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Triggers.Add(highestVersionTrigger);

            var unavailableVersionTrigger = new DataTrigger
            {
                Binding = new Binding(string.Format("Cells[{0}].HasComparableVersion", index)),
                Value = false
            };
            unavailableVersionTrigger.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("TextSecondaryBrush")));
            style.Triggers.Add(unavailableVersionTrigger);

            return style;
        }

        private static void AddStateTrigger(Style style, PathConflictWinnerState winnerState, Brush background)
        {
            var trigger = new DataTrigger
            {
                Binding = new Binding("WinnerState"),
                Value = winnerState
            };
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, background));
            style.Triggers.Add(trigger);
        }

        private Brush GetOriginBackground(PathConflictColumnOrigin origin)
        {
            if (origin == PathConflictColumnOrigin.System)
            {
                return (Brush)FindResource("PrimaryBrush");
            }

            if (origin == PathConflictColumnOrigin.User)
            {
                return (Brush)FindResource("SuccessBrush");
            }

            return (Brush)FindResource("SectionHeaderBrush");
        }

        private Brush GetOriginForeground(PathConflictColumnOrigin origin)
        {
            return origin == PathConflictColumnOrigin.Mixed
                ? (Brush)FindResource("TextPrimaryBrush")
                : Brushes.White;
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
