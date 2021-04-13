using System.Collections.Generic;

namespace CylheimUpdater
{
    public class Cylheim7zInstaller
    {
        public string Root { get; set; }
        public List<string> Replace { get; set; }
        public List<string> Replenish { get; set; }
        public string MinDotNetVersion { get; set; }
        public string DotNetUrl { get; set; }
    }
}