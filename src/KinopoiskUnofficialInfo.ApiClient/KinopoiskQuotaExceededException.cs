using System;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// The API quota is exceeded (HTTP 402 "You exceeded the quota"): thrown
    /// for the request that got it and, without a request, for every request
    /// until <see cref="BlockedUntil"/>.
    /// </summary>
    public class KinopoiskQuotaExceededException : Exception
    {
        public DateTime BlockedUntil { get; }

        public KinopoiskQuotaExceededException(DateTime blockedUntil, Exception innerException = null)
            : base($"Kinopoisk API quota exceeded, requests blocked until {blockedUntil:u}", innerException)
        {
            BlockedUntil = blockedUntil;
        }
    }
}
