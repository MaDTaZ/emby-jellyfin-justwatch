using System.Collections.Generic;

namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchProvider
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public sealed class JustWatchDiscoverResponse
    {
        public IReadOnlyList<JustWatchSearchResult> Results { get; set; } = new List<JustWatchSearchResult>();
        public string? NextCursor { get; set; }
        public bool HasNextPage { get; set; }
        public string Country { get; set; } = string.Empty;
    }
}
