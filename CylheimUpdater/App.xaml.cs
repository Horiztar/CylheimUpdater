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
            CylheimUpdater.MainWindow mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;

            mainWindow.Updater.DeleteOldUpdater();

            mainWindow.Show();

            foreach (var arg in e.Args)
            {
                if (arg == IgnoreUpdaterVersionCommand)
                {
                    mainWindow.Updater.StartUpdate(true);
                }
            }
        }
    }
}
