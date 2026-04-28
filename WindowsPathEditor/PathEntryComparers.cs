using System;
using System.Collections.Generic;

namespace WindowsPathEditor
{
    public static class PathEntryComparers
    {
        public static readonly IEqualityComparer<PathEntry> SymbolicPath = new SymbolicPathComparer();

        private sealed class SymbolicPathComparer : IEqualityComparer<PathEntry>
        {
            public bool Equals(PathEntry x, PathEntry y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                return string.Equals(x.SymbolicPath, y.SymbolicPath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(PathEntry obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj == null ? "" : obj.SymbolicPath ?? "");
            }
        }
    }
}
