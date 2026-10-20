using System.Collections.Generic;

namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchSearchResult
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? OriginalTitle { get; set; }

        public int? Year { get; set; }

        public string? ObjectType { get; set; }

        public string? PosterUrl { get; set; }

        public string? JustWatchUrl { get; set; }

        public string Country { get; set; } = string.Empty;

        public IReadOnlyList<JustWatchOffer> Offers { get; set; } = new List<JustWatchOffer>();

        public IReadOnlyList<string> Providers { get; set; } = new List<string>();
    }
}
