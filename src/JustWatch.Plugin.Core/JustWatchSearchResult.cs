using System.Collections.Generic;

namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchSearchResponse
    {
        public IReadOnlyList<JustWatchSearchResult> Results { get; set; } = new List<JustWatchSearchResult>();
        public IReadOnlyList<string> FailedCountries { get; set; } = new List<string>();
        public IReadOnlyList<string> SearchedCountries { get; set; } = new List<string>();
    }

    public sealed class JustWatchSearchResult
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? OriginalTitle { get; set; }

        public int? Year { get; set; }

        public string? ObjectType { get; set; }

        public string? PosterUrl { get; set; }

        public string? JustWatchUrl { get; set; }

        public bool CanRequestInSeerr { get; set; }

        public int? TmdbId { get; set; }

        public string? SeerrUrl { get; set; }

        public string Country { get; set; } = string.Empty;

        public IReadOnlyList<JustWatchOffer> Offers { get; set; } = new List<JustWatchOffer>();

        public IReadOnlyList<string> Providers { get; set; } = new List<string>();
    }
}
