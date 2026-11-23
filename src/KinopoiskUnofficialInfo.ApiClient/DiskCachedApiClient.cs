using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Persistent cache of the responses by id (film, staff, trailers, person):
    /// one JSON file per request under the cache directory
    /// (&lt;directory&gt;/&lt;kind&gt;/&lt;id&gt;.json). A file younger than the TTL is
    /// returned without asking the API, so a library-wide metadata refresh only
    /// costs requests for new / expired items. Search is not cached here (new
    /// films must be found right away). A TTL of zero or less: pass-through.
    /// </summary>
    public class DiskCachedKinopoiskApiClient : IKinopoiskApiClient
    {
        // The settings of the generated client: its contract resolver relaxes
        // the "required" fields the API returns as null
        private static readonly JsonSerializerSettings SerializerSettings =
            JsonTransformator.TransformSettings(new JsonSerializerSettings());

        private readonly IKinopoiskApiClient _innerClient;
        private readonly string _directory;
        private readonly Func<TimeSpan> _ttl;
        private readonly Func<DateTime> _utcNow;
        private readonly ILogger<DiskCachedKinopoiskApiClient> _logger;

        /// <param name="innerClient">The client asked on a cache miss</param>
        /// <param name="directory">Cache root directory, created on demand</param>
        /// <param name="ttl">Max age of a cached response, read on every request (config changes apply at once)</param>
        /// <param name="logger">Logger</param>
        /// <param name="utcNow">Clock (tests), default DateTime.UtcNow</param>
        public DiskCachedKinopoiskApiClient(IKinopoiskApiClient innerClient, string directory, Func<TimeSpan> ttl,
            ILogger<DiskCachedKinopoiskApiClient> logger, Func<DateTime> utcNow = null)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException($"'{nameof(directory)}' cannot be null or empty.", nameof(directory));
            }

            _directory = directory;
            _ttl = ttl ?? throw new ArgumentNullException(nameof(ttl));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Get("person", personId, () => _innerClient.GetPerson(personId, cancellationToken));

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => Get("film", filmId, () => _innerClient.GetSingleFilm(filmId, cancellationToken));

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => Get("staff", filmId, () => _innerClient.GetStaff(filmId, cancellationToken));

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
            => Get("trailers", filmId, () => _innerClient.GetTrailers(filmId, cancellationToken));

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => _innerClient.SearchByKeyword(keyword, page, cancellationToken);

        private async Task<T> Get<T>(string kind, int id, Func<Task<T>> fetch)
            where T : class
        {
            var ttl = _ttl();
            if (ttl <= TimeSpan.Zero)
            {
                return await fetch().ConfigureAwait(false);
            }

            var path = Path.Combine(_directory, kind, id.ToString(CultureInfo.InvariantCulture) + ".json");
            var cached = TryRead<T>(path, ttl);
            if (cached != null)
            {
                _logger.LogDebug($"Disk cache hit: {kind} {id}");
                return cached;
            }

            // Errors (quota, 404, ...) are not cached: they propagate as is
            var result = await fetch().ConfigureAwait(false);
            if (result != null)
            {
                TryWrite(path, result);
            }

            return result;
        }

        private T TryRead<T>(string path, TimeSpan ttl)
            where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                if (_utcNow() - File.GetLastWriteTimeUtc(path) > ttl)
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), SerializerSettings);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // A broken / unreadable file is a miss: fetched and rewritten
                _logger.LogWarning(e, $"Disk cache: can't read {path}, asking the API");
                return null;
            }
        }

        private void TryWrite<T>(string path, T value)
        {
            // Write + rename: a reader never sees a half-written file
            var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, JsonConvert.SerializeObject(value, SerializerSettings));
                File.Move(tmp, path, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // Not cached, the response itself is fine
                _logger.LogWarning(e, $"Disk cache: can't write {path}");
                try
                {
                    File.Delete(tmp);
                }
                catch (Exception de) when (de is IOException or UnauthorizedAccessException)
                {
                    // Nothing more to do
                }
            }
        }
    }
}
