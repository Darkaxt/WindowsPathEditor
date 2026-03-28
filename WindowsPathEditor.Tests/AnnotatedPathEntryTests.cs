using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class AnnotatedPathEntryTests
    {
        [TestMethod]
        public void NewEntry_StartsPendingUntilValidationCompletes()
        {
            var entry = new AnnotatedPathEntry(new PathEntry(@"C:\Temp"));

            Assert.AreEqual(-1, entry.AlertLevel);
            Assert.AreEqual("Validation pending", entry.StatusSummary);

            entry.SetStatus(new string[0], false, new string[0]);

            Assert.AreEqual(0, entry.AlertLevel);
            Assert.AreEqual(string.Empty, entry.StatusSummary);
        }

        [TestMethod]
        public void BeginValidation_ClearsStaleStatusAndReturnsToPending()
        {
            var entry = new AnnotatedPathEntry(new PathEntry(@"C:\Temp"));
            entry.SetStatus(new[] { "Does not exist" }, true, new[] { "tool.dll" });

            entry.BeginValidation();

            Assert.AreEqual(-1, entry.AlertLevel);
            Assert.AreEqual("Validation pending", entry.StatusSummary);
            Assert.IsFalse(entry.HasConflicts);
            Assert.IsFalse(entry.SeriousError);
        }
    }
}
