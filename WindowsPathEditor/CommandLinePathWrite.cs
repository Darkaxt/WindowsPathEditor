using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsPathEditor
{
    public enum LegacyCommandLineStatus
    {
        None,
        Applied,
        Invalid
    }

    public interface ILegacyPathWriteTarget
    {
        IEnumerable<PathEntry> SystemPath { set; }
        IEnumerable<PathEntry> UserPath { set; }
    }

    internal static class CommandLinePathWrite
    {
        public static LegacyCommandLineStatus TryApplyLegacyArgs(IEnumerable<string> args, PathRegistry registry, out string error)
        {
            if (registry == null) throw new ArgumentNullException("registry");

            return TryApplyLegacyArgs(args, new PathRegistryWriteTarget(registry), out error);
        }

        public static LegacyCommandLineStatus TryApplyLegacyArgs(IEnumerable<string> args, ILegacyPathWriteTarget target, out string error)
        {
            if (target == null) throw new ArgumentNullException("target");

            error = null;

            var tokens = (args ?? Enumerable.Empty<string>()).ToList();
            var systemPath = default(IEnumerable<PathEntry>);
            var userPath = default(IEnumerable<PathEntry>);
            var sawLegacySwitch = false;
            var sawSystem = false;
            var sawUser = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (string.Equals(token, "/system", StringComparison.OrdinalIgnoreCase))
                {
                    if (sawSystem)
                    {
                        error = "The /system switch was specified more than once.";
                        return LegacyCommandLineStatus.Invalid;
                    }

                    sawSystem = true;
                    sawLegacySwitch = true;
                    if (i + 1 >= tokens.Count || IsSwitchToken(tokens[i + 1]))
                    {
                        error = "Missing value for /system.";
                        return LegacyCommandLineStatus.Invalid;
                    }

                    systemPath = ParseCommandLinePath(tokens[++i]);
                    continue;
                }

                if (string.Equals(token, "/user", StringComparison.OrdinalIgnoreCase))
                {
                    if (sawUser)
                    {
                        error = "The /user switch was specified more than once.";
                        return LegacyCommandLineStatus.Invalid;
                    }

                    sawUser = true;
                    sawLegacySwitch = true;
                    if (i + 1 >= tokens.Count || IsSwitchToken(tokens[i + 1]))
                    {
                        error = "Missing value for /user.";
                        return LegacyCommandLineStatus.Invalid;
                    }

                    userPath = ParseCommandLinePath(tokens[++i]);
                    continue;
                }

                if (IsSwitchToken(token))
                {
                    error = "Unknown legacy switch '" + token + "'.";
                    return LegacyCommandLineStatus.Invalid;
                }

                error = "Unexpected legacy argument '" + token + "'.";
                return LegacyCommandLineStatus.Invalid;
            }

            if (!sawLegacySwitch)
            {
                return LegacyCommandLineStatus.None;
            }

            if (systemPath != null)
            {
                target.SystemPath = systemPath;
            }

            if (userPath != null)
            {
                target.UserPath = userPath;
            }

            return LegacyCommandLineStatus.Applied;
        }

        private static bool IsSwitchToken(string token)
        {
            return !string.IsNullOrEmpty(token) && token[0] == '/';
        }

        private static IEnumerable<PathEntry> ParseCommandLinePath(string argument)
        {
            return (argument ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => new PathEntry(path))
                .ToList();
        }

        private sealed class PathRegistryWriteTarget : ILegacyPathWriteTarget
        {
            private readonly PathRegistry registry;

            public PathRegistryWriteTarget(PathRegistry registry)
            {
                this.registry = registry;
            }

            public IEnumerable<PathEntry> SystemPath
            {
                set { registry.SystemPath = value; }
            }

            public IEnumerable<PathEntry> UserPath
            {
                set { registry.UserPath = value; }
            }
        }
    }
}
