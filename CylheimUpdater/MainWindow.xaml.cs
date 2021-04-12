using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HttpProgress;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using Path = System.IO.Path;

namespace CylheimUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HttpClient HttpClient { get; } = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        private Progress<ICopyProgress> DownloadProgress { get; } = new Progress<ICopyProgress>();
        private string CylheimManifestUrl => "https://cdn.jsdelivr.net/gh/Horiztar/CylheimUpdater@latest/Manifest/Cylheim.json";
        private string UpdaterManifestUrl => "https://cdn.jsdelivr.net/gh/Horiztar/CylheimUpdater@latest/Manifest/CylheimUpdater.json";
        private string X64BitArch=>"win-x64";
        private string X86BitArch => "win-x86";
        
        private string CylheimUpdaterExeName => "CylheimUpdater.exe";
        internal static string CylheimUpdaterOldExeName => "CylheimUpdater_old.exe";
        private WebUtil WebUtil { get; set; }
        public Updater Updater { get; } = new Updater();

        public MainWindow()
        {
            InitializeComponent();

            Updater.ProgressChanged += ((sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
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
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Updater.StartUpdate();
        }
        

        
        private async Task UpdateCylheim()
        {
            
        }

        

        private void AppendInfo(string text)
        {
            Dispatcher.Invoke(() =>
            {
                InfoTextBox.AppendText(text);
                InfoTextBox.AppendText("\n");
            });
        }

        private void SetProcessing(bool isProcessing)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateButton.IsEnabled = !isProcessing;
                CancelButton.IsEnabled = isProcessing;
            });
        }

        private void SetProgress(double percent)
        {
            Dispatcher.Invoke(() =>
            {
                if (percent < 0)
                {
                    DownloadStatus.Text = "Ready";
                    PercentText.Text = "";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 0;
                }
                else if(percent<100)
                {
                    DownloadStatus.Text = "Downloading...";
                    PercentText.Text = $"{percent}%";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = percent;
                }else if (percent == 100)
                {
                    DownloadStatus.Text = "Finish";
                    PercentText.Text = $"{percent}%";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 100;
                }
                else
                {
                    DownloadStatus.Text = "Processing...";
                    PercentText.Text = $"";
                    ProgressBar.IsIndeterminate = true;
                }
            });
        }

        private void DispatcherShowError(string error)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            //WebUtil.CancelSource.Cancel();
            SetProcessing(false);
            SetProgress(-1);
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
