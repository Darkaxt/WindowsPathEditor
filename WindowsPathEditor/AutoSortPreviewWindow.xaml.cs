using System.ComponentModel;
using System.Windows;

namespace WindowsPathEditor
{
    public partial class AutoSortPreviewWindow : Window, INotifyPropertyChanged
    {
        public static readonly DependencyProperty PlanProperty =
            DependencyProperty.Register(
                "Plan",
                typeof(AutoSortPlan),
                typeof(AutoSortPreviewWindow),
                new PropertyMetadata(null, OnPlanChanged));

        public AutoSortPreviewWindow()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public AutoSortPlan Plan
        {
            get { return (AutoSortPlan)GetValue(PlanProperty); }
            set { SetValue(PlanProperty, value); }
        }

        public string ChangeSummaryText
        {
            get
            {
                var plan = Plan;
                if (plan == null)
                    return "No preview data is available.";

                return string.Format(
                    "{0} cleanup(s), {1} promotion(s), {2} demotion(s), {3} normalization(s), {4} reorder(s), {5} warning(s).",
                    plan.Cleanup.Count,
                    plan.Promotions.Count,
                    plan.Demotions.Count,
                    plan.Normalizations.Count,
                    plan.Reorders.Count,
                    plan.Warnings.Count);
            }
        }

        public string MixedScopeMetricText
        {
            get
            {
                var plan = Plan;
                if (plan == null) return "";
                return string.Format(
                    "{0}  \u2192  {1}",
                    plan.Before.Conflicts.MixedScopeGroupCount,
                    plan.AfterAutosort.Conflicts.MixedScopeGroupCount);
            }
        }

        public string ShadowedRowMetricText
        {
            get
            {
                var plan = Plan;
                if (plan == null) return "";
                return string.Format(
                    "{0}  \u2192  {1}",
                    plan.Before.Conflicts.ShadowedByHigherVersionRowCount,
                    plan.AfterAutosort.Conflicts.ShadowedByHigherVersionRowCount);
            }
        }

        public string PromotionsTabHeader
        {
            get { return FormatTabHeader("Promotions", Plan == null ? 0 : Plan.Promotions.Count); }
        }

        public string CleanupTabHeader
        {
            get { return FormatTabHeader("Cleanup", Plan == null ? 0 : Plan.Cleanup.Count); }
        }

        public string DemotionsTabHeader
        {
            get { return FormatTabHeader("Demotions", Plan == null ? 0 : Plan.Demotions.Count); }
        }

        public string NormalizationsTabHeader
        {
            get { return FormatTabHeader("Normalizations", Plan == null ? 0 : Plan.Normalizations.Count); }
        }

        public string ReordersTabHeader
        {
            get { return FormatTabHeader("Reorders", Plan == null ? 0 : Plan.Reorders.Count); }
        }

        public string WarningsTabHeader
        {
            get { return FormatTabHeader("Warnings", Plan == null ? 0 : Plan.Warnings.Count); }
        }

        private static string FormatTabHeader(string name, int count)
        {
            return string.Format("{0}  ({1})", name, count);
        }

        private static void OnPlanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = d as AutoSortPreviewWindow;
            if (window == null) return;

            window.RaisePropertyChanged("ChangeSummaryText");
            window.RaisePropertyChanged("MixedScopeMetricText");
            window.RaisePropertyChanged("ShadowedRowMetricText");
            window.RaisePropertyChanged("CleanupTabHeader");
            window.RaisePropertyChanged("PromotionsTabHeader");
            window.RaisePropertyChanged("DemotionsTabHeader");
            window.RaisePropertyChanged("NormalizationsTabHeader");
            window.RaisePropertyChanged("ReordersTabHeader");
            window.RaisePropertyChanged("WarningsTabHeader");
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
