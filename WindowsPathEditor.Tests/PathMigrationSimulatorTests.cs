using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathMigrationSimulatorTests
    {
        [TestMethod]
        public void Simulate_NormalizesKnownUserPathsWithoutPromotingThem()
        {
            using (var root = TestDirectory.Create())
            {
                var userProfile = root.CreateDirectory("UserProfile");
                var localAppData = Path.Combine(userProfile, "AppData", "Local");
                var toolPath = Path.Combine(localAppData, "Programs", "MyTool");
                Directory.CreateDirectory(toolPath);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("UserProfile", userProfile),
                        PathMigrationPolicy.Variable("LocalAppData", localAppData)
                    },
                    new[] { userProfile, localAppData },
                    new string[0]);

                var result = PathMigrationSimulator.Simulate(
                    new PathEntry[0],
                    new[] { new PathEntry(toolPath) },
                    new[] { ".exe" },
                    policy);

                var entry = result.Entries.Single();
                Assert.AreEqual(PathScope.User, entry.OriginalScope);
                Assert.AreEqual(PathScope.User, entry.ProposedScope);
                Assert.IsTrue(entry.IsNormalized);
                Assert.IsFalse(entry.IsPromotedToSystem);
                Assert.IsFalse(entry.RequiresManualReview);
                Assert.AreEqual(@"%LocalAppData%\Programs\MyTool", entry.ProposedPath.SymbolicPath);
            }
        }

        [TestMethod]
        public void Simulate_PromotesMachineOwnedUserPathsToSystem()
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

                var result = PathMigrationSimulator.Simulate(
                    new PathEntry[0],
                    new[] { new PathEntry(toolPath) },
                    new[] { ".exe" },
                    policy);

                var entry = result.Entries.Single();
                Assert.AreEqual(PathScope.System, entry.ProposedScope);
                Assert.IsTrue(entry.IsPromotedToSystem);
                Assert.AreEqual(@"%ProgramFiles%\Tool", entry.ProposedPath.SymbolicPath);
                CollectionAssert.AreEqual(
                    new[] { @"%ProgramFiles%\Tool" },
                    result.SimulatedSystemPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(0, result.SimulatedUserPath.Count);
            }
        }

        [TestMethod]
        public void Simulate_FlagsCustomRootsForManualReviewInsteadOfPromoting()
        {
            using (var root = TestDirectory.Create())
            {
                var userProfile = root.CreateDirectory("UserProfile");
                var programFiles = root.CreateDirectory("Program Files");
                var customRoot = root.CreateDirectory("Downloads");
                var customTool = Path.Combine(customRoot, "Programs", "PortableTool");
                Directory.CreateDirectory(customTool);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("UserProfile", userProfile),
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new[] { userProfile },
                    new[] { programFiles });

                var result = PathMigrationSimulator.Simulate(
                    new PathEntry[0],
                    new[] { new PathEntry(customTool) },
                    new[] { ".exe" },
                    policy);

                var entry = result.Entries.Single();
                Assert.AreEqual(PathOwnership.Custom, entry.Ownership);
                Assert.AreEqual(PathScope.User, entry.ProposedScope);
                Assert.IsFalse(entry.IsPromotedToSystem);
                Assert.IsTrue(entry.RequiresManualReview);
            }
        }

        [TestMethod]
        public void Simulate_WithAggressiveAutosortPolicy_PromotesResolvedCustomRoots()
        {
            using (var root = TestDirectory.Create())
            {
                var userProfile = root.CreateDirectory("UserProfile");
                var programFiles = root.CreateDirectory("Program Files");
                var customRoot = root.CreateDirectory("Downloads");
                var customTool = Path.Combine(customRoot, "Programs", "PortableTool");
                Directory.CreateDirectory(customTool);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("UserProfile", userProfile),
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new[] { userProfile },
                    new[] { programFiles });

                var result = PathMigrationSimulator.Simulate(
                    new PathEntry[0],
                    new[] { new PathEntry(customTool) },
                    new[] { ".exe" },
                    policy,
                    PathMigrationSimulationMode.AggressiveAutosort);

                var entry = result.Entries.Single();
                Assert.AreEqual(PathOwnership.Custom, entry.Ownership);
                Assert.AreEqual(PathScope.System, entry.ProposedScope);
                Assert.IsTrue(entry.IsPromotedToSystem);
                Assert.IsFalse(entry.RequiresManualReview);
                CollectionAssert.AreEqual(
                    new[] { customTool },
                    result.SimulatedSystemPath.Select(_ => _.SymbolicPath).ToArray());
                Assert.AreEqual(0, result.SimulatedUserPath.Count);
            }
        }

        [TestMethod]
        public void Simulate_DeduplicatesWhenSamePathExistsInBothSystemAndUser()
        {
            using (var root = TestDirectory.Create())
            {
                var programFiles = root.CreateDirectory("Program Files");
                var sharedTool = Path.Combine(programFiles, "SharedTool");
                Directory.CreateDirectory(sharedTool);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new string[0],
                    new[] { programFiles });

                // sharedTool lives in BOTH system and user PATH
                var result = PathMigrationSimulator.Simulate(
                    new[] { new PathEntry(sharedTool) },
                    new[] { new PathEntry(sharedTool) },
                    new[] { ".exe" },
                    policy);

                // After simulation the normalised/promoted version appears in system exactly once
                var systemSymbolicPaths = result.SimulatedSystemPath.Select(_ => _.SymbolicPath).ToArray();
                Assert.AreEqual(1, systemSymbolicPaths.Length,
                    "Path should appear exactly once in SimulatedSystemPath, not duplicated.");

                // It must NOT also appear in the user list
                var userSymbolicPaths = result.SimulatedUserPath.Select(_ => _.SymbolicPath).ToArray();
                Assert.AreEqual(0, userSymbolicPaths.Length,
                    "Path that was promoted/already in system must not also appear in SimulatedUserPath.");
            }
        }

        [TestMethod]
        public void Simulate_PromotedEntriesAreAppendedAlphabeticallyAfterOriginalSystemEntries()
        {
            using (var root = TestDirectory.Create())
            {
                var programFiles = root.CreateDirectory("Program Files");
                var alphaDir = Path.Combine(programFiles, "Alpha");
                var betaDir = Path.Combine(programFiles, "Beta");
                var gammaDir = Path.Combine(programFiles, "Gamma");
                Directory.CreateDirectory(alphaDir);
                Directory.CreateDirectory(betaDir);
                Directory.CreateDirectory(gammaDir);

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new string[0],
                    new[] { programFiles });

                // System has Gamma; user has Beta then Alpha (both machine-owned → will be promoted)
                var result = PathMigrationSimulator.Simulate(
                    new[] { new PathEntry(gammaDir) },
                    new[] { new PathEntry(betaDir), new PathEntry(alphaDir) },
                    new[] { ".exe" },
                    policy);

                var systemPaths = result.SimulatedSystemPath.Select(_ => _.SymbolicPath).ToArray();
                // Gamma stays first (original system order), then Alpha, Beta appended alphabetically
                Assert.AreEqual(3, systemPaths.Length);
                StringAssert.Contains(systemPaths[0], "Gamma");
                StringAssert.Contains(systemPaths[1], "Alpha");
                StringAssert.Contains(systemPaths[2], "Beta");

                Assert.AreEqual(0, result.SimulatedUserPath.Count,
                    "All machine-owned paths promoted; user list should be empty.");
            }
        }

        [TestMethod]
        public void Simulate_ReducesMixedScopeConflictsWhenMachineOwnedUserPathsMoveToSystem()
        {
            using (var root = TestDirectory.Create())
            {
                var systemDir = root.CreateDirectory("system");
                var programFiles = root.CreateDirectory("Program Files");
                var userMachineDir = Path.Combine(programFiles, "Zulu");
                Directory.CreateDirectory(userMachineDir);

                root.CreateFile(systemDir, "shared.exe");
                root.CreateFile(userMachineDir, "shared.exe");

                var policy = new PathMigrationPolicy(
                    new[]
                    {
                        PathMigrationPolicy.Variable("ProgramFiles", programFiles)
                    },
                    new string[0],
                    new[] { programFiles });

                var result = PathMigrationSimulator.Simulate(
                    new[] { new PathEntry(systemDir) },
                    new[] { new PathEntry(userMachineDir) },
                    new[] { ".exe" },
                    policy);

                Assert.AreEqual(1, result.BeforeConflicts.MixedScopeGroupCount);
                Assert.AreEqual(0, result.AfterMigrationConflicts.MixedScopeGroupCount);
            }
        }
    }
}
