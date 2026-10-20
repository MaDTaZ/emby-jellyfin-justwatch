using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediaBrowser.Common.Api;

namespace Jellyfin.Plugin.JustWatch.Api
{
    [Authorize]
    [ApiController]
    [Route("JustWatch")]
    public sealed class JustWatchController : ControllerBase
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly HttpClient SeerrHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet("Seerr/Details")]
        public async Task<ActionResult<SeerrDetails>> SeerrDetails([FromQuery] int tmdbId, [FromQuery] string mediaType, CancellationToken token)
        {
            try { return await new SeerrClient(SeerrHttp, Plugin.Instance!.Configuration.Options).DetailsAsync(tmdbId, mediaType, token); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
            { return BadRequest(new SeerrResult { Success = false, Message = ex is ArgumentException || ex is InvalidOperationException ? ex.Message : "Could not contact Seerr. Check its URL and API key, then try again." }); }
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpPost("Seerr/Request")]
        public async Task<ActionResult<SeerrResult>> SeerrRequest([FromBody] SeerrRequest input, CancellationToken token)
        {
            try { return await new SeerrClient(SeerrHttp, Plugin.Instance!.Configuration.Options).RequestAsync(input, token); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
            { return BadRequest(new SeerrResult { Success = false, Message = ex is ArgumentException || ex is InvalidOperationException ? ex.Message : "Could not contact Seerr. Check its URL and API key, then try again." }); }
        }

        [HttpGet("Search")]
        public Task<IReadOnlyList<JustWatchSearchResult>> Search(
            [FromQuery] string title,
            [FromQuery] int? year,
            [FromQuery] string? mediaType,
            CancellationToken cancellationToken)
        {
            var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
            var client = new JustWatchClient(Http, options);

            return client.SearchAsync(title, year, mediaType, cancellationToken);
        }
    }
}
