using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
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

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpGet("Seerr/Test")]
        public async Task<ActionResult<SeerrResult>> TestSeerr(CancellationToken token)
        {
            try { return await new SeerrClient(SeerrHttp, Plugin.Instance!.Configuration.Options).TestConnectionAsync(token); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
            { return BadRequest(new SeerrResult { Message = ex is InvalidOperationException ? ex.Message : "Could not connect to Seerr. Check the saved URL and API key." }); }
        }

        [Authorize(Policy = Policies.RequiresElevation)]
        [HttpPost("Maintenance/Cleanup")]
        public ActionResult<SeerrResult> Cleanup([FromServices] IPluginManager manager)
        {
            var current = Plugin.Instance;
            if (current == null) return BadRequest(new SeerrResult { Message = "The active plugin is not ready." });
            var version = current.GetType().Assembly.GetName().Version!;
            var active = manager.Plugins.FirstOrDefault(plugin => ReferenceEquals(plugin.Instance, current));
            if (active == null) return BadRequest(new SeerrResult { Message = "Could not identify the running plugin. Nothing was removed." });
            var backupDirectory = Path.Combine(Directory.GetParent(active.Path)!.Parent!.FullName, "where-to-watch-backups");
            var old = manager.Plugins.Where(plugin => LegacyPluginPolicy.CanArchive(plugin.Id, current.Id, plugin.Version,
                version, plugin.Name, plugin.Manifest.Status.ToString(), plugin.Instance != null)).ToArray();
            var removed = 0;
            try
            {
                foreach (var plugin in old)
                {
                    if ((System.IO.File.GetAttributes(plugin.Path) & FileAttributes.ReparsePoint) != 0) continue;
                    Directory.CreateDirectory(backupDirectory);
                    var backup = Path.Combine(backupDirectory, "plugin-" + plugin.Version + "-" + Guid.NewGuid().ToString("N") + ".zip");
                    ZipFile.CreateFromDirectory(plugin.Path, backup);
                    if (manager.RemovePlugin(plugin)) removed++;
                }
                return new SeerrResult { Success = true, Message = removed == 0 ? "No inactive older entries were found. Nothing was removed."
                    : "Removed " + removed + " inactive older entries after backing them up in where-to-watch-backups beside the plugins folder. Settings were retained. Refresh Plugins; restart Jellyfin if an old entry remains." };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { return BadRequest(new SeerrResult { Message = "Cleanup stopped after removing " + removed + " entries. Existing backups and settings were retained. Check plugin-folder permissions." }); }
        }

        [HttpGet("Providers")]
        public async Task<ActionResult<IReadOnlyList<JustWatchProvider>>> Providers([FromQuery] string country, CancellationToken token)
        {
            try { return Ok(await new JustWatchClient(Http, Plugin.Instance!.Configuration.Options).ProvidersAsync(country, token)); }
            catch (Exception ex) when (ex is ArgumentException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
            { return BadRequest(new SeerrResult { Message = ex is ArgumentException ? ex.Message : "Could not load streaming services. Try again later." }); }
        }

        [HttpGet("Discover")]
        public async Task<ActionResult<JustWatchDiscoverResponse>> Discover([FromQuery] string country, [FromQuery] string? mediaType, [FromQuery] string? providers, [FromQuery] string? after, CancellationToken token)
        {
            try { return await new JustWatchClient(Http, Plugin.Instance!.Configuration.Options).DiscoverAsync(country, mediaType, providers, after, token); }
            catch (Exception ex) when (ex is ArgumentException || ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException)
            { return BadRequest(new SeerrResult { Message = ex is ArgumentException ? ex.Message : "Discover could not load titles. Try again later." }); }
        }

        [HttpGet("Search")]
        public async Task<JustWatchSearchResponse> Search(
            [FromQuery] string title,
            [FromQuery] int? year,
            [FromQuery] string? mediaType,
            [FromQuery] bool anywhere,
            [FromQuery] string? country,
            CancellationToken cancellationToken)
        {
            var options = Plugin.Instance?.Configuration.Options ?? new JustWatchOptions();
            var client = new JustWatchClient(Http, options);

            if (anywhere) return await client.SearchAcrossCountriesDetailedAsync(title, year, mediaType, cancellationToken);
            var results = !string.IsNullOrWhiteSpace(country) ? await client.SearchCountryAsync(country, title, year, mediaType, cancellationToken)
                : await client.SearchAsync(title, year, mediaType, cancellationToken);
            return new JustWatchSearchResponse { Results = results, SearchedCountries = new[] { country ?? options.Country } };
        }
    }
}
