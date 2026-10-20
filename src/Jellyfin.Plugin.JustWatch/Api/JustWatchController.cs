using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Jellyfin.Plugin.JustWatch.Api
{
    [Authorize]
    [ApiController]
    [Route("JustWatch")]
    public sealed class JustWatchController : ControllerBase
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
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
