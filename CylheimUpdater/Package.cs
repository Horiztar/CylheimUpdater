using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CylheimUpdater
{
    class Package
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public List<Installer> Installers { get; set; }

        [JsonIgnore]
        public Version LatestVersion=>System.Version.Parse(Version);
    }

    class Installer
    {
        public string Architecture { get; set; }
        public string Sha256 { get; set; }
        public string Url { get; set; }
        public string InstallerType { get; set; }
        public string InstallerArgs { get; set; }
    }

    
}
