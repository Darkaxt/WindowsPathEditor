using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace WindowsPathEditor
{
    internal static class RegistryImportRunner
    {
        private const int ImportTimeoutMilliseconds = 10000;

        internal static PathImportExecutionResult Import(string regFilePath, bool elevated)
        {
            if (string.IsNullOrEmpty(regFilePath))
            {
                throw new ArgumentException("A .reg file path is required.", "regFilePath");
            }

            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "reg.exe"),
                Arguments = string.Format("import \"{0}\"", regFilePath),
                UseShellExecute = elevated
            };

            if (elevated)
            {
                info.Verb = "runas";
            }

            try
            {
                using (var process = Process.Start(info))
                {
                    if (process == null)
                    {
                        return PathImportExecutionResult.Failure("The registry import process could not be started.");
                    }

                    process.WaitForExit(ImportTimeoutMilliseconds);
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                        }

                        return PathImportExecutionResult.Failure("The registry import timed out.");
                    }

                    return process.ExitCode == 0
                        ? PathImportExecutionResult.Success()
                        : new PathImportExecutionResult(true, process.ExitCode, "The registry import exited with code " + process.ExitCode + ".");
                }
            }
            catch (Win32Exception ex)
            {
                return PathImportExecutionResult.Failure(ex.Message);
            }
        }
    }
}
