using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JustWatch.Plugin.Core
{
    public sealed partial class JustWatchClient
    {
        private sealed class ProviderCache
        {
            public DateTime Expires { get; set; }
            public IReadOnlyList<JustWatchProvider> Providers { get; set; } = Array.Empty<JustWatchProvider>();
        }
        private static readonly ConcurrentDictionary<string, ProviderCache> ProviderCatalog = new ConcurrentDictionary<string, ProviderCache>();

        private async Task<JsonDocument> DiscoverQueryAsync(string query, object variables, CancellationToken token)
        {
            using var body = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(_options.BaseUrl.TrimEnd('/') + "/graphql", body, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            { document.Dispose(); throw new HttpRequestException("JustWatch could not process Discover. Try again later."); }
            return document;
        }

        public async Task<IReadOnlyList<JustWatchProvider>> ProvidersAsync(string country, CancellationToken token)
        {
            if (!IsCode(country)) throw new ArgumentException("Choose a two-letter country code.");
            country = country.ToUpperInvariant();
            var key = _options.BaseUrl + ":" + country;
            if (ProviderCatalog.TryGetValue(key, out var cached) && cached.Expires > DateTime.UtcNow) return cached.Providers;
            using var document = await DiscoverQueryAsync("query($country:Country!){packages(country:$country,platform:WEB){packageId clearName shortName}}", new { country }, token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("packages", out var packages) || packages.ValueKind != JsonValueKind.Array)
                throw new HttpRequestException("JustWatch returned an unexpected service list.");
            var providers = packages.EnumerateArray().Select(package => new JustWatchProvider
            { Id = GetString(package, "packageId") ?? string.Empty, Code = GetString(package, "shortName") ?? string.Empty, Name = GetString(package, "clearName") ?? "Unknown service" })
                .Where(provider => provider.Id.Length > 0 && provider.Code.Length > 0).GroupBy(provider => provider.Code).Select(group => group.First()).OrderBy(provider => provider.Name).ToArray();
            if (ProviderCatalog.Count >= 32) ProviderCatalog.Clear();
            ProviderCatalog[key] = new ProviderCache { Providers = providers, Expires = DateTime.UtcNow.AddHours(1) };
            return providers;
        }

        public async Task<JustWatchDiscoverResponse> DiscoverAsync(string country, string? mediaType, string? providerCodes, string? after, CancellationToken token)
        {
            if (!IsCode(country) || !IsCode(_options.Language)) throw new ArgumentException("Country and language must be two-letter codes.");
            if (!string.IsNullOrEmpty(mediaType) && mediaType != "movie" && mediaType != "series") throw new ArgumentException("Choose movies, series, or both.");
            if (after != null && after.Length > 512) throw new ArgumentException("Invalid Discover page cursor.");
            country = country.ToUpperInvariant();
            if (providerCodes != null && providerCodes.Length > 256) throw new ArgumentException("The selected service list is too long.");
            var codes = (providerCodes ?? string.Empty).Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToArray();
            if (codes.Length > 8) throw new ArgumentException("Choose at most eight streaming services.");
            var catalog = await ProvidersAsync(country, token).ConfigureAwait(false);
            if (codes.Any(code => !catalog.Any(provider => provider.Code == code))) throw new ArgumentException("A selected streaming service is not available in this country. Reload the service list.");
            var selectedIds = catalog.Where(provider => codes.Contains(provider.Code)).Select(provider => provider.Id).ToArray();
            var parser = new JustWatchClient(_httpClient, new JustWatchOptions { Country = country, Language = _options.Language, SeerrUrl = _options.SeerrUrl, SeerrApiKey = _options.SeerrApiKey });
            using var document = await DiscoverQueryAsync(DiscoverQuery, new
            {
                country, language = _options.Language.ToLowerInvariant(), after = string.IsNullOrWhiteSpace(after) ? null : after,
                filter = new { packages = codes, monetizationTypes = new[] { "FLATRATE" }, objectTypes = mediaType == "movie" ? new[] { "MOVIE" } : mediaType == "series" ? new[] { "SHOW" } : new[] { "MOVIE", "SHOW" } }
            }, token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("popularTitles", out var popular) || popular.ValueKind != JsonValueKind.Object
                || !popular.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
                throw new HttpRequestException("JustWatch returned an unexpected Discover response.");
            var results = edges.EnumerateArray().Select(edge => parser.ParseResult(edge.GetProperty("node")))
                .Where(item => item.Offers.Any(offer => offer.MonetizationType == "FLATRATE" && (selectedIds.Length == 0 || selectedIds.Contains(offer.ProviderId)))).ToArray();
            var next = popular.TryGetProperty("pageInfo", out var pageInfo) && pageInfo.ValueKind == JsonValueKind.Object
                && pageInfo.TryGetProperty("hasNextPage", out var hasNext) && hasNext.ValueKind == JsonValueKind.True ? GetString(pageInfo, "endCursor") : null;
            return new JustWatchDiscoverResponse { Results = results, Country = country, NextCursor = next == after ? null : next, HasNextPage = !string.IsNullOrEmpty(next) && next != after };
        }

        private const string DiscoverQuery = @"
query Discover($country:Country!,$language:Language!,$after:String,$filter:TitleFilter!){
 popularTitles(country:$country,filter:$filter,first:12,after:$after,sortBy:POPULAR,sortRandomSeed:0){
  edges{node{id objectType content(country:$country,language:$language){title originalReleaseYear fullPath posterUrl(profile:S166) externalIds{tmdbId}}
   offers(country:$country,platform:WEB,filter:{bestOnly:false}){monetizationType presentationType retailPrice(language:$language) currency standardWebURL package{packageId clearName}}}}
  pageInfo{endCursor hasNextPage}
 }
}";
    }
}
