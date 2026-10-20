using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.JustWatch.Api
{
    [Route("/JustWatch/Search", "GET", Summary = "Search JustWatch by title.")]
    public sealed class SearchJustWatch : IReturn<IReadOnlyList<JustWatchSearchResult>>
    {
        public string Title { get; set; } = string.Empty;

        public int? Year { get; set; }

        public string? MediaType { get; set; }
    }

    public sealed class JustWatchService : IService
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        public Task<IReadOnlyList<JustWatchSearchResult>> Get(SearchJustWatch request)
        {
            var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
            var client = new JustWatchClient(Http, options);

            return client.SearchAsync(request.Title, request.Year, request.MediaType, CancellationToken.None);
        }
    }
}
