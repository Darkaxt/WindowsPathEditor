using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathApplyServiceTests
    {
        [TestMethod]
        public void Apply_UserOnlyChange_WritesOnlyUserScope()
        {
            var current = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var expected = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"%LocalAppData%\Microsoft\WindowsApps") });

            bool? includeSystem = null;
            bool? includeUser = null;

            var service = new PathApplyService(
                () => expected,
                (systemPath, userPath, timestamp, writeSystem, writeUser) =>
                {
                    includeSystem = writeSystem;
                    includeUser = writeUser;
                    return "backup.reg";
                },
                (systemPath, userPath, timestamp, writeSystem, writeUser) =>
                {
                    includeSystem = writeSystem;
                    includeUser = writeUser;
                    return "apply.reg";
                },
                (filePath, elevated) => PathImportExecutionResult.Success(),
                () => { },
                () => new DateTime(2026, 3, 29, 6, 0, 0));

            var result = service.Apply(current, expected, false);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(false, includeSystem);
            Assert.AreEqual(true, includeUser);
        }

        [TestMethod]
        public void Apply_WhenImportFails_ReturnsFailureWithoutVerification()
        {
            var current = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var expected = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32"), new PathEntry(@"C:\Program Files\PowerShell\7") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var verificationCalled = false;

            var service = new PathApplyService(
                () =>
                {
                    verificationCalled = true;
                    return current;
                },
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "backup.reg",
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "apply.reg",
                (filePath, elevated) => PathImportExecutionResult.Failure("reg import failed."),
                () => { },
                () => new DateTime(2026, 3, 29, 6, 0, 0));

            var result = service.Apply(current, expected, true);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(verificationCalled);
            Assert.AreEqual("backup.reg", result.BackupPath);
            Assert.AreEqual("apply.reg", result.ApplyPath);
            StringAssert.Contains(result.ErrorMessage, "reg import failed");
        }

        [TestMethod]
        public void Apply_WhenVerificationFails_ReturnsFailureWithActualState()
        {
            var current = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var expected = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32"), new PathEntry(@"C:\Program Files\PowerShell\7") },
                new[] { new PathEntry(@"%LocalAppData%\Microsoft\WindowsApps") });
            var actual = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"%LocalAppData%\Microsoft\WindowsApps") });

            var service = new PathApplyService(
                () => actual,
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "backup.reg",
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "apply.reg",
                (filePath, elevated) => PathImportExecutionResult.Success(),
                () => { },
                () => new DateTime(2026, 3, 29, 6, 0, 0));

            var result = service.Apply(current, expected, true);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.SystemMatches);
            Assert.IsTrue(result.UserMatches);
            Assert.IsNotNull(result.ActualState);
            StringAssert.Contains(result.ErrorMessage, "did not match");
        }

        [TestMethod]
        public void Apply_WhenApplyFileCreationThrows_ReturnsFailureAndPreservesBackupPath()
        {
            var current = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var expected = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32"), new PathEntry(@"C:\Program Files\PowerShell\7") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });

            var service = new PathApplyService(
                () => expected,
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "backup.reg",
                (systemPath, userPath, timestamp, writeSystem, writeUser) => throw new InvalidOperationException("cannot write apply file"),
                (filePath, elevated) => PathImportExecutionResult.Success(),
                () => { },
                () => new DateTime(2026, 3, 29, 6, 0, 0));

            var result = service.Apply(current, expected, true);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("backup.reg", result.BackupPath);
            Assert.IsNull(result.ApplyPath);
            StringAssert.Contains(result.ErrorMessage, "cannot write apply file");
        }

        [TestMethod]
        public void Apply_WhenImportAndVerificationSucceed_ReturnsSuccessAndNotifiesEnvironment()
        {
            var current = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32") },
                new[] { new PathEntry(@"C:\Users\darka\AppData\Local\Microsoft\WindowsApps") });
            var expected = new PathStateSnapshot(
                new[] { new PathEntry(@"C:\Windows\system32"), new PathEntry(@"C:\Program Files\PowerShell\7") },
                new[] { new PathEntry(@"%LocalAppData%\Microsoft\WindowsApps") });
            var notifications = 0;

            var service = new PathApplyService(
                () => expected,
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "backup.reg",
                (systemPath, userPath, timestamp, writeSystem, writeUser) => "apply.reg",
                (filePath, elevated) => PathImportExecutionResult.Success(),
                () => notifications++,
                () => new DateTime(2026, 3, 29, 6, 0, 0));

            var result = service.Apply(current, expected, true);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual("backup.reg", result.BackupPath);
            Assert.AreEqual("apply.reg", result.ApplyPath);
        }
    }
}
