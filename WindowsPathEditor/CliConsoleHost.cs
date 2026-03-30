using System;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Windows;

namespace WindowsPathEditor
{
    internal static class CliConsoleHost
    {
        private const int AttachParentProcess = -1;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private const int StandardOutputHandle = -11;
        private const int StandardErrorHandle = -12;
        private const uint FileTypeUnknown = 0;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareWrite = 0x2;
        private const uint OpenExisting = 3;

        public static bool AttachToParentConsole()
        {
            var stdout = TryCreateWriterFromStandardHandle(StandardOutputHandle);
            var stderr = TryCreateWriterFromStandardHandle(StandardErrorHandle);
            if (stdout != null && stderr != null)
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);
                return true;
            }

            if (!AttachConsole(AttachParentProcess))
            {
                MessageBox.Show(
                    "This command-line mode requires a parent console.",
                    "Windows Path Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            stdout = stdout ?? TryCreateWriterForConsole();
            stderr = stderr ?? TryCreateWriterForConsole();
            if (stdout == null || stderr == null)
            {
                MessageBox.Show(
                    "Unable to initialize command-line output.",
                    "Windows Path Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            Console.SetOut(stdout);
            Console.SetError(stderr);
            return true;
        }

        private static StreamWriter TryCreateWriterFromStandardHandle(int standardHandle)
        {
            var handle = GetStdHandle(standardHandle);
            if (handle == IntPtr.Zero || handle == InvalidHandleValue || GetFileType(handle) == FileTypeUnknown)
            {
                return null;
            }

            return CreateWriter(handle, false);
        }

        private static StreamWriter TryCreateWriterForConsole()
        {
            var handle = CreateFile("CONOUT$", GenericWrite, FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == InvalidHandleValue)
            {
                return null;
            }

            return CreateWriter(handle, true);
        }

        private static StreamWriter CreateWriter(IntPtr handle, bool ownsHandle)
        {
            var safeHandle = new SafeFileHandle(handle, ownsHandle);
            var stream = new FileStream(safeHandle, FileAccess.Write);
            return new StreamWriter(stream, Encoding.Default)
            {
                AutoFlush = true
            };
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileType(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
    }
}
