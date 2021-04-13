using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CylheimUpdater
{
    public class Updater
    {

        private static string X64BitArch => "win-x64";
        private static string X86BitArch => "win-x86";
        private static string CylheimUpdaterExeName => "CylheimUpdater.exe";
        private static string CylheimUpdaterOldExeName => "CylheimUpdater_old.exe";

        public event EventHandler<string> InfoSent;
        public event EventHandler<UpdaterProgressArgs> ProgressChanged;
        public event EventHandler<Exception> ErrorOccurred;


        private WebUtil WebUtil { get; } = new WebUtil();
        private CancellationTokenSource CancelSource { get; set; }
        internal bool IsCancelled => CancelSource.IsCancellationRequested;


        public Updater()
        {
            WebUtil.DownloadProgress.ProgressChanged += ((sender, progress) =>
            {
                ProgressChanged?.Invoke(this, new(UpdaterStatus.Downloading, progress.PercentComplete));
            });
        }

        internal async Task StartUpdate(bool ignoreUpdater = false)
        {
            CancelSource = new CancellationTokenSource();

            ProgressChanged?.Invoke(this, new(UpdaterStatus.Connecting, -1));

            bool complete = false;

            try
            {
                await WebUtil.InitRoute(CancelSource.Token);

                if (!ignoreUpdater) await UpdateUpdater();
                await UpdateCylheim();

                Process.Start(CylheimService.CylheimExeName);
                complete = true;
            }
            catch (OperationCanceledException e)
            {
                InfoSent?.Invoke(this, "Update cancelled.");
            }
            catch (Exception e)
            {
                ErrorOccurred?.Invoke(this, e);
                InfoSent?.Invoke(this, "Update unexpectedly interrupted.");
            }
            finally
            {
                if (complete) ProgressChanged?.Invoke(this, new(UpdaterStatus.Complete, 1));
                else ProgressChanged?.Invoke(this, new(UpdaterStatus.Ready, 0));
            }
        }

        private async Task UpdateUpdater()
        {
            InfoSent?.Invoke(this, "Getting updater's manifest...");
#if DEBUG
            var packageText = File.ReadAllText("../../../../Manifest/CylheimUpdater.json");
            var package = JsonSerializer.Deserialize<Package>(packageText);
#elif RELEASE
            var package = await GetManifest(new Dictionary<RegionInfo, string>()
            {
                {new RegionInfo("IV"),"https://raw.githubusercontent.com/Horiztar/CylheimUpdater/master/Manifest/CylheimUpdater.json"},
                {new RegionInfo("CN"),"https://cylheim-1305581578.cos.ap-guangzhou.myqcloud.com/CylheimUpdater_CN.json"}
            });
#endif


            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var latestVersion = package.LatestVersion;
            if (currentVersion >= latestVersion)
            {
                InfoSent?.Invoke(this, "Updater is up to date.");
                return;
            }

            InfoSent?.Invoke(this, "Downloading latest updater...");
            var stream = await WebUtil.DownloadFromUrl(package.Installers[0].Url);

            if (File.Exists(CylheimUpdaterExeName))
            {
                if (File.Exists(CylheimUpdaterOldExeName))
                {
                    File.Delete(CylheimUpdaterOldExeName);
                }
                File.Move(CylheimUpdaterExeName, CylheimUpdaterOldExeName);
            }

            using (var fileStream = File.Create(CylheimUpdaterExeName))
            {
                stream.Position = 0;
                stream.CopyTo(fileStream);
            }

            Process.Start(CylheimUpdaterExeName);
            App.Current.Dispatcher.Invoke(() => App.Current.Shutdown());
        }

        private async Task UpdateCylheim()
        {
            InfoSent?.Invoke(this, "Getting Cylheim's manifest...");

#if DEBUG
            var packageText = File.ReadAllText("../../../../Manifest/Cylheim.json");
            var package = JsonSerializer.Deserialize<Package>(packageText);
#elif RELEASE
            var package = await GetManifest(new Dictionary<RegionInfo, string>()
            {
                {new RegionInfo("IV"),"https://raw.githubusercontent.com/Horiztar/CylheimUpdater/master/Manifest/Cylheim.json"},
                {new RegionInfo("CN"),"https://cylheim-1305581578.cos.ap-guangzhou.myqcloud.com/Cylheim_CN.json"}
            });
#endif


            var currentVersion = CylheimService.GetCurrentCylheimVersion();
            var latestVersion = package.LatestVersion;
            InfoSent?.Invoke(this, $"Latest version: {latestVersion.ToString()}");
            if (currentVersion == null)
            {
                InfoSent?.Invoke(this, "Cylheim is not installed.");
            }
            else
            {
                InfoSent?.Invoke(this, $"Current version: {currentVersion.ToString()}");
                var isLatest = currentVersion >= latestVersion;
                if (isLatest)
                {
                    InfoSent?.Invoke(this, "Cylheim is up to date.");
                    return;
                }
            }

            InfoSent?.Invoke(this, "Downloading Cylheim...");
            var isX64 = Environment.Is64BitOperatingSystem;
            var archName = isX64 ? X64BitArch : X86BitArch;
            var installer = package.Installers.FirstOrDefault(p => p.Architecture == archName);
            InfoSent?.Invoke(this, $"Architecture: {installer.Architecture}");
#if DEBUG
            using (var stream = File.OpenRead("Cylheim_1.1.3_win-x64.7z"))
#elif RELEASE
            using (var stream = await WebUtil.DownloadFromUrl(installer.Url))
#endif
            {
                if (!(await CylheimService.CloseCylheim()))
                {
                    InfoSent?.Invoke(this, "Update cancelled.");
                    throw new OperationCanceledException();
                }

                ProgressChanged?.Invoke(this, new(UpdaterStatus.Extracting, -1));
                InfoSent?.Invoke(this, "Extracting...");

                try
                {
                    SevenZipUtil.Init7zDll(isX64);
                }
                catch (Exception e) { }


                SevenZip.SevenZipExtractor extractor = new SevenZipExtractor(stream);
                var infos = extractor.ArchiveFileData;

                decimal totalSize = infos.Sum(info => (decimal)info.Size);
                decimal completeSize = 0;
                decimal thisSize = 0;

                extractor.Extracting += (s, e) =>
                {
                    var totalPercent = completeSize / totalSize + thisSize / totalSize * ((decimal)e.PercentDone / 100);
                    ProgressChanged?.Invoke(this, new(UpdaterStatus.Extracting, (double)totalPercent));
                };
                var extractList = JsonSerializer.Deserialize<ExtractList>(installer.InstallerArgs);

                for (int i = 0; i < infos.Count; i++)
                {
                    completeSize += thisSize;

                    var info = infos[i];
                    thisSize = info.Size;
                    var path = Path.GetRelativePath(extractList.Root, info.FileName);
                    var filename = Path.GetFileName(path);
                    var directory = Path.GetDirectoryName(path);

                    if (path == ".") continue;

                    if (info.IsDirectory)
                    {
                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        continue;
                    }

                    if (extractList.Replenish.Any(i => Path.GetRelativePath(i, path) == "."))
                    {
                        if (File.Exists(path)) continue;
                    }
                    try
                    {
                        if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();
                        using (var fileStream = File.Create(path))
                        {
                            extractor.ExtractFile(i, fileStream);
                        }
                        if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();

                        var attr = (FileAttributes)info.Attributes;
                        File.SetAttributes(path, attr);
                        File.SetCreationTime(path, info.CreationTime);
                        File.SetLastAccessTime(path, info.LastAccessTime);
                        File.SetLastWriteTime(path, info.LastWriteTime);
                    }
                    catch (IOException e)
                    {
                        if (filename != CylheimUpdaterExeName) throw e;
                    }

                    if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();
                }

            }

            if (CancelSource.IsCancellationRequested) throw new OperationCanceledException();
        }

        private async Task<Package> GetManifest(Dictionary<RegionInfo, string> urls)
        {
            string text = null;
            if (urls.ContainsKey(WebUtil.Region))
            {
                try
                {
                    text = await WebUtil.GetTextFromUrl(urls[WebUtil.Region]);
                    if (!string.IsNullOrWhiteSpace(text)) return JsonSerializer.Deserialize<Package>(text);
                }
                catch
                {

                }

            }

            text = await WebUtil.GetTextFromUrl(urls.ElementAtOrDefault(0).Value);
            return JsonSerializer.Deserialize<Package>(text);
        }

        internal void DeleteOldUpdater()
        {
            var exeName = CylheimUpdaterOldExeName;
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
        }

        internal void CancelUpdate()
        {
            CancelSource.Cancel();
        }
    }

    public class UpdaterProgressArgs
    {
        public UpdaterStatus Status { get; private set; }
        public double Progress { get; private set; }

        public UpdaterProgressArgs(UpdaterStatus status, double progress)
        {
            Status = status;
            Progress = progress;
        }
    }

    public enum UpdaterStatus
    {
        Ready,
        Connecting,
        Downloading,
        Extracting,
        Complete
    }
}