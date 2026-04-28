using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathCleanupTests
    {
        [TestMethod]
        public void Clean_PreservesUnresolvableEntriesAndRemovesMissingAndDuplicateResolvedPaths()
        {
            var root = TestDirectory.Create();
            var shared = root.CreateDirectory("shared");
            var userOnly = root.CreateDirectory("user-only");
            var missing = Path.Combine(root.Root, "missing");

            var systemPath = new[]
            {
                new PathEntry("%PATH%"),
                new PathEntry(shared),
                new PathEntry(shared + @"\")
            };
            var userPath = new[]
            {
                new PathEntry(shared),
                new PathEntry(missing),
                new PathEntry(userOnly)
            };

            var cleaned = PathCleanup.Clean(systemPath, userPath);

            CollectionAssert.AreEqual(
                new[] { "%PATH%", shared },
                cleaned.SystemPath.Select(_ => _.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(
                new[] { userOnly },
                cleaned.UserPath.Select(_ => _.SymbolicPath).ToArray());
        }

        [TestMethod]
        public void Clean_ReportsRemovedEntriesWithScopeAndReason()
        {
            var root = TestDirectory.Create();
            var shared = root.CreateDirectory("shared");
            var missing = Path.Combine(root.Root, "missing");

            var cleaned = PathCleanup.Clean(
                new[] { new PathEntry(shared) },
                new[] { new PathEntry(shared), new PathEntry(missing) });

            Assert.AreEqual(2, cleaned.RemovedEntries.Count);

            var duplicate = cleaned.RemovedEntries.Single(_ => _.Path.SymbolicPath == shared && _.Scope == PathScope.User);
            Assert.AreEqual(PathCleanupRemovalKind.DuplicateResolvedPath, duplicate.Kind);

            var missingEntry = cleaned.RemovedEntries.Single(_ => _.Path.SymbolicPath == missing && _.Scope == PathScope.User);
            Assert.AreEqual(PathCleanupRemovalKind.MissingResolvedPath, missingEntry.Kind);
        }
    }
}
