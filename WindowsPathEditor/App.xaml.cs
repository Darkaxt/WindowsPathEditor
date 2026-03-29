using System;
using System.Windows;

namespace WindowsPathEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (CliCommandLine.IsCliRequest(e.Args))
            {
                if (!CliConsoleHost.AttachToParentConsole())
                {
                    Shutdown(1);
                    return;
                }

                CliCommandLine commandLine;
                string error;
                if (!CliCommandLine.TryParse(e.Args, out commandLine, out error))
                {
                    Console.Error.WriteLine(error);
                    Console.Error.WriteLine(CliCommandLine.Usage);
                    Shutdown(1);
                    return;
                }

                Shutdown(CliRunner.Run(commandLine));
                return;
            }

            var registry = new PathRegistry();
            string legacyError;
            var legacyStatus = CommandLinePathWrite.TryApplyLegacyArgs(e.Args, registry, out legacyError);
            if (legacyStatus == LegacyCommandLineStatus.Applied)
            {
                Shutdown(0);
                return;
            }

            if (legacyStatus == LegacyCommandLineStatus.Invalid)
            {
                MessageBox.Show(
                    legacyError ?? "Invalid legacy command-line arguments.",
                    "Windows Path Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
