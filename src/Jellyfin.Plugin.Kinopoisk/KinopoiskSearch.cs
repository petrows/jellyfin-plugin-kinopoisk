using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Search result: the cleaned title / year of the name, the candidates
    /// whose name matches the title exactly (loose: case, punctuation, ё/е, ...)
    /// and all candidates (matched first).
    /// </summary>
    public sealed record KinopoiskSearchResult(
        string Title,
        int? Year,
        IReadOnlyList<FilmSearchResponse_films> Matched,
        IReadOnlyList<FilmSearchResponse_films> All);

    /// <summary>
    /// Keyword search by an item name: cleans the name first (TitleCleaner,
    /// Kodi style), a Russian title written in Latin letters ("Deti peremen")
    /// is searched once more in Cyrillic when the first search has no exact
    /// match.
    /// </summary>
    public class KinopoiskSearch
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly ILogger<KinopoiskSearch> _logger;

        public KinopoiskSearch(IKinopoiskApiClient apiClient, ILogger<KinopoiskSearch> logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<KinopoiskSearchResult> Search(string name, CancellationToken ct)
        {
            var (title, year) = TitleCleaner.Clean(name);
            if (title != name)
                _logger.LogDebug("Cleaned name '{Name}' to '{Title}' (year {Year})", name, title, year);

            var titleKey = title.ToTitleKey();
            var cyrillic = title.HasLatin() && !title.HasCyrillic()
                ? title.TransliterateLatinToCyrillic()
                : null;
            var cyrillicKey = cyrillic?.ToTitleKey();

            var films = await SearchKeyword(title, ct);
            var matched = Match(films, titleKey, cyrillicKey);

            if (matched.Count == 0 && cyrillic is not null)
            {
                _logger.LogDebug("No exact match for '{Title}', searching the transliteration '{Cyrillic}'", title, cyrillic);
                var translitFilms = await SearchKeyword(cyrillic, ct);
                matched = Match(translitFilms, titleKey, cyrillicKey);
                films = films.Concat(translitFilms).ToList();
            }

            _logger.LogDebug("Search '{Title}': {Matched} exact matches, {All} results", title, matched.Count, films.Count);

            var all = matched.Concat(films).DistinctBy(f => f.FilmId).ToList();
            return new KinopoiskSearchResult(title, year, matched.DistinctBy(f => f.FilmId).ToList(), all);
        }

        private async Task<IReadOnlyList<FilmSearchResponse_films>> SearchKeyword(string keyword, CancellationToken ct)
        {
            var res = await _apiClient.SearchByKeyword(keyword, 1, ct);
            return res?.Films?.Where(f => f is not null).ToList() ?? new List<FilmSearchResponse_films>();
        }

        private static List<FilmSearchResponse_films> Match(IEnumerable<FilmSearchResponse_films> films, string titleKey, string cyrillicKey)
        {
            if (string.IsNullOrEmpty(titleKey))
                return new List<FilmSearchResponse_films>();

            return films
                .Where(f =>
                {
                    var ru = f.NameRu.ToTitleKey();
                    var en = f.NameEn.ToTitleKey();
                    return ru == titleKey || en == titleKey
                        || (!string.IsNullOrEmpty(cyrillicKey) && ru == cyrillicKey);
                })
                .ToList();
        }
    }
}
