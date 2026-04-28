using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathRegistryTests
    {
        [TestMethod]
        public void MergeExecutableExtensions_KeepsStableOrderAndRemovesCaseInsensitiveDuplicates()
        {
            var merged = PathRegistry.MergeExecutableExtensions(
                new[] { ".COM", ".EXE", ".CMD" },
                new[] { ".cmd", ".DLL", ".exe", ".BAT" });

            CollectionAssert.AreEqual(
                new[] { ".COM", ".EXE", ".CMD", ".DLL", ".BAT" },
                new System.Collections.Generic.List<string>(merged));
        }
    }
}
