using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class SeriesMetadataProvider : BaseVideoMetadataProvider<Series, SeriesInfo>
    {
        public SeriesMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<SeriesInfo> providerIdResolver, KinopoiskSearch search, ILogger<SeriesMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(kinopoiskApiClient, providerIdResolver, search, logger, httpClientFactory)
        {
        }

        protected override Series ConvertResponseToItem(Film apiResponse)
            => apiResponse.ToSeries();
    }
}
