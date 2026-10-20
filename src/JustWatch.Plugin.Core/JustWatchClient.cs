using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchClient : IJustWatchClient
    {
        private readonly HttpClient _httpClient;
        private readonly JustWatchOptions _options;

        public JustWatchClient(HttpClient httpClient, JustWatchOptions options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public async Task<IReadOnlyList<JustWatchSearchResult>> SearchAsync(
            string title,
            int? year,
            string? mediaType,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Array.Empty<JustWatchSearchResult>();
            }

            if (title.Length > 200) throw new ArgumentException("Title must be at most 200 characters.", nameof(title));
            if (!IsCode(_options.Country) || !IsCode(_options.Language))
                throw new ArgumentException("Country and language must be two-letter codes.");
            if (!string.IsNullOrEmpty(mediaType) && !string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mediaType, "series", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Media type must be movie or series.", nameof(mediaType));

            var body = JsonSerializer.Serialize(new
            {
                query = SearchQuery,
                variables = new
                {
                    country = _options.Country.ToUpperInvariant(),
                    language = _options.Language.ToLowerInvariant(),
                    first = Math.Min(20, Math.Max(1, _options.ResultLimit)),
                    filter = new { searchQuery = title, objectTypes = string.Equals(mediaType, "series", StringComparison.OrdinalIgnoreCase)
                        ? new[] { "SHOW" } : string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase)
                        ? new[] { "MOVIE" } : new[] { "MOVIE", "SHOW" } }
                }
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_options.BaseUrl.TrimEnd('/')}/graphql", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                throw new HttpRequestException("JustWatch could not process the availability lookup.");
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("popularTitles", out var titles) || titles.ValueKind != JsonValueKind.Object
                || !titles.TryGetProperty("edges", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new HttpRequestException("JustWatch returned an unexpected response.");

            return items.EnumerateArray()
                .Select(edge => ParseResult(edge.GetProperty("node")))
                .Where(result => IsLikelyMatch(result, title, year))
                .Take(Math.Max(1, _options.ResultLimit))
                .ToList();
        }

        private const string SearchQuery = @"
query WhereToWatch($country: Country!, $language: Language!, $first: Int!, $filter: TitleFilter!) {
  popularTitles(country: $country, filter: $filter, first: $first, sortBy: POPULAR, sortRandomSeed: 0) {
    edges { node {
      id objectType
      content(country: $country, language: $language) { title originalReleaseYear fullPath posterUrl(profile: S166) externalIds { tmdbId } }
      offers(country: $country, platform: WEB, filter: {bestOnly: false}) {
        monetizationType presentationType retailPrice(language: $language) currency standardWebURL
        package { packageId clearName }
      }
    } }
  }
}";

        private static bool IsCode(string? value) => value != null && value.Length == 2
            && value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        private JustWatchSearchResult ParseResult(JsonElement item)
        {
            var content = item.GetProperty("content");
            var result = new JustWatchSearchResult
            {
                Id = GetString(item, "id") ?? string.Empty,
                Title = GetString(content, "title") ?? string.Empty,
                Year = GetInt(content, "originalReleaseYear"),
                ObjectType = GetString(item, "objectType"),
                PosterUrl = SafeUrl(GetString(content, "posterUrl"), "https://images.justwatch.com"),
                Country = _options.Country.ToUpperInvariant(),
                JustWatchUrl = SafeUrl(GetString(content, "fullPath"), "https://www.justwatch.com")
            };
            if (content.TryGetProperty("externalIds", out var externalIds) && externalIds.ValueKind == JsonValueKind.Object)
            {
                result.TmdbId = GetInt(externalIds, "tmdbId");
                result.SeerrUrl = SeerrLinks.ForTitle(_options.SeerrUrl, result.TmdbId, result.ObjectType);
                result.CanRequestInSeerr = result.SeerrUrl != null && !string.IsNullOrWhiteSpace(_options.SeerrApiKey);
            }
            if (item.TryGetProperty("offers", out var offers) && offers.ValueKind == JsonValueKind.Array)
            {
                result.Offers = offers.EnumerateArray().Select(offer =>
                {
                    var package = offer.GetProperty("package");
                    return new JustWatchOffer
                    {
                        ProviderId = GetString(package, "packageId") ?? string.Empty,
                        ProviderName = GetString(package, "clearName") ?? "Unknown provider",
                        MonetizationType = GetString(offer, "monetizationType") ?? "OTHER",
                        Quality = GetString(offer, "presentationType"),
                        Price = GetString(offer, "retailPrice"),
                        Currency = GetString(offer, "currency"),
                        Url = SafeUrl(GetString(offer, "standardWebURL"))
                    };
                }).ToList();
                result.Providers = result.Offers.Select(o => o.ProviderName).Distinct().ToList();
            }
            return result;
        }

        private static string? SafeUrl(string? value, string? baseUrl = null)
        {
            if (value == null || string.IsNullOrWhiteSpace(value)) return null;
            if (baseUrl != null && value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal)) value = baseUrl + value;
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http") ? uri.AbsoluteUri : null;
        }

        private static bool IsLikelyMatch(JustWatchSearchResult result, string title, int? year)
        {
            if (string.IsNullOrWhiteSpace(result.Title))
            {
                return false;
            }

            if (year.HasValue && result.Year.HasValue && Math.Abs(result.Year.Value - year.Value) > 1)
            {
                return false;
            }

            return result.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf(result.Title, StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(result.OriginalTitle, title, StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetString(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out var value) && (value.ValueKind == JsonValueKind.String || value.ValueKind == JsonValueKind.Number)
                ? value.ToString()
                : null;
        }

        private static int? GetInt(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var value)) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            return value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null;
        }

    }
}
