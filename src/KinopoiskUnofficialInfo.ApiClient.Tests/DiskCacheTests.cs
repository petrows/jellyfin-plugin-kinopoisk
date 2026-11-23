using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public sealed class DiskCacheTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "kp-cache-test-" + Guid.NewGuid().ToString("N"));
        private readonly Mock<IKinopoiskApiClient> _inner = new();
        private DateTime _now = DateTime.UtcNow;

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        private DiskCachedKinopoiskApiClient Create(TimeSpan ttl)
            => new(_inner.Object, _dir, () => ttl, NullLogger<DiskCachedKinopoiskApiClient>.Instance, () => _now);

        // Fields the API returns as null although "required" in its schema
        private static Film TestFilm() => new() { KinopoiskId = 42, NameRu = "Тест", NameEn = null, ImdbId = null, RatingKinopoisk = 7.5 };

        [Fact]
        public async Task Film_ShouldBeServedFromDisk_AcrossInstances()
        {
            _inner.Setup(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>())).ReturnsAsync(TestFilm());

            var first = await Create(TimeSpan.FromDays(30)).GetSingleFilm(42);
            // A new instance (plugin restart): from the file, no API request
            var second = await Create(TimeSpan.FromDays(30)).GetSingleFilm(42);

            _inner.Verify(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>()), Times.Once);
            Assert.Equal("Тест", first.NameRu);
            Assert.Equal(42, second.KinopoiskId);
            Assert.Equal("Тест", second.NameRu);
            Assert.Null(second.NameEn);
            Assert.Equal(7.5, second.RatingKinopoisk);
        }

        [Fact]
        public async Task Expired_ShouldAskApiAgain()
        {
            _inner.Setup(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>())).ReturnsAsync(TestFilm());
            var client = Create(TimeSpan.FromDays(1));

            await client.GetSingleFilm(42);
            _now = _now.AddDays(2);
            await client.GetSingleFilm(42);

            _inner.Verify(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ZeroTtl_ShouldPassThrough()
        {
            _inner.Setup(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>())).ReturnsAsync(TestFilm());
            var client = Create(TimeSpan.Zero);

            await client.GetSingleFilm(42);
            await client.GetSingleFilm(42);

            _inner.Verify(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>()), Times.Exactly(2));
            Assert.False(Directory.Exists(_dir));
        }

        [Fact]
        public async Task StaffAndTrailers_ShouldBeCachedPerKind()
        {
            ICollection<StaffResponse> staff = new List<StaffResponse> { new() { StaffId = 1, NameRu = "Актёр" } };
            _inner.Setup(c => c.GetStaff(42, It.IsAny<CancellationToken?>())).ReturnsAsync(staff);
            _inner.Setup(c => c.GetTrailers(42, It.IsAny<CancellationToken?>())).ReturnsAsync(new VideoResponse { Total = 0 });
            var client = Create(TimeSpan.FromDays(30));

            await client.GetStaff(42);
            await client.GetTrailers(42);
            var cachedStaff = await client.GetStaff(42);
            await client.GetTrailers(42);

            _inner.Verify(c => c.GetStaff(42, It.IsAny<CancellationToken?>()), Times.Once);
            _inner.Verify(c => c.GetTrailers(42, It.IsAny<CancellationToken?>()), Times.Once);
            Assert.Single(cachedStaff);
            Assert.Equal("Актёр", Assert.Single(cachedStaff).NameRu);
        }

        [Fact]
        public async Task Search_ShouldNotBeCached()
        {
            _inner.Setup(c => c.SearchByKeyword("x", 1, It.IsAny<CancellationToken?>())).ReturnsAsync(new FilmSearchResponse());
            var client = Create(TimeSpan.FromDays(30));

            await client.SearchByKeyword("x");
            await client.SearchByKeyword("x");

            _inner.Verify(c => c.SearchByKeyword("x", 1, It.IsAny<CancellationToken?>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Errors_ShouldNotBeCached()
        {
            _inner.SetupSequence(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>()))
                .ThrowsAsync(new KinopoiskQuotaExceededException(DateTime.UtcNow))
                .ReturnsAsync(TestFilm());
            var client = Create(TimeSpan.FromDays(30));

            await Assert.ThrowsAsync<KinopoiskQuotaExceededException>(() => client.GetSingleFilm(42));
            var film = await client.GetSingleFilm(42);

            Assert.Equal("Тест", film.NameRu);
        }

        [Fact]
        public async Task BrokenFile_ShouldBeRefetched()
        {
            _inner.Setup(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>())).ReturnsAsync(TestFilm());
            Directory.CreateDirectory(Path.Combine(_dir, "film"));
            await File.WriteAllTextAsync(Path.Combine(_dir, "film", "42.json"), "{ not json");
            var client = Create(TimeSpan.FromDays(30));

            var film = await client.GetSingleFilm(42);
            await client.GetSingleFilm(42);

            Assert.Equal("Тест", film.NameRu);
            // Rewritten by the first call, the second one reads it
            _inner.Verify(c => c.GetSingleFilm(42, It.IsAny<CancellationToken?>()), Times.Once);
        }
    }
}
