using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WindowsPathEditor
{
    internal static class CliRunner
    {
        public static int Run(CliCommandLine commandLine)
        {
            if (commandLine == null) throw new ArgumentNullException("commandLine");

            try
            {
                switch (commandLine.Command)
                {
                    case CliCommand.Paths:
                        return RunPaths(commandLine);
                    case CliCommand.Autosort:
                        return RunAutosort(commandLine);
                    case CliCommand.Cleanup:
                        return RunCleanup(commandLine);
                    case CliCommand.Conflicts:
                        return RunConflicts(commandLine);
                    case CliCommand.Migrate:
                        return RunMigrate(commandLine);
                    case CliCommand.Scan:
                        return RunScan(commandLine);
                    default:
                        Console.Error.WriteLine("The requested CLI command is not implemented yet.");
                        Console.Error.WriteLine(CliCommandLine.Usage);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int RunPaths(CliCommandLine commandLine)
        {
            var payload = LoadPathsPayload(commandLine, new PathRegistry());

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatPaths(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatPaths(payload));
            }

            return 0;
        }

        private static int RunCleanup(CliCommandLine commandLine)
        {
            var payload = CliAnalysisPayloadFactory.CreateCleanup(LoadPathsPayload(commandLine, new PathRegistry()));

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatCleanup(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatCleanup(payload));
            }

            return 0;
        }

        private static int RunAutosort(CliCommandLine commandLine)
        {
            var registry = new PathRegistry();
            var payload = CliAnalysisPayloadFactory.CreateAutosort(
                LoadPathsPayload(commandLine, registry),
                registry.ExecutableExtensions);

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatAutosort(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatAutosort(payload));
            }

            return 0;
        }

        private static int RunConflicts(CliCommandLine commandLine)
        {
            var registry = new PathRegistry();
            var paths = LoadPathsPayload(commandLine, registry);
            var payload = CliAnalysisPayloadFactory.CreateConflicts(paths, registry.ExecutableExtensions);

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatConflicts(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatConflicts(payload));
            }

            return 0;
        }

        private static int RunMigrate(CliCommandLine commandLine)
        {
            var registry = new PathRegistry();
            var payload = CliAnalysisPayloadFactory.CreateMigrate(
                LoadPathsPayload(commandLine, registry),
                registry.ExecutableExtensions,
                registry.IsSystemPathWritable);

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatMigrate(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatMigrate(payload));
            }

            return 0;
        }

        private static int RunScan(CliCommandLine commandLine)
        {
            var payload = BuildScanPayload(commandLine, LoadPathsPayload(commandLine, new PathRegistry()));

            if (commandLine.Json)
            {
                Console.Out.WriteLine(CliJsonFormatter.FormatScan(payload));
            }
            else
            {
                Console.Out.WriteLine(CliTextFormatter.FormatScan(payload));
            }

            return 0;
        }

        internal static CliScanPayload BuildScanPayload(CliCommandLine commandLine, CliPathsPayload paths)
        {
            if (commandLine == null)
            {
                throw new ArgumentNullException("commandLine");
            }

            if (paths == null)
            {
                throw new ArgumentNullException("paths");
            }

            if (!Directory.Exists(commandLine.RootPath))
            {
                throw new DirectoryNotFoundException("Scan root does not exist: " + commandLine.RootPath);
            }

            var existingPath = (paths.Parsed == null ? Enumerable.Empty<string>() : paths.Parsed.EffectivePath).ToList();
            var existingEntries = existingPath.Select(path => new PathEntry(path)).ToList();
            var currentPath = new HashSet<PathEntry>(existingEntries);
            var search = new SearchOperation(commandLine.RootPath, commandLine.Depth, new CliNullProgressReporter());

            var results = search.Run()
                .Select(path => new CliScanResultPayload
                {
                    Path = path,
                    WouldAdd = currentPath.Add(PathEntry.FromFilePath(path))
                })
                .ToList();

            return new CliScanPayload
            {
                Source = paths.Source ?? "scan",
                Root = commandLine.RootPath,
                Depth = commandLine.Depth,
                CurrentPath = existingPath,
                Results = results
            };
        }

        private static CliPathsPayload LoadPathsPayload(CliCommandLine commandLine, PathRegistry registry)
        {
            if (commandLine.HasInput)
            {
                return CliPathSource.LoadSnapshot(CliInputSnapshot.Load(commandLine.InputPath));
            }

            return CliPathSource.LoadLive(registry);
        }
    }
}
