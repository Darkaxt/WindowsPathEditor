using System.Linq;
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
    }
}
