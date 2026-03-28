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
    }
}
