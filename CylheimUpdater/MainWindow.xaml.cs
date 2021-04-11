using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Downloader;
using HttpProgress;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using YamlDotNet.Serialization;
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
        private DownloadService Downloader { get; }
        private DownloadService VisualDownloader { get; }
        private string CylheimManifestUrl => "https://cdn.jsdelivr.net/gh/Horiztar/CylheimUpdater@master/Manifest/Cylheim.json";
        private string UpdaterManifestUrl => "https://cdn.jsdelivr.net/gh/Horiztar/CylheimUpdater@master/Manifest/CylheimUpdater.json";
        private string X64BitArch=>"win-x64";
        private string X86BitArch => "win-x86";
        private string CylheimExeName => "Cylheim.exe";
        private string CylheimUpdaterExeName => "CylheimUpdater.exe";
        internal static string CylheimUpdaterOldExeName => "CylheimUpdater_old.exe";
        private CancellationTokenSource CancelSource { get; set; }

        public MainWindow()
        {
            Debug();

            InitializeComponent();
            
            DownloadProgress.ProgressChanged += ((sender, progress) =>
            {
                if(!CancelSource.IsCancellationRequested)
                    SetProgress(Math.Round(progress.PercentComplete * 100,2));
            });
        }

        private void Debug()
        {
            //ExtractList list = new ExtractList()
            //{
            //    Root = "win-x64",
            //    Replenish = new List<string>()
            //    {
            //        "Resources/TapFX.wav"
            //    }
            //};
            //var text=JsonSerializer.Serialize(list);
            ////Directory.CreateDirectory("Test/Test");
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            StartUpdate();
        }

        private void StartUpdate()
        {
            CancelSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                SetProcessing(true);
                SetProgress(101);
                try
                {
                    await UpdateUpdater();
                    await UpdateCylheim();
                }
                catch (OperationCanceledException e)
                {
                    AppendInfo($"Update cancelled.");
                }
                catch (Exception e)
                {
                    DispatcherShowError(e.GetDetail());
                }
                finally
                {
                    SetProcessing(false);
                    SetProgress(-1);
                }
            },CancelSource.Token);
        }

        internal void StartUpdateIgnoreUpdaterVersion()
        {
            CancelSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                SetProcessing(true);
                SetProgress(101);
                try
                {
                    await UpdateCylheim();
                }
                catch (OperationCanceledException e)
                {
                    AppendInfo($"Update cancelled.");
                }
                catch (Exception e)
                {
                    DispatcherShowError(e.GetDetail());
                }
                finally
                {
                    SetProcessing(false);
                    SetProgress(-1);
                }
            },CancelSource.Token);
        }

        private async Task UpdateUpdater()
        {
            AppendInfo("Getting updater's manifest...");

            var packageText = await GetTextFromUrl(UpdaterManifestUrl);
            //var packageText = File.ReadAllText("../../../../Manifest/CylheimUpdater.json");
            var package = JsonSerializer.Deserialize<Package>(packageText);

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var latestVersion = package.LatestVersion;
            if (currentVersion >= latestVersion)
            {
                AppendInfo("Updater is up to date.");
                return;
            }

            AppendInfo("Downloading latest updater...");
            var stream=await DownloadFromUrl(package.Installers[0].Url);

            if (File.Exists(CylheimUpdaterExeName))
            {
                if (File.Exists(CylheimUpdaterOldExeName))
                {
                    File.Delete(CylheimUpdaterOldExeName);
                }
                File.Move(CylheimUpdaterExeName,CylheimUpdaterOldExeName);
            }

            using (var fileStream = File.Create(CylheimUpdaterExeName))
            {
                stream.Position = 0;
                stream.CopyTo(fileStream);
            }

            Process.Start(CylheimUpdaterExeName);
            Dispatcher.Invoke(()=> App.Current.Shutdown()) ;
        }

        private async Task UpdateCylheim()
        {
            AppendInfo("Getting Cylheim's manifest...");

            var packageText = await GetTextFromUrl(CylheimManifestUrl);
            //var packageText = File.ReadAllText("../../../../Manifest/Cylheim.json");
            var package = JsonSerializer.Deserialize<Package>(packageText);

            var currentVersion = GetCurrentCylheimVersion();
            var latestVersion = package.LatestVersion;
            AppendInfo($"Latest version: {latestVersion.ToString()}");
            if (currentVersion == null)
            {
                AppendInfo($"Cylheim is not installed.");
            }
            else
            {
                AppendInfo($"Current version: {currentVersion.ToString()}");
                var isLatest = currentVersion >= latestVersion;
                if (isLatest)
                {
                    AppendInfo($"Cylheim is up to date.");
                    return;
                }
            }

            AppendInfo($"Downloading Cylheim...");
            var isX64 = Environment.Is64BitOperatingSystem;
            var archName = isX64 ? X64BitArch : X86BitArch;
            var installer = package.Installers.FirstOrDefault(p => p.Architecture == archName);
            AppendInfo($"Architecture: {installer.Architecture}");
            
            using (var stream = await DownloadFromUrl(installer.Url))
            //using (var stream = File.OpenRead("Cylheim_1.1.3_win-x64.7z"))
            {
                if (!(await CloseCylheim()))
                {
                    AppendInfo($"Update cancelled.");
                    return;
                }

                AppendInfo($"Extracting...");
                SetProgress(101);
                IArchive archive = null;
                if(installer.InstallerType=="7z") archive=SevenZipArchive.Open(stream);
                else if(installer.InstallerType=="zip") archive=ZipArchive.Open(stream);
                var extractList = JsonSerializer.Deserialize<ExtractList>(installer.InstallerArgs);
                ExtractionOptions options = new ExtractionOptions()
                {
                    Overwrite = true,
                    PreserveFileTime = true,
                    PreserveAttributes = true
                };

                foreach (var entry in archive.Entries)
                {
                    if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();
                    var path = Path.GetRelativePath( extractList.Root, entry.Key);
                    var filename = Path.GetFileName(path);

                    if(path==".") continue;

                    if (entry.IsDirectory)
                    {
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        continue;
                    }

                    if (extractList.Replenish.Any(i=>Path.GetRelativePath(i,path)=="."))
                    {
                        continue;
                    }

                    try
                    {
                        entry.WriteToFile(path, options);
                    }
                    catch (IOException e)
                    {
                        if (filename != CylheimUpdaterExeName) throw e;
                    }
                }
            }

            if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();
            AppendInfo($"Update complete.");
            SetProgress(100);
            Process.Start(CylheimExeName);
        }

        private async Task<string> GetTextFromUrl(string url)
        {
            var response = await HttpClient.GetStringAsync(url,CancelSource.Token);

            return response;
        }

        private async Task<Stream> DownloadFromUrl(string url)
        {
            url = await GetRedirectedUrl(url) ?? url;
            MemoryStream stream = new();
            var response = await HttpClient.GetAsync(url,stream,DownloadProgress,CancelSource.Token);
            return stream;
        }
        
        public static async Task<string> GetRedirectedUrl(string url)
        {
            //this allows you to set the settings so that we can get the redirect url
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            string redirectedUrl = null;

            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url))
            using (HttpContent content = response.Content)
            {
                // ... Read the response to see if we have the redirected url
                if (response.StatusCode == System.Net.HttpStatusCode.Found)
                {
                    HttpResponseHeaders headers = response.Headers;
                    if (headers != null && headers.Location != null)
                    {
                        redirectedUrl = headers.Location.AbsoluteUri;
                    }
                }
            }

            return redirectedUrl;
        }

        private Version GetCurrentCylheimVersion()
        {
            if (!File.Exists(CylheimExeName))
            {
                return null;
            }

            var info=FileVersionInfo.GetVersionInfo(CylheimExeName);
            return Version.Parse(info.FileVersion);
        }

        private async Task<bool> CloseCylheim()
        {
            var exeName = CylheimExeName;
            bool flag = false;
            foreach (var process in Process.GetProcesses().Where(p => p.ProcessName == Path.GetFileNameWithoutExtension(exeName)))
            {
                if (!flag)
                {
                    TaskCompletionSource<MessageBoxResult> promise = new();
                    Dispatcher.Invoke(() =>
                    {
                        var r=MessageBox.Show(this, "Cylheim is running. Please ensure that your projects are saved. " +
                                              "Would you like to exit Cylheim and continue?", "Warning", MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        promise.SetResult(r);
                    });
                    var result = await promise.Task;
                    if (result == MessageBoxResult.No) return false;
                    flag = true;
                }
                process.Kill();
                process.WaitForExit();
            }

            return true;
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

        private static byte[] GetHashSha256(string filename)
        {
            SHA256 Sha256 = SHA256.Create();
            using (FileStream stream = File.OpenRead(filename))
            {
                return Sha256.ComputeHash(stream);
            }
        }

        private static string BytesToString(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelSource.Cancel();
            SetProcessing(false);
            SetProgress(-1);
        }
    }
}
