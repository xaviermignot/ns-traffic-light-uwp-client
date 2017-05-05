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
        /// <summary>
        /// Gets or sets the url of the Api
        /// </summary>
        public string ApiUrl { get; set; }

        /// <summary>
        /// Gets or sets the uri of the Api
        /// </summary>
        public Uri  ApiUri { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if SignalR should be used or not
        /// </summary>
        public bool UseSignalR { get; set; }

        /// <summary>
        /// Gets or sets the period to wait between each Api call if the polling mode is on (UseSignalR = false)
        /// </summary>
        public int ApiPollingPeriodInMs { get; set; }

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

            var configuration = JsonConvert.DeserializeObject<Configuration>(content);
            configuration.ApiUri = new Uri(configuration.ApiUrl);

            return configuration;
        }
    }
}
