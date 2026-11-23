using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public class QuotaTests
    {
        private sealed class CountingHandler : HttpMessageHandler
        {
            public int Calls;
            public HttpStatusCode Status = HttpStatusCode.PaymentRequired;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return Task.FromResult(new HttpResponseMessage(Status)
                {
                    Content = new StringContent(
                        "{\"message\":\"You exceeded the quota. You have sent 502 request, but available 500 per day\"}",
                        Encoding.UTF8, "application/json"),
                });
            }
        }

        [Fact]
        public async Task QuotaExceeded_ShouldBlockRequestsForPause()
        {
            var handler = new CountingHandler();
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var client = new KinopoiskApiClient("token", NullLogger<KinopoiskApiClient>.Instance, factory.Object,
                TimeSpan.FromHours(1), () => now);

            // 402: blocked, the request was sent
            var first = await Assert.ThrowsAsync<KinopoiskQuotaExceededException>(() => client.GetSingleFilm(1));
            Assert.Equal(1, handler.Calls);
            Assert.Equal(now.AddHours(1), first.BlockedUntil);

            // Within the hour: fails without a request
            now = now.AddMinutes(59);
            await Assert.ThrowsAsync<KinopoiskQuotaExceededException>(() => client.SearchByKeyword("x"));
            await Assert.ThrowsAsync<KinopoiskQuotaExceededException>(() => client.GetTrailers(1));
            Assert.Equal(1, handler.Calls);

            // After the hour: asks the API again
            now = now.AddMinutes(2);
            await Assert.ThrowsAsync<KinopoiskQuotaExceededException>(() => client.GetSingleFilm(1));
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task OtherErrors_ShouldNotBlock()
        {
            var handler = new CountingHandler { Status = HttpStatusCode.InternalServerError };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
            var client = new KinopoiskApiClient("token", NullLogger<KinopoiskApiClient>.Instance, factory.Object);

            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            Assert.Equal(2, handler.Calls);
        }
    }
}
