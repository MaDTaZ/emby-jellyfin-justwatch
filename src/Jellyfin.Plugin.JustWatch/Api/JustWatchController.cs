using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JustWatch.Plugin.Core;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JustWatch.Api
{
    [ApiController]
    [Route("JustWatch")]
    public sealed class JustWatchController : ControllerBase
    {
        [HttpGet("Search")]
        public Task<IReadOnlyList<JustWatchSearchResult>> Search(
            [FromQuery] string title,
            [FromQuery] int? year,
            [FromQuery] string? mediaType,
            CancellationToken cancellationToken)
        {
            var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
            var client = new JustWatchClient(new HttpClient(), options);

            return client.SearchAsync(title, year, mediaType, cancellationToken);
        }
    }
}
