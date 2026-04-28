using System;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class AutoSortPreviewServiceTests
    {
        [TestMethod]
        public void ApplyIfConfirmed_LeavesListsUntouchedWhenPreviewIsCancelled()
        {
            var originalSystem = new[] { new PathEntry(@"C:\Windows\system32") };
            var originalUser = new[] { new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin") };
            var plan = new AutoSortPlan(
                new AutoSortPlanStage(AutoSortPlanStageKind.Before, originalSystem, originalUser),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, originalSystem, new PathEntry[0]),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, originalSystem, new PathEntry[0]),
                new[] { new AutoSortPromotion(new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin"), new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin"), PathScope.User, PathScope.System, PathOwnership.Machine) },
                new AutoSortNormalization[0],
                new AutoSortReorder[0],
                new AutoSortDemotion[0],
                new AutoSortWarning[0]);

            var result = AutoSortPreviewService.ApplyIfConfirmed(plan, _ => false);

            CollectionAssert.AreEqual(originalSystem.Select(_ => _.SymbolicPath).ToArray(), result.SystemPath.Select(_ => _.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(originalUser.Select(_ => _.SymbolicPath).ToArray(), result.UserPath.Select(_ => _.SymbolicPath).ToArray());
            Assert.IsFalse(result.Applied);
        }

        [TestMethod]
        public void ApplyIfConfirmed_ReturnsAutosortedMigratedListsWhenConfirmed()
        {
            var finalSystem = new[]
            {
                new PathEntry(@"C:\Windows\system32"),
                new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin")
            };

            var plan = new AutoSortPlan(
                new AutoSortPlanStage(
                    AutoSortPlanStageKind.Before,
                    new[] { new PathEntry(@"C:\Windows\system32") },
                    new[] { new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin") }),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, finalSystem, new PathEntry[0]),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, finalSystem, new PathEntry[0]),
                new[] { new AutoSortPromotion(new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin"), new PathEntry(@"C:\Program Files\Zulu\zulu-21\bin"), PathScope.User, PathScope.System, PathOwnership.Machine) },
                new AutoSortNormalization[0],
                new AutoSortReorder[0],
                new AutoSortDemotion[0],
                new AutoSortWarning[0]);

            var result = AutoSortPreviewService.ApplyIfConfirmed(plan, _ => true);

            Assert.IsTrue(result.Applied);
            CollectionAssert.AreEqual(finalSystem.Select(_ => _.SymbolicPath).ToArray(), result.SystemPath.Select(_ => _.SymbolicPath).ToArray());
            Assert.AreEqual(0, result.UserPath.Count);
        }

        [TestMethod]
        public void ApplyIfConfirmed_AppliesCleanupOnlyPlansWhenConfirmed()
        {
            var originalSystem = new[] { new PathEntry(@"C:\Windows\system32") };
            var originalUser = new[]
            {
                new PathEntry(@"C:\missing"),
                new PathEntry(@"C:\Tools")
            };
            var finalUser = new[] { new PathEntry(@"C:\Tools") };

            var plan = new AutoSortPlan(
                new AutoSortPlanStage(AutoSortPlanStageKind.Before, originalSystem, originalUser),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, originalSystem, finalUser),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, originalSystem, finalUser),
                new AutoSortPromotion[0],
                new AutoSortNormalization[0],
                new AutoSortReorder[0],
                new AutoSortDemotion[0],
                new AutoSortWarning[0],
                new[]
                {
                    new AutoSortCleanup(new PathEntry(@"C:\missing"), PathScope.User, PathCleanupRemovalKind.MissingResolvedPath)
                });

            var result = AutoSortPreviewService.ApplyIfConfirmed(plan, _ => true);

            Assert.IsTrue(result.Applied);
            CollectionAssert.AreEqual(originalSystem.Select(_ => _.SymbolicPath).ToArray(), result.SystemPath.Select(_ => _.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(finalUser.Select(_ => _.SymbolicPath).ToArray(), result.UserPath.Select(_ => _.SymbolicPath).ToArray());
        }

        [TestMethod]
        public void PreviewWindow_IncludesCleanupCountInSummaryAndTabHeader()
        {
            var plan = new AutoSortPlan(
                new AutoSortPlanStage(AutoSortPlanStageKind.Before, new[] { new PathEntry(@"C:\Windows\system32") }, new[] { new PathEntry(@"C:\missing") }),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterMigration, new[] { new PathEntry(@"C:\Windows\system32") }, new PathEntry[0]),
                new AutoSortPlanStage(AutoSortPlanStageKind.AfterAutosort, new[] { new PathEntry(@"C:\Windows\system32") }, new PathEntry[0]),
                new AutoSortPromotion[0],
                new AutoSortNormalization[0],
                new AutoSortReorder[0],
                new AutoSortDemotion[0],
                new AutoSortWarning[0],
                new[]
                {
                    new AutoSortCleanup(new PathEntry(@"C:\missing"), PathScope.User, PathCleanupRemovalKind.MissingResolvedPath)
                });

            string summary = null;
            string header = null;
            Exception error = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        var application = new Application();
                        application.Resources.MergedDictionaries.Add(
                            new ResourceDictionary
                            {
                                Source = new Uri("/WindowsPathEditor;component/Resources/Theme.xaml", UriKind.Relative)
                            });
                    }

                    var window = new AutoSortPreviewWindow();
                    window.Plan = plan;
                    summary = window.ChangeSummaryText;
                    header = window.CleanupTabHeader;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null) throw error;

            StringAssert.Contains(summary, "1 cleanup");
            Assert.AreEqual("Cleanup  (1)", header);
        }
    }
}
