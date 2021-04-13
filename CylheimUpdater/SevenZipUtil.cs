using System;
using System.IO;

namespace CylheimUpdater
{
    public class SevenZipUtil
    {
        public static void Init7zDll(bool isX64=false)
        {
            string arch = isX64 ? "x64" : "x86";
            Uri uri = new Uri($"pack://application:,,,/CylheimUpdater;component/Resources/x86/7z.dll");
            var resource = App.GetResourceStream(uri);
            var stream = resource.Stream;
            using (var writer = File.Create("7z.dll"))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(writer);
            }

            var libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll");

            SevenZip.SevenZipBase.SetLibraryPath(libraryPath);

        }
    }
}