using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers
{
    public class VideoResolver<T> : CommonLookupInfoResolver<T>
        where T : ItemLookupInfo
    {
        private readonly IKinopoiskApiClient _kinopoiskApiClient;
        private readonly KinopoiskSearch _search;

        public VideoResolver(IKinopoiskApiClient kinopoiskApiClient, KinopoiskSearch search, ILogger<VideoResolver<T>> logger) : base(logger)
        {
            _kinopoiskApiClient = kinopoiskApiClient ?? throw new ArgumentNullException(nameof(kinopoiskApiClient));
            _search = search ?? throw new ArgumentNullException(nameof(search));
        }

        public override async Task<(bool IsSuccess, int ProviderId)> TryResolve(T info, CancellationToken? ct = null)
        {
            // Try to get from standart sources
            var possibleResult = await base.TryResolve(info, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            // Trying to find empirically on kinopoisk
            if (string.IsNullOrWhiteSpace(info.Name))
            {
                _logger.LogDebug($"Film name is empty, skipping KinopoiskProviderId search");
                return (false, 0);
            }

            _logger.LogDebug($"Trying to get suitable film with name '{info.Name}'...");
            var searchResult = await _search.Search(info.Name, ct ?? CancellationToken.None);
            if (searchResult.All.Count < 1)
            {
                _logger.LogDebug($"Received empty search result");
                return (false, 0);
            }
            // Exact title matches only, when there are any
            var candidates = (searchResult.Matched.Count > 0 ? searchResult.Matched : searchResult.All).ToArray();
            _logger.LogDebug($"Received {searchResult.All.Count} results ({searchResult.Matched.Count} exact matches), trying to filter and match...");

            // Year: from the metadata, else from the name ("Title.2024.1080p")
            var year = info.Year ?? searchResult.Year;
            var candidates_by_year = FilterByYear(year, candidates);

            // Check if there are single candidate filtered by year
            possibleResult = await TryResolveBySingleCandidateLeft(info, candidates_by_year, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            // Try to resolve by ImdbId match filtered by year
            possibleResult = await TryResolveByImdbMatch(info, candidates_by_year, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            // Try to resolve by ImdbId match without filtering
            possibleResult = await TryResolveByImdbMatch(info, candidates, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            // TODO: Maybe check type?

            if (0 < candidates_by_year.Count)
            {
                var kinopoiskId = candidates_by_year.First().FilmId;
                _logger.LogDebug($"All other checks failed, use first result by year, setting KinopoiskProviderId to {kinopoiskId} ({info.Name})");
                return (true, kinopoiskId);
            }

            if (0 < candidates.Length)
            {
                var kinopoiskId = candidates.First().FilmId;
                _logger.LogDebug($"All other checks failed, use first result, setting KinopoiskProviderId to {kinopoiskId} ({info.Name})");
                return (true, kinopoiskId);
            }

            _logger.LogDebug($"Suitable result not found");
            return (false, 0);
        }

        public async Task<(bool IsSuccess, int ProviderId)> TryResolveByImdbMatch(T info, ICollection<FilmSearchResponse_films> candidates, CancellationToken? ct = null)
        {
            if (info.TryGetProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                _logger.LogDebug($"Trying to find result with ImdbId '{imdbId}'...");
                var index = 0;
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var film = await _kinopoiskApiClient.GetSingleFilm(candidate.FilmId, ct);

                        if (imdbId == film?.ImdbId)
                        {
                            _logger.LogDebug($"Found match: {candidate.FilmId} '{film.GetLocalName()}', ImdbId '{film?.ImdbId}', setting KinopoiskProviderId to {candidate.FilmId}");
                            return (true, candidate.FilmId);
                        }

                        _logger.LogDebug($"Film {candidate.FilmId} '{film.GetLocalName()}' has ImdbId '{film?.ImdbId}', skipping, {candidates.Count - ++index} candidates left...");
                    } catch (KinopoiskQuotaExceededException)
                    {
                        throw;
                    } catch (Exception e)
                    {
                        _logger.LogError(e, $"Error while retrieving film {candidate.FilmId}");
                        continue;
                    }
                }
            }

            return (false, 0);
        }

        public Task<(bool IsSuccess, int ProviderId)> TryResolveBySingleCandidateLeft(T info, ICollection<FilmSearchResponse_films> candidates, CancellationToken? ct = null)
        {
            if (candidates.Count == 1)
            {
                var kinopoiskId = candidates.Single().FilmId;
                _logger.LogDebug($"There is single candidate left, setting KinopoiskProviderId to {kinopoiskId} ({info.Name})");
                return Task.FromResult((true, kinopoiskId));
            }

            return Task.FromResult((false, 0));
        }

        public ICollection<FilmSearchResponse_films> FilterByYear(int? year, ICollection<FilmSearchResponse_films> candidates)
        {
            if (!year.HasValue)
            {
                _logger.LogDebug($"Can't filter by year, no year in metadata or name...");
                return Array.Empty<FilmSearchResponse_films>();
            }

            var targetYear = year.Value.ToString();
            var res = candidates.Where(f => f.Year == targetYear).ToArray();
            _logger.LogDebug($"Filtered by year {targetYear}, {res.Length} results left...");
            return res;
        }
    }
}
