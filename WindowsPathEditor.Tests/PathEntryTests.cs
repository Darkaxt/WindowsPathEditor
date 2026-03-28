using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathEntryTests
    {
        [TestMethod]
        public void Resolve_WithEnvironmentPathVariable_DoesNotThrowAndReportsError()
        {
            var entry = new PathEntry("%PATH%");

            var resolution = entry.Resolve();

            Assert.IsFalse(resolution.IsResolved);
            Assert.IsFalse(entry.Exists);
            Assert.IsFalse(string.IsNullOrEmpty(resolution.ErrorMessage));
            Assert.AreEqual(entry, new PathEntry("%PATH%"));
            Assert.AreNotEqual(0, entry.GetHashCode());
        }

        [TestMethod]
        public void FromFilePath_PrefersMoreSpecificEnvironmentVariableWhenValuesOverlap()
        {
            var environment = new Dictionary<string, string>
            {
                { "ProgramFiles", @"C:\Program Files (x86)" },
                { "ProgramFiles(x86)", @"C:\Program Files (x86)" }
            };

            var entry = PathEntry.FromFilePath(@"C:\Program Files (x86)\Inno Setup 6", environment);

            Assert.AreEqual(@"%ProgramFiles(x86)%\Inno Setup 6", entry.SymbolicPath);
        }
    }
}
