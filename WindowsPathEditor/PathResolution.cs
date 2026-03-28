using System;

namespace WindowsPathEditor
{
    public sealed class PathResolution
    {
        private PathResolution(bool isResolved, string actualPath, string actualPathForAccess, string errorMessage)
        {
            IsResolved = isResolved;
            ActualPath = actualPath ?? "";
            ActualPathForAccess = actualPathForAccess ?? "";
            ErrorMessage = errorMessage ?? "";
        }

        public bool IsResolved { get; private set; }
        public string ActualPath { get; private set; }
        public string ActualPathForAccess { get; private set; }
        public string ErrorMessage { get; private set; }

        public static PathResolution Resolved(string actualPath, string actualPathForAccess)
        {
            return new PathResolution(true, actualPath, actualPathForAccess, "");
        }

        public static PathResolution Unresolved(string errorMessage)
        {
            return new PathResolution(false, "", "", errorMessage);
        }
    }
}
