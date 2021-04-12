using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CylheimUpdater
{
    public class CylheimService
    {
        public static string CylheimExeName => "Cylheim.exe";

        public static Version GetCurrentCylheimVersion()
        {
            if (!File.Exists(CylheimExeName))
            {
                return null;
            }

            var info = FileVersionInfo.GetVersionInfo(CylheimExeName);
            return Version.Parse(info.FileVersion);
        }

        public static async Task<bool> CloseCylheim()
        {
            var exeName = CylheimService.CylheimExeName;
            bool flag = false;
            foreach (var process in Process.GetProcesses().Where(p => p.ProcessName == Path.GetFileNameWithoutExtension(exeName)))
            {
                if (!flag)
                {
                    TaskCompletionSource<MessageBoxResult> promise = new();
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var r = MessageBox.Show(App.Current.MainWindow, "Cylheim is running. Please ensure that your projects are saved. " +
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
    }
}