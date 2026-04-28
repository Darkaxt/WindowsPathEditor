using System.Collections.Generic;
using System.Diagnostics;
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
        public void Build_DemotesUserOwnedEntriesAlreadyInSystemPath()
        {
            using (var root = TestDirectory.Create())
            {
                var userRoot = root.CreateDirectory("user");
                var userTool = Path.Combine(userRoot, "Tools");
                Directory.CreateDirectory(userTool);

                var policy = new PathMigrationPolicy(
                    new KeyValuePair<string, string>[0],
                    new[] { userRoot },
                    new string[0]);

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(userTool) },
                    new PathEntry[0],
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                Assert.AreEqual(0, plan.AfterMigration.SystemPath.Count);
                CollectionAssert.AreEqual(
                    new[] { userTool },
                    plan.AfterMigration.UserPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(1, plan.Demotions.Count);
                Assert.AreEqual(userTool, plan.Demotions.Single().Path.SymbolicPath);
                Assert.IsFalse(plan.Warnings.Any(_ => _.Path != null && _.Path.SymbolicPath == userTool));
            }
        }

        [TestMethod]
        public void Build_WhenStabilizationChangesFinalOrder_ReportsNetReorders()
        {
            using (var root = TestDirectory.Create())
            {
                var machineRoot = root.CreateDirectory("machine");
                var first = Path.Combine(machineRoot, "first");
                var second = Path.Combine(machineRoot, "second");
                Directory.CreateDirectory(first);
                Directory.CreateDirectory(second);

                var lowVersionSource = typeof(AutoSortPlannerTests).Assembly.Location;
                var highVersionSource = typeof(PathEntry).Assembly.Location;
                var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
                var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

                var lowNumericVersion = new System.Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
                var highNumericVersion = new System.Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
                if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
                {
                    var swap = lowVersionSource;
                    lowVersionSource = highVersionSource;
                    highVersionSource = swap;
                }

                File.Copy(lowVersionSource, Path.Combine(first, "shared.dll"));
                File.Copy(highVersionSource, Path.Combine(second, "shared.dll"));

                var policy = new PathMigrationPolicy(
                    new KeyValuePair<string, string>[0],
                    new string[0],
                    new[] { machineRoot });
                var normalizedFirst = PathEntry.FromFilePath(first, policy.NormalizationVariables).SymbolicPath;
                var normalizedSecond = PathEntry.FromFilePath(second, policy.NormalizationVariables).SymbolicPath;

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(first), new PathEntry(second) },
                    new PathEntry[0],
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                CollectionAssert.AreEqual(
                    new[] { normalizedFirst, normalizedSecond },
                    plan.AfterMigration.SystemPath.Select(_ => _.SymbolicPath).ToArray());
                CollectionAssert.AreEqual(
                    new[] { normalizedSecond, normalizedFirst },
                    plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToArray());

                Assert.AreEqual(2, plan.Reorders.Count);
                Assert.IsTrue(plan.Reorders.Any(_ =>
                    _.Path.SymbolicPath == normalizedSecond &&
                    _.FromIndex == 1 &&
                    _.ToIndex == 0));
                Assert.IsTrue(plan.Reorders.Any(_ =>
                    _.Path.SymbolicPath == normalizedFirst &&
                    _.FromIndex == 0 &&
                    _.ToIndex == 1));
            }
        }

        [TestMethod]
        public void Build_ProtectsSystemRootEntriesFromAutomaticDemotion()
        {
            using (var root = TestDirectory.Create())
            {
                var userRoot = root.CreateDirectory("user");
                var userTool = Path.Combine(userRoot, "UserTool");
                Directory.CreateDirectory(userTool);

                var systemRoot = root.CreateDirectory("Windows");
                var systemTool = Path.Combine(systemRoot, "System32");
                Directory.CreateDirectory(systemTool);

                var lowVersionSource = typeof(AutoSortPlannerTests).Assembly.Location;
                var highVersionSource = typeof(PathEntry).Assembly.Location;
                var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
                var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

                var lowNumericVersion = new System.Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
                var highNumericVersion = new System.Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
                if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
                {
                    var swap = lowVersionSource;
                    lowVersionSource = highVersionSource;
                    highVersionSource = swap;
                }

                File.Copy(lowVersionSource, Path.Combine(systemTool, "shared.dll"));
                File.Copy(highVersionSource, Path.Combine(userTool, "shared.dll"));

                var previousSystemRoot = System.Environment.GetEnvironmentVariable("TestSystemRoot");
                var previousWindir = System.Environment.GetEnvironmentVariable("TestWindir");
                System.Environment.SetEnvironmentVariable("TestSystemRoot", systemRoot);
                System.Environment.SetEnvironmentVariable("TestWindir", systemRoot);

                try
                {
                    var policy = new PathMigrationPolicy(
                        new[]
                        {
                            PathMigrationPolicy.Variable("TestSystemRoot", systemRoot),
                            PathMigrationPolicy.Variable("TestWindir", systemRoot)
                        },
                        new[] { userRoot },
                        new[] { systemRoot });

                    var plan = AutoSortPlanner.Build(
                        new[] { new PathEntry(systemTool) },
                        new[] { new PathEntry(userTool) },
                        new[] { ".exe" },
                        policy,
                        AutoSortPlannerMode.AggressivePromotion);

                    var afterSystem = plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToArray();
                    var afterUser = plan.AfterAutosort.UserPath.Select(_ => _.SymbolicPath).ToArray();

                    CollectionAssert.AreEqual(
                        new[] { @"%TestSystemRoot%\System32" },
                        afterSystem,
                        "AfterSystem={0}; AfterUser={1}; Demotions={2}; Warnings={3}",
                        string.Join("|", afterSystem),
                        string.Join("|", afterUser),
                        plan.Demotions.Count,
                        string.Join(" || ", plan.Warnings.Select(_ => (_.Path == null ? "" : _.Path.SymbolicPath) + ": " + _.Message).ToArray()));
                    CollectionAssert.AreEqual(
                        new[] { userTool },
                        afterUser);
                    Assert.AreEqual(0, plan.Demotions.Count);

                    var warning = plan.Warnings.Single(_ => _.Path.SymbolicPath == systemTool);
                    Assert.AreEqual(AutoSortWarningKind.SystemDemotionRequiresManualReview, warning.Kind);
                    StringAssert.Contains(warning.Message, "automatic demotion");
                }
                finally
                {
                    System.Environment.SetEnvironmentVariable("TestSystemRoot", previousSystemRoot);
                    System.Environment.SetEnvironmentVariable("TestWindir", previousWindir);
                }
            }
        }

        [TestMethod]
        public void Build_DemotesProgramFilesSystemEntriesWhenHigherVersionUserToolsWin()
        {
            using (var root = TestDirectory.Create())
            {
                var userRoot = root.CreateDirectory("user");
                var userTool = Path.Combine(userRoot, "UserTool");
                Directory.CreateDirectory(userTool);

                var programFiles = root.CreateDirectory("Program Files");
                var systemTool = Path.Combine(programFiles, "SVP 4", "mpv64");
                Directory.CreateDirectory(systemTool);

                var lowVersionSource = typeof(AutoSortPlannerTests).Assembly.Location;
                var highVersionSource = typeof(PathEntry).Assembly.Location;
                var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
                var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

                var lowNumericVersion = new System.Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
                var highNumericVersion = new System.Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
                if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
                {
                    var swap = lowVersionSource;
                    lowVersionSource = highVersionSource;
                    highVersionSource = swap;
                }

                File.Copy(lowVersionSource, Path.Combine(systemTool, "shared.dll"));
                File.Copy(highVersionSource, Path.Combine(userTool, "shared.dll"));

                var previousProgramFiles = System.Environment.GetEnvironmentVariable("TestProgramFiles");
                System.Environment.SetEnvironmentVariable("TestProgramFiles", programFiles);

                try
                {
                    var policy = new PathMigrationPolicy(
                        new[]
                        {
                            PathMigrationPolicy.Variable("TestProgramFiles", programFiles)
                        },
                        new[] { userRoot },
                        new[] { programFiles });

                    var plan = AutoSortPlanner.Build(
                        new[] { new PathEntry(systemTool) },
                        new[] { new PathEntry(userTool) },
                        new[] { ".exe" },
                        policy,
                        AutoSortPlannerMode.AggressivePromotion);

                    Assert.AreEqual(0, plan.AfterAutosort.SystemPath.Count);
                    CollectionAssert.AreEqual(
                        new[] { userTool, @"%TestProgramFiles%\SVP 4\mpv64" },
                        plan.AfterAutosort.UserPath.Select(_ => _.SymbolicPath).ToArray());
                    Assert.AreEqual(1, plan.Demotions.Count);
                    Assert.AreEqual(systemTool, plan.Demotions.Single().Path.SymbolicPath);
                    Assert.IsFalse(plan.Warnings.Any(_ => _.Path != null && _.Path.SymbolicPath == systemTool));
                }
                finally
                {
                    System.Environment.SetEnvironmentVariable("TestProgramFiles", previousProgramFiles);
                }
            }
        }

        [TestMethod]
        public void Build_DeduplicatesDuplicatePromotedUserPathsWithoutThrowing()
        {
            using (var root = TestDirectory.Create())
            {
                var programFiles = root.CreateDirectory("Program Files");
                var toolPath = Path.Combine(programFiles, "Tool");
                Directory.CreateDirectory(toolPath);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new string[0],
                    new[] { programFiles });

                var plan = AutoSortPlanner.Build(
                    new PathEntry[0],
                    new[] { new PathEntry(toolPath), new PathEntry(toolPath) },
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                Assert.AreEqual(1, plan.AfterAutosort.SystemPath.Count);
                StringAssert.Contains(plan.AfterAutosort.SystemPath.Single().SymbolicPath, "Tool");
                Assert.AreEqual(0, plan.AfterAutosort.UserPath.Count);
            }
        }

        [TestMethod]
        public void Build_DeduplicatesDuplicateSystemPathsWithoutThrowing()
        {
            using (var root = TestDirectory.Create())
            {
                var machineRoot = root.CreateDirectory("machine");
                var toolPath = Path.Combine(machineRoot, "Tool");
                Directory.CreateDirectory(toolPath);

                var policy = new PathMigrationPolicy(
                    new KeyValuePair<string, string>[0],
                    new string[0],
                    new[] { machineRoot });

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(toolPath), new PathEntry(toolPath) },
                    new PathEntry[0],
                    new[] { ".exe" },
                    policy,
                    AutoSortPlannerMode.AggressivePromotion);

                CollectionAssert.AreEqual(
                    new[] { toolPath },
                    plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(0, plan.AfterAutosort.UserPath.Count);
            }
        }

        [TestMethod]
        public void Build_RecordsCleanupRemovalsBeforeAutosortPlanning()
        {
            using (var root = TestDirectory.Create())
            {
                var machineRoot = root.CreateDirectory("machine");
                var systemDir = Path.Combine(machineRoot, "system");
                Directory.CreateDirectory(systemDir);
                var missing = Path.Combine(root.Root, "missing");

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(systemDir), new PathEntry(systemDir + @"\") },
                    new[] { new PathEntry(missing) },
                    new[] { ".exe" },
                    new PathMigrationPolicy(
                        new KeyValuePair<string, string>[0],
                        new string[0],
                        new[] { machineRoot }),
                    AutoSortPlannerMode.AggressivePromotion);

                Assert.AreEqual(2, plan.Cleanup.Count);
                Assert.IsTrue(plan.Cleanup.Any(_ =>
                    _.Path.SymbolicPath == systemDir + @"\" &&
                    _.Scope == PathScope.System &&
                    _.Kind == PathCleanupRemovalKind.DuplicateResolvedPath));
                Assert.IsTrue(plan.Cleanup.Any(_ =>
                    _.Path.SymbolicPath == missing &&
                    _.Scope == PathScope.User &&
                    _.Kind == PathCleanupRemovalKind.MissingResolvedPath));

                CollectionAssert.AreEqual(
                    new[] { systemDir },
                    plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(0, plan.AfterAutosort.UserPath.Count);
            }
        }

        [TestMethod]
        public void Build_AlphabetizesConflictFreePathsWithinEachScope()
        {
            using (var root = TestDirectory.Create())
            {
                var machineRoot = root.CreateDirectory("machine");
                var zeta = Path.Combine(machineRoot, "zeta");
                var alpha = Path.Combine(machineRoot, "Alpha");
                var beta = Path.Combine(machineRoot, "beta");
                Directory.CreateDirectory(zeta);
                Directory.CreateDirectory(alpha);
                Directory.CreateDirectory(beta);

                var plan = AutoSortPlanner.Build(
                    new[] { new PathEntry(zeta), new PathEntry(alpha), new PathEntry(beta) },
                    new PathEntry[0],
                    new[] { ".exe" },
                    new PathMigrationPolicy(
                        new KeyValuePair<string, string>[0],
                        new string[0],
                        new[] { machineRoot }),
                    AutoSortPlannerMode.AggressivePromotion);

                CollectionAssert.AreEqual(
                    new[] { alpha, beta, zeta },
                    plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(0, plan.AfterAutosort.UserPath.Count);
            }
        }

        [TestMethod]
        public void Build_AlphabetizesAllConflictFreePathsAcrossConflictAnchors()
        {
            using (var root = TestDirectory.Create())
            {
                var machineRoot = root.CreateDirectory("machine");
                var zeta = Path.Combine(machineRoot, "zeta");
                var low = Path.Combine(machineRoot, "low");
                var beta = Path.Combine(machineRoot, "beta");
                var alpha = Path.Combine(machineRoot, "Alpha");
                var high = Path.Combine(machineRoot, "high");
                Directory.CreateDirectory(zeta);
                Directory.CreateDirectory(low);
                Directory.CreateDirectory(beta);
                Directory.CreateDirectory(alpha);
                Directory.CreateDirectory(high);

                var lowVersionSource = typeof(AutoSortPlannerTests).Assembly.Location;
                var highVersionSource = typeof(PathEntry).Assembly.Location;
                var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
                var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

                var lowNumericVersion = new System.Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
                var highNumericVersion = new System.Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
                if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
                {
                    var swap = lowVersionSource;
                    lowVersionSource = highVersionSource;
                    highVersionSource = swap;
                }

                File.Copy(lowVersionSource, Path.Combine(low, "shared.dll"));
                File.Copy(highVersionSource, Path.Combine(high, "shared.dll"));

                var plan = AutoSortPlanner.Build(
                    new[]
                    {
                        new PathEntry(zeta),
                        new PathEntry(low),
                        new PathEntry(beta),
                        new PathEntry(alpha),
                        new PathEntry(high)
                    },
                    new PathEntry[0],
                    new[] { ".exe" },
                    new PathMigrationPolicy(
                        new KeyValuePair<string, string>[0],
                        new string[0],
                        new[] { machineRoot }),
                    AutoSortPlannerMode.AggressivePromotion);

                var finalOrder = plan.AfterAutosort.SystemPath.Select(_ => _.SymbolicPath).ToList();
                CollectionAssert.AreEqual(
                    new[] { alpha, beta, zeta },
                    finalOrder.Where(_ => _ != low && _ != high).ToArray());
                Assert.IsTrue(finalOrder.IndexOf(high) < finalOrder.IndexOf(low));
            }
        }
    }
}
