using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class KinopoiskApiClient : IKinopoiskApiClient
    {
        // After a "quota exceeded" answer every request fails at once for this
        // long, without asking the API (the quota is per day, every request
        // still counts and floods the log)
        public static readonly TimeSpan DefaultQuotaPause = TimeSpan.FromHours(1);

        private readonly string _apiToken;
        private readonly TimeSpan _quotaPause;
        private readonly Func<DateTime> _utcNow;
        // UTC ticks, 0: not blocked
        private long _blockedUntilTicks;
        private readonly ILogger<KinopoiskApiClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Client _apiClient;

        public KinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> logger, IHttpClientFactory httpClientFactory)
            : this(apiToken, logger, httpClientFactory, DefaultQuotaPause, null)
        {
        }

        public KinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> logger, IHttpClientFactory httpClientFactory, TimeSpan quotaPause, Func<DateTime> utcNow)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new System.ArgumentException($"'{nameof(apiToken)}' cannot be null or empty.", nameof(apiToken));
            }

            _apiToken = apiToken;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new System.ArgumentNullException(nameof(httpClientFactory));

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", _apiToken);
            _apiClient = new Client(httpClient);
            _quotaPause = quotaPause;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        private static bool IsQuotaExceeded(ApiException e)
            => e.StatusCode == 402
                || (e.StatusCode == 429 && e.Response?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true);

        private async Task<T> Invoke<T>(Func<CancellationToken, Task<T>> method, CancellationToken? ct, [CallerMemberName] string memberName = "")
        {
            var blockedUntil = new DateTime(Interlocked.Read(ref _blockedUntilTicks), DateTimeKind.Utc);
            if (_utcNow() < blockedUntil)
            {
                _logger.LogDebug($"{memberName} request skipped: quota exceeded, blocked until {blockedUntil:u}");
                throw new KinopoiskQuotaExceededException(blockedUntil);
            }

            try
            {
                _logger.LogDebug($"{memberName} request starting...");
                var res = await method.Invoke(ct ?? CancellationToken.None);
                _logger.LogDebug($"{memberName} request complete successfully");
                return res;
            }
            catch (ApiException e) when (IsQuotaExceeded(e))
            {
                var until = _utcNow() + _quotaPause;
                var previous = Interlocked.Exchange(ref _blockedUntilTicks, until.Ticks);
                // Once per block (parallel requests get it too)
                if (_utcNow().Ticks >= previous)
                    _logger.LogWarning($"Kinopoisk API quota exceeded (status {e.StatusCode}: {e.Response}), all requests fail until {until:u}");
                throw new KinopoiskQuotaExceededException(until, e);
            }
            catch (ApiException e)
            {
                _logger.LogError($"Received non-success result status code {e.StatusCode} from Kinopoisk API, response content is:\n{e.Response}");
                throw;
            }
        }

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.FilmsAsync(filmId, ct), cancellationToken);

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAllAsync(filmId, ct), cancellationToken);

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.SearchByKeywordAsync(keyword, null, ct), cancellationToken);

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAsync(personId, ct), cancellationToken);

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
        {
            return Invoke(async (ct) => {
                try {
                    return await _apiClient.VideosAsync(filmId, ct);
                } catch (ApiException e)
                {
                    if (e.StatusCode == 404)
                        return new VideoResponse();
                    throw;
                }
            }, cancellationToken);
        }
    }
}
