using System;
using System.IO;

namespace WindowsPathEditor.Tests
{
    internal sealed class TestDirectory : IDisposable
    {
        public string Root { get; private set; }

        private TestDirectory(string root)
        {
            Root = root;
        }

        public static TestDirectory Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "WindowsPathEditor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestDirectory(root);
        }

        public string CreateDirectory(string name)
        {
            var dir = Path.Combine(Root, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public void CreateFile(string directory, string fileName)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), new byte[] { 0x1 });
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
