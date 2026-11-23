using System;
using System.IO;
using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Registers services
    /// </summary>
    public class KinopoiskPluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton((sp) => new KinopoiskApiClient(
                Plugin.Instance.Configuration.ApiToken,
                sp.GetRequiredService<ILogger<KinopoiskApiClient>>(),
                sp.GetRequiredService<IHttpClientFactory>()
            ));
            // API -> disk cache (by id, days, <data>/kinopoisk-cache: survives
            // restarts and plugin updates) -> memory cache (1 min, also search)
            serviceCollection.AddSingleton((sp) => new DiskCachedKinopoiskApiClient(
                sp.GetRequiredService<KinopoiskApiClient>(),
                Path.Combine(sp.GetRequiredService<IApplicationPaths>().DataPath, "kinopoisk-cache"),
                () => TimeSpan.FromDays(Math.Max(0, Plugin.Instance?.Configuration?.CacheDays ?? 0)),
                sp.GetRequiredService<ILogger<DiskCachedKinopoiskApiClient>>()
            ));
            serviceCollection.AddSingleton<IKinopoiskApiClient>((sp) => new CachedKinopoiskApiClient(
                sp.GetRequiredService<DiskCachedKinopoiskApiClient>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>()
            ));

            serviceCollection.AddSingleton<KinopoiskSearch>();

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();
        }
    }
}
