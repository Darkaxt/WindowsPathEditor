using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathBackupExporterTests
    {
        [TestMethod]
        public void CreateBackupFileName_UsesTimestampFormat()
        {
            var fileName = PathBackupExporter.CreateBackupFileName(new System.DateTime(2026, 3, 28, 17, 5, 9));

            Assert.AreEqual("path_backup_260328_170509.reg", fileName);
        }

        [TestMethod]
        public void BuildRegFileContents_ExportsOnlySystemAndUserPathAsExpandStrings()
        {
            var contents = PathBackupExporter.BuildRegFileContents(
                new[] { new PathEntry("A"), new PathEntry("B") },
                new[] { new PathEntry("C") });

            StringAssert.Contains(contents, "Windows Registry Editor Version 5.00");
            StringAssert.Contains(contents, @"[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment]");
            StringAssert.Contains(contents, @"[HKEY_CURRENT_USER\Environment]");
            StringAssert.Contains(contents, "\"Path\"=hex(2):41,00,3b,00,42,00,00,00");
            StringAssert.Contains(contents, "\"Path\"=hex(2):43,00,00,00");
            Assert.IsFalse(contents.Contains("\"PATHEXT\""));
        }
    }
}
