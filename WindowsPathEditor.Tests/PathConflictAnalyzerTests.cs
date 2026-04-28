using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowsPathEditor.Tests
{
    [TestClass]
    public class PathConflictAnalyzerTests
    {
        [TestMethod]
        public void BuildReport_GroupsByExactParticipantSetAndPreservesSearchOrder()
        {
            var root = TestDirectory.Create();
            var alpha = root.CreateDirectory("alpha");
            var beta = root.CreateDirectory("beta");
            var gamma = root.CreateDirectory("gamma");

            root.CreateFile(alpha, "shared-ab-1.exe");
            root.CreateFile(alpha, "shared-ab-2.dll");
            root.CreateFile(beta, "shared-ab-1.exe");
            root.CreateFile(beta, "shared-ab-2.dll");
            root.CreateFile(beta, "shared-bc.dll");
            root.CreateFile(gamma, "shared-bc.dll");

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(alpha),
                    new PathEntry(beta),
                    new PathEntry(gamma)
                },
                new[] { ".exe" });

            Assert.AreEqual(2, report.Groups.Count);

            CollectionAssert.AreEqual(
                new[] { alpha, beta },
                report.Groups[0].Columns.Select(_ => _.Path.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(
                new[] { "shared-ab-1.exe", "shared-ab-2.dll" },
                report.Groups[0].Rows.Select(_ => _.Filename).ToArray());

            CollectionAssert.AreEqual(
                new[] { beta, gamma },
                report.Groups[1].Columns.Select(_ => _.Path.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(
                new[] { "shared-bc.dll" },
                report.Groups[1].Rows.Select(_ => _.Filename).ToArray());

            CollectionAssert.AreEqual(
                new[] { "shared-ab-1.exe", "shared-ab-2.dll" },
                report.Graph.Edges.Single(_ => _.LeftPathIndex == 0 && _.RightPathIndex == 1).Filenames.ToArray());
            CollectionAssert.AreEqual(
                new[] { "shared-bc.dll" },
                report.Graph.Edges.Single(_ => _.LeftPathIndex == 1 && _.RightPathIndex == 2).Filenames.ToArray());
            Assert.IsFalse(report.Graph.Edges.Any(_ => _.LeftPathIndex == 0 && _.RightPathIndex == 2));
        }

        [TestMethod]
        public void BuildReport_UsesSearchOrderForGroupColumnsAndShowsBlankOrNaForCells()
        {
            var root = TestDirectory.Create();
            var unique = root.CreateDirectory("unique");
            var alpha = root.CreateDirectory("alpha");
            var beta = root.CreateDirectory("beta");
            var gamma = root.CreateDirectory("gamma");

            root.CreateFile(unique, "single.exe");
            root.CreateFile(alpha, "alpha.exe");
            root.CreateFile(beta, "beta.dll");
            root.CreateFile(gamma, "alpha.exe");
            root.CreateFile(gamma, "beta.dll");

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(unique),
                    new PathEntry(alpha),
                    new PathEntry(beta),
                    new PathEntry(gamma)
                },
                new[] { ".exe" });

            Assert.AreEqual(2, report.Groups.Count);

            var alphaGroup = report.Groups.Single(_ => _.Columns.Select(column => column.Path.SymbolicPath)
                .SequenceEqual(new[] { alpha, gamma }));
            CollectionAssert.AreEqual(
                new[] { alpha, gamma },
                alphaGroup.Columns.Select(_ => _.Path.SymbolicPath).ToArray());
            CollectionAssert.AreEqual(
                new[] { "alpha.exe" },
                alphaGroup.Rows.Select(_ => _.Filename).ToArray());

            var alphaRow = alphaGroup.Rows.Single(_ => _.Filename == "alpha.exe");
            Assert.AreEqual("n/a", alphaRow.Cells[0].DisplayValue);
            Assert.AreEqual("n/a", alphaRow.Cells[1].DisplayValue);

            var betaGroup = report.Groups.Single(_ => _.Columns.Select(column => column.Path.SymbolicPath)
                .SequenceEqual(new[] { beta, gamma }));
            CollectionAssert.AreEqual(
                new[] { "beta.dll" },
                betaGroup.Rows.Select(_ => _.Filename).ToArray());

            var betaRow = betaGroup.Rows.Single(_ => _.Filename == "beta.dll");
            Assert.AreEqual("n/a", betaRow.Cells[0].DisplayValue);
            Assert.AreEqual("n/a", betaRow.Cells[1].DisplayValue);
            Assert.AreEqual(PathConflictWinnerState.Unknown, betaRow.WinnerState);
        }

        [TestMethod]
        public void BuildReport_FlagsWhenCurrentWinnerDoesNotHaveHighestVersion()
        {
            var root = TestDirectory.Create();
            var first = root.CreateDirectory("first");
            var second = root.CreateDirectory("second");

            var lowVersionSource = typeof(PathConflictAnalyzerTests).Assembly.Location;
            var highVersionSource = typeof(PathEntry).Assembly.Location;
            var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
            var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

            var lowNumericVersion = new Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
            var highNumericVersion = new Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
            if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
            {
                var swap = lowVersionSource;
                lowVersionSource = highVersionSource;
                highVersionSource = swap;
            }

            File.Copy(lowVersionSource, Path.Combine(first, "shared.dll"));
            File.Copy(highVersionSource, Path.Combine(second, "shared.dll"));

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(first),
                    new PathEntry(second)
                },
                new[] { ".exe" });

            var row = report.Groups.Single().Rows.Single(_ => _.Filename == "shared.dll");
            Assert.AreEqual(PathConflictWinnerState.ShadowedByHigherVersion, row.WinnerState);
            Assert.IsTrue(row.Cells[0].IsRuntimeWinner);
            Assert.IsFalse(row.Cells[0].IsHighestVersion);
            Assert.IsTrue(row.Cells[1].IsHighestVersion);
            Assert.IsTrue(row.Cells[1].HasComparableVersion);
        }

        [TestMethod]
        public void BuildReport_HidesRowsWhenAllComparableVersionsAreIdentical()
        {
            var root = TestDirectory.Create();
            var first = root.CreateDirectory("first");
            var second = root.CreateDirectory("second");
            var source = typeof(PathEntry).Assembly.Location;

            File.Copy(source, Path.Combine(first, "shared.dll"));
            File.Copy(source, Path.Combine(second, "shared.dll"));

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(first),
                    new PathEntry(second)
                },
                new[] { ".exe" });

            Assert.AreEqual(0, report.Groups.Count);
            Assert.IsFalse(report.HasConflicts);
            Assert.AreEqual(0, report.Graph.Edges.Count);
            Assert.AreEqual(0, report.ConflictFilesByPathIndex.Count);
        }

        [TestMethod]
        public void BuildReport_KeepsOnlyRowsWithMeaningfulVersionDifferencesWithinAGroup()
        {
            var root = TestDirectory.Create();
            var first = root.CreateDirectory("first");
            var second = root.CreateDirectory("second");
            var sameVersionSource = typeof(PathEntry).Assembly.Location;
            var lowVersionSource = typeof(PathConflictAnalyzerTests).Assembly.Location;
            var highVersionSource = typeof(PathEntry).Assembly.Location;
            var lowVersion = FileVersionInfo.GetVersionInfo(lowVersionSource);
            var highVersion = FileVersionInfo.GetVersionInfo(highVersionSource);

            var lowNumericVersion = new Version(lowVersion.FileMajorPart, lowVersion.FileMinorPart, lowVersion.FileBuildPart, lowVersion.FilePrivatePart);
            var highNumericVersion = new Version(highVersion.FileMajorPart, highVersion.FileMinorPart, highVersion.FileBuildPart, highVersion.FilePrivatePart);
            if (lowNumericVersion.CompareTo(highNumericVersion) > 0)
            {
                var swap = lowVersionSource;
                lowVersionSource = highVersionSource;
                highVersionSource = swap;
            }

            File.Copy(sameVersionSource, Path.Combine(first, "same.dll"));
            File.Copy(sameVersionSource, Path.Combine(second, "same.dll"));
            File.Copy(lowVersionSource, Path.Combine(first, "different.dll"));
            File.Copy(highVersionSource, Path.Combine(second, "different.dll"));

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(first),
                    new PathEntry(second)
                },
                new[] { ".exe" });

            Assert.AreEqual(1, report.Groups.Count);
            CollectionAssert.AreEqual(
                new[] { "different.dll" },
                report.Groups.Single().Rows.Select(_ => _.Filename).ToArray());
            CollectionAssert.AreEqual(
                new[] { "different.dll" },
                report.Graph.Edges.Single().Filenames.ToArray());
            CollectionAssert.AreEqual(
                new[] { "different.dll" },
                report.ConflictFilesByPathIndex[0].ToArray());
            CollectionAssert.AreEqual(
                new[] { "different.dll" },
                report.ConflictFilesByPathIndex[1].ToArray());
        }

        [TestMethod]
        public void BuildReport_CollapsesDuplicateDirectoriesBeforeCreatingGroups()
        {
            var root = TestDirectory.Create();
            var repeated = root.CreateDirectory("repeated");

            root.CreateFile(repeated, "vcpkg.exe");

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(repeated),
                    new PathEntry(repeated + "\\"),
                    new PathEntry(repeated)
                },
                new[] { ".exe" });

            Assert.IsFalse(report.HasConflicts);
            Assert.AreEqual(0, report.Groups.Count);
        }

        [TestMethod]
        public void BuildReport_TracksWhetherColumnsComeFromSystemOrUserPath()
        {
            var root = TestDirectory.Create();
            var systemDir = root.CreateDirectory("system");
            var userDir = root.CreateDirectory("user");

            root.CreateFile(systemDir, "shared.exe");
            root.CreateFile(userDir, "shared.exe");

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(systemDir),
                    new PathEntry(userDir)
                },
                new[] { ".exe" },
                1);

            var group = report.Groups.Single();
            Assert.AreEqual(PathConflictColumnOrigin.System, group.Columns[0].Origin);
            Assert.AreEqual(PathConflictColumnOrigin.User, group.Columns[1].Origin);
        }

        [TestMethod]
        public void BuildReport_TracksWinningLosingAndMixedConflictPaths()
        {
            var root = TestDirectory.Create();
            var alpha = root.CreateDirectory("alpha");
            var beta = root.CreateDirectory("beta");
            var gamma = root.CreateDirectory("gamma");

            root.CreateFile(alpha, "shared-ab.exe");
            root.CreateFile(beta, "shared-ab.exe");
            root.CreateFile(beta, "shared-bc.dll");
            root.CreateFile(gamma, "shared-bc.dll");

            var report = PathConflictAnalyzer.BuildReport(
                new[]
                {
                    new PathEntry(alpha),
                    new PathEntry(beta),
                    new PathEntry(gamma)
                },
                new[] { ".exe" });

            Assert.AreEqual(ConflictWinStatus.Winning, report.WinStatusByPathIndex[0]);
            Assert.AreEqual(ConflictWinStatus.Mixed, report.WinStatusByPathIndex[1]);
            Assert.AreEqual(ConflictWinStatus.Losing, report.WinStatusByPathIndex[2]);
        }
    }
}
