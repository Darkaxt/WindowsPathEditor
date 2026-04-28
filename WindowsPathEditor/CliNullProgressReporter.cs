namespace WindowsPathEditor
{
    internal sealed class CliNullProgressReporter : IReportProgress
    {
        public void Begin()
        {
        }

        public void ReportProgress(string progress)
        {
        }

        public void FoundCandidate(string path)
        {
        }

        public void Done()
        {
        }

        public bool Cancelled
        {
            get { return false; }
        }
    }
}
