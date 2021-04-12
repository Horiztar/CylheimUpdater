using System;
using System.IO;

namespace CylheimUpdater
{
    public class SevenZipUtil
    {
        public static void Init7zDll()
        {
            Uri uri = new Uri("pack://application:,,,/CylheimUpdater;component/Resources/7z.dll");
            var resource = App.GetResourceStream(uri);
            var stream = resource.Stream;
            using (var writer = File.Create("7z.dll"))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(writer);
            }
            SevenZip.SevenZipBase.SetLibraryPath("7z.dll");
        }
    }
}