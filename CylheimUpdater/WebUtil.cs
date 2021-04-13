using HttpProgress;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CylheimUpdater
{
    public class WebUtil
    {
        private static string IpApi => "https://api.ipify.org/";
        private static string IpGeolocationApi => "http://ip-api.com/json";
        private HttpClient HttpClient { get; } = new HttpClient();
        internal RegionInfo Region { get; private set; }
        private CancellationToken CancelToken { get; set; }
        internal Progress<ICopyProgress> DownloadProgress { get; } = new Progress<ICopyProgress>();


        public async Task<string> GetRedirectedUrl(string url)
        {
            //this allows you to set the settings so that we can get the redirect url
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            string redirectedUrl = null;

            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(url, CancelToken))
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

        internal async Task<string> GetTextFromUrl(string url)
        {
            var response = await HttpClient.GetStringAsync(url, CancelToken);

            return response;
        }

        internal async Task<Stream> DownloadFromUrl(string url)
        {
            url = await GetRedirectedUrl(url) ?? url;
            MemoryStream stream = new();
            var response = await HttpClient.GetAsync(url, stream, DownloadProgress, CancelToken);
            return stream;
        }

        internal async Task InitRoute(CancellationToken token)
        {
#if DEBUG
            string geoText = File.ReadAllText("../../../../IpGeolocation.json");
#elif RELEASE
            string geoText = await HttpClient.GetStringAsync(IpGeolocationApi, token);
#endif

            IpGeolocation geolocation = JsonSerializer.Deserialize<IpGeolocation>(geoText);
            RegionInfo region = new(geolocation.CountryCode);
            Region = region;

            CancelToken = token;
        }
    }
}