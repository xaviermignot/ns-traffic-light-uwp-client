using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NsTrafficLightUwpClient
{
    /// <summary>
    /// Contains the configuration values
    /// </summary>
    public sealed class Configuration
    {
        public bool UseTpm { get; set; }

        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets the current instance
        /// </summary>
        public static Configuration Instance { get { return Lazy.Value; } }

        private static Lazy<Configuration> Lazy = new Lazy<Configuration>(
            () => ReadConfiguration().GetAwaiter().GetResult());

        private static async Task<Configuration> ReadConfiguration()
        {
            var packageFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var sampleFile = await packageFolder.GetFileAsync("configuration.json");
            var content = await Windows.Storage.FileIO.ReadTextAsync(sampleFile);

            return JsonConvert.DeserializeObject<Configuration>(content);
        }
    }
}
