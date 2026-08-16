using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.JustWatch.Api;

[Route("/JustWatch/Search", "GET", Summary = "Search JustWatch by title.")]
public sealed class SearchJustWatch : IReturn<IReadOnlyList<JustWatchSearchResult>>
{
    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string? MediaType { get; set; }
}

public sealed class JustWatchService : IService
{
    public Task<IReadOnlyList<JustWatchSearchResult>> Get(SearchJustWatch request)
    {
        var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
        var client = new JustWatchClient(new HttpClient(), options);

        return client.SearchAsync(request.Title, request.Year, request.MediaType, CancellationToken.None);
    }
}
