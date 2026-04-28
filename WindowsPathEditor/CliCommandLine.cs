using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public enum CliCommand
    {
        Paths,
        Conflicts,
        Autosort,
        Cleanup,
        Migrate,
        Scan
    }

    public sealed class CliCommandLine
    {
        private CliCommandLine()
        {
            RootPath = @"C:\";
            Depth = 4;
        }

        public CliCommand Command { get; private set; }

        public bool Json { get; private set; }

        public string InputPath { get; private set; }

        public bool HasInput { get; private set; }

        public string RootPath { get; private set; }

        public bool HasRoot { get; private set; }

        public int Depth { get; private set; }

        public bool HasDepth { get; private set; }

        public string ErrorMessage { get; private set; }

        public static string Usage
        {
            get
            {
                return "Usage: WindowsPathEditor.exe /cli <paths|conflicts|autosort|cleanup|migrate|scan> [/json] [/input <snapshot.json>] [/root <path>] [/depth <n>]";
            }
        }

        public static bool IsCliRequest(IEnumerable<string> args)
        {
            return (args ?? Enumerable.Empty<string>())
                .Any(token => string.Equals(token, "/cli", StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryParse(IEnumerable<string> args, out CliCommandLine parsed, out string error)
        {
            parsed = null;
            error = null;

            var tokens = (args ?? Enumerable.Empty<string>()).ToList();
            if (tokens.Count == 0)
            {
                error = "Missing /cli switch.";
                return false;
            }

            var cliIndexes = tokens
                .Select((token, index) => new { Token = token, Index = index })
                .Where(item => string.Equals(item.Token, "/cli", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Index)
                .ToList();
            if (cliIndexes.Count == 0)
            {
                error = "Missing /cli switch.";
                return false;
            }

            if (cliIndexes.Count > 1)
            {
                error = "The /cli switch was specified more than once.";
                return false;
            }

            tokens.RemoveAt(cliIndexes[0]);

            var commandLine = new CliCommandLine();
            bool commandSeen = false;
            bool jsonSeen = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (string.Equals(token, "/json", StringComparison.OrdinalIgnoreCase))
                {
                    if (jsonSeen)
                    {
                        error = "The /json switch was specified more than once.";
                        return false;
                    }

                    jsonSeen = true;
                    commandLine.Json = true;
                    continue;
                }

                if (string.Equals(token, "/input", StringComparison.OrdinalIgnoreCase))
                {
                    if (commandLine.HasInput)
                    {
                        error = "The /input switch was specified more than once.";
                        return false;
                    }

                    if (i + 1 >= tokens.Count)
                    {
                        error = "Missing value for /input.";
                        return false;
                    }

                    commandLine.InputPath = tokens[++i];
                    commandLine.HasInput = true;
                    continue;
                }

                if (string.Equals(token, "/root", StringComparison.OrdinalIgnoreCase))
                {
                    if (commandLine.HasRoot)
                    {
                        error = "The /root switch was specified more than once.";
                        return false;
                    }

                    if (i + 1 >= tokens.Count)
                    {
                        error = "Missing value for /root.";
                        return false;
                    }

                    commandLine.RootPath = tokens[++i];
                    commandLine.HasRoot = true;
                    continue;
                }

                if (string.Equals(token, "/depth", StringComparison.OrdinalIgnoreCase))
                {
                    if (commandLine.HasDepth)
                    {
                        error = "The /depth switch was specified more than once.";
                        return false;
                    }

                    if (i + 1 >= tokens.Count)
                    {
                        error = "Missing value for /depth.";
                        return false;
                    }

                    int depth;
                    if (!int.TryParse(tokens[++i], out depth) || depth < 0)
                    {
                        error = "The /depth value must be a non-negative integer.";
                        return false;
                    }

                    commandLine.Depth = depth;
                    commandLine.HasDepth = true;
                    continue;
                }

                if (token.Length > 0 && token[0] == '/')
                {
                    error = "Unknown CLI switch '" + token + "'.";
                    return false;
                }

                if (commandSeen)
                {
                    error = "The CLI command was specified more than once.";
                    return false;
                }

                CliCommand command;
                if (!TryParseCommand(token, out command))
                {
                    error = "Unknown CLI command '" + token + "'.";
                    return false;
                }

                commandLine.Command = command;
                commandSeen = true;
            }

            if (!commandSeen)
            {
                error = "Missing CLI command.";
                return false;
            }

            parsed = commandLine;
            return true;
        }

        public static CliCommandLine ParseOrThrow(IEnumerable<string> args)
        {
            CliCommandLine parsed;
            string error;
            if (!TryParse(args, out parsed, out error))
            {
                throw new InvalidOperationException(error);
            }

            return parsed;
        }

        private static bool TryParseCommand(string token, out CliCommand command)
        {
            if (string.Equals(token, "paths", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Paths;
                return true;
            }

            if (string.Equals(token, "conflicts", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Conflicts;
                return true;
            }

            if (string.Equals(token, "autosort", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Autosort;
                return true;
            }

            if (string.Equals(token, "cleanup", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Cleanup;
                return true;
            }

            if (string.Equals(token, "migrate", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Migrate;
                return true;
            }

            if (string.Equals(token, "scan", StringComparison.OrdinalIgnoreCase))
            {
                command = CliCommand.Scan;
                return true;
            }

            command = default(CliCommand);
            return false;
        }
    }
}
