using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.Plugin.JustWatch.Api
{
    [Route("/JustWatch/Search", "GET", Summary = "Search JustWatch by title.")]
    public sealed class SearchJustWatch : IReturn<IReadOnlyList<JustWatchSearchResult>>
    {
        public string Title { get; set; } = string.Empty;

        public int? Year { get; set; }

        public string? MediaType { get; set; }
    }

    [Authenticated(Roles = "Admin")]
    [Route("/JustWatch/Seerr/Details", "GET")]
    public sealed class GetSeerrDetails : IReturn<SeerrDetails>
    {
        public int TmdbId { get; set; }
        public string MediaType { get; set; } = string.Empty;
    }

    [Authenticated(Roles = "Admin")]
    [Route("/JustWatch/Seerr/Request", "POST")]
    public sealed class RequestSeerrTitle : IReturn<SeerrResult>
    {
        public int TmdbId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public int[] Seasons { get; set; } = Array.Empty<int>();
    }

    public sealed class JustWatchService : IService
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly HttpClient SeerrHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

        public async Task<SeerrDetails> Get(GetSeerrDetails request)
        {
            try { return await new SeerrClient(SeerrHttp, Plugin.Instance!.Configuration.Options).DetailsAsync(request.TmdbId, request.MediaType, CancellationToken.None); }
            catch (Exception ex) when (IsSeerrFailure(ex)) { return new SeerrDetails { ErrorMessage = "Could not load seasons. Check your Seerr URL and API key." }; }
        }

        public async Task<SeerrResult> Post(RequestSeerrTitle request)
        {
            try { return await new SeerrClient(SeerrHttp, Plugin.Instance!.Configuration.Options).RequestAsync(new SeerrRequest { TmdbId = request.TmdbId, MediaType = request.MediaType, Seasons = request.Seasons }, CancellationToken.None); }
            catch (Exception ex) when (IsSeerrFailure(ex)) { return new SeerrResult { Success = false, Message = "Could not submit the request. Check your Seerr URL, API key, and selected seasons." }; }
        }

        private static bool IsSeerrFailure(Exception ex) => ex is ArgumentException || ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException;

        public Task<IReadOnlyList<JustWatchSearchResult>> Get(SearchJustWatch request)
        {
            var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
            var client = new JustWatchClient(Http, options);

            return client.SearchAsync(request.Title, request.Year, request.MediaType, CancellationToken.None);
        }
    }
}
