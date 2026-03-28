using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class ScanImportPlannerTests
    {
        [TestMethod]
        public void SelectPathsToImport_ReturnsOnlyCheckedNonDuplicatePaths()
        {
            var selected = ScanImportPlanner.SelectPathsToImport(
                new[]
                {
                    new SelectablePath(@"C:\tools\alpha", true),
                    new SelectablePath(@"C:\tools\beta", false),
                    new SelectablePath(@"C:\tools\current", true)
                },
                new[] { new PathEntry(@"C:\tools\current") })
                .ToArray();

            Assert.AreEqual(1, selected.Length);
            Assert.AreEqual(Path.GetFullPath(@"C:\tools\alpha"), selected[0].Resolve().ActualPath);
        }
    }
}
