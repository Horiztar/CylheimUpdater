using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace CylheimUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Updater Updater { get; } = new Updater();

        public MainWindow()
        {
            InitializeComponent();

            Updater.ProgressChanged += ((sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var idle = args.Status is UpdaterStatus.Ready or UpdaterStatus.Complete;
                    UpdateButton.IsEnabled = idle;
                    CancelButton.IsEnabled = !idle;

                    if (Updater.IsCancelled && !idle) return;

                    DownloadStatus.Text = args.Status.ToString();

                    var progress = args.Progress;
                    PercentText.Text = progress >= 0 ? $"{Math.Round(progress * 100, 2)}%" : null;
                    ProgressBar.IsIndeterminate = progress < 0;
                    ProgressBar.Value = Math.Max(0, progress);
                });
            });

            Updater.InfoSent += ((sender, s) =>
            {
                Dispatcher.Invoke(() =>
                {
                    InfoTextBox.AppendText(s);
                    InfoTextBox.AppendText("\n");
                });
            });

            Updater.ErrorOccurred += Updater_ErrorOccurred;
        }

        internal void Updater_ErrorOccurred(object sender, Exception e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, e.GetDetail(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Updater.StartUpdate();
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Updater.CancelUpdate();
        }

        private static byte[] GetHashSha256(string filename)
        {
            SHA256 Sha256 = SHA256.Create();
            using (FileStream stream = File.OpenRead(filename))
            {
                return Sha256.ComputeHash(stream);
            }
        }

        private static string BytesToString(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
    }
}
