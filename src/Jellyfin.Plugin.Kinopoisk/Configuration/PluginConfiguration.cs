using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // https://kinopoiskapiunofficial.tech/
        public string ApiToken { get; set; } = "85d30ae5-d875-4c5f-900d-8e37bb20625e";

        // Disk cache of the responses by id (film, staff, trailers, person),
        // days; 0: off. A library-wide refresh then costs API requests only for
        // new / expired items (the API quota is per day)
        public int CacheDays { get; set; } = 30;

        // Cast / crew of films and series: one API request per item
        public bool LoadStaff { get; set; } = true;

        // Trailers of films and series: one API request per item
        public bool LoadTrailers { get; set; } = true;
    }
}
