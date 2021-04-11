using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CylheimUpdater
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += ((sender, args) =>
            {

            });
        }
        private string DeleteOldUpdaterCommand => "--delete-old";

        internal string IgnoreUpdaterVersionCommand => "--ignore-updater-version";

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var exeName = CylheimUpdater.MainWindow.CylheimUpdaterOldExeName;
            if (File.Exists(exeName))
            {
                try
                {
                    File.Delete(exeName);
                }
                catch (UnauthorizedAccessException)
                {
                    foreach (var process in Process.GetProcesses().Where(p => p.ProcessName == Path.GetFileNameWithoutExtension(exeName)))
                    {
                        process.Kill();
                        process.WaitForExit();
                        File.Delete(exeName);
                    }
                }
            }

            CylheimUpdater.MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            foreach (var arg in e.Args)
            {
                if (arg == IgnoreUpdaterVersionCommand)
                {
                    mainWindow.StartUpdateIgnoreUpdaterVersion();
                }
            }
        }
    }
}
