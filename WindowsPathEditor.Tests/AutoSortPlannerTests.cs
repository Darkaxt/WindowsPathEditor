using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class AutoSortPlannerTests
    {
        [TestMethod]
        public void Build_PromotesResolvedCustomUserPathsIntoSystemPath()
        {
            using (var root = TestDirectory.Create())
            {
                var userProfile = root.CreateDirectory("UserProfile");
                var programFiles = root.CreateDirectory("Program Files");
                var downloads = root.CreateDirectory("Downloads");
                var customTool = Path.Combine(downloads, "Programs", "PortableTool");
                Directory.CreateDirectory(customTool);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("UserProfile", userProfile),
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new[] { userProfile },
                    new[] { programFiles });

                var plan = AutoSortPlanner.Build(
                    new PathEntry[0],
                    new[] { new PathEntry(customTool) },
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                Assert.AreEqual(1, plan.Promotions.Count);
                Assert.AreEqual(customTool, plan.AfterMigration.SystemPath.Single().SymbolicPath);
                Assert.AreEqual(0, plan.AfterMigration.UserPath.Count);
                Assert.IsTrue(plan.Promotions.Any(_ => _.Path.SymbolicPath == customTool));
            }
        }

        [TestMethod]
        public void Build_KeepsUnresolvedEntriesInPlaceAndAddsWarning()
        {
            var plan = AutoSortPlanner.Build(
                new[] { new PathEntry("%PATH%") },
                new PathEntry[0],
                new[] { ".exe" },
                PathMigrationPolicy.CreateDefault(),
                AutoSortPlannerMode.AggressivePromotion);

            Assert.AreEqual("%PATH%", plan.AfterMigration.SystemPath.Single().SymbolicPath);
            Assert.AreEqual(0, plan.AfterMigration.UserPath.Count);
            Assert.IsFalse(plan.HasChanges);
            Assert.IsTrue(plan.HasPreviewContent);
            Assert.IsTrue(plan.Warnings.Any(_ =>
                _.Kind == AutoSortWarningKind.UnresolvedPath &&
                _.Path.SymbolicPath == "%PATH%"));
        }

        [TestMethod]
        public void Build_WarnsAboutUserOwnedEntriesAlreadyInSystemPath()
        {
            using (var root = TestDirectory.Create())
            {
                var userProfile = root.CreateDirectory("UserProfile");
                var localAppData = Path.Combine(userProfile, "AppData", "Local");
                var userTool = Path.Combine(localAppData, "Tools");
                Directory.CreateDirectory(userTool);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("UserProfile", userProfile),
                        PathMigrationPolicy.Variable("LocalAppData", localAppData)
                    },
                    new[] { userProfile, localAppData },
                    new string[0]);

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(userTool) },
                    new PathEntry[0],
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                Assert.AreEqual(@"%LocalAppData%\Tools", plan.AfterMigration.SystemPath.Single().SymbolicPath);
                Assert.IsTrue(plan.Warnings.Any(_ =>
                    _.Kind == AutoSortWarningKind.UserOwnedSystemPath &&
                    _.Path.SymbolicPath == userTool));
            }
        }
    }
}
