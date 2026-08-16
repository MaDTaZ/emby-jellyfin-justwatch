using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JustWatch.Plugin.Core;

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

        var locale = string.Create(
            CultureInfo.InvariantCulture,
            $"{_options.Language}_{_options.Country.ToUpperInvariant()}");
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/content/titles/{locale}/popular";
        var body = CreateSearchBody(title, mediaType);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JustWatchSearchResult>();
        }

        return items.EnumerateArray()
            .Select(ParseResult)
            .Where(result => IsLikelyMatch(result, title, year))
            .Take(Math.Max(1, _options.ResultLimit))
            .ToList();
    }

    private static string CreateSearchBody(string title, string? mediaType)
    {
        var objectTypes = string.Equals(mediaType, "series", StringComparison.OrdinalIgnoreCase)
            ? "[\"show\"]"
            : string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase)
                ? "[\"movie\"]"
                : "[\"movie\",\"show\"]";

        return $$"""
        {
          "content_types": {{objectTypes}},
          "page": 1,
          "page_size": 20,
          "query": {{JsonSerializer.Serialize(title)}}
        }
        """;
    }

    private static JustWatchSearchResult ParseResult(JsonElement item)
    {
        var result = new JustWatchSearchResult
        {
            Id = GetString(item, "id") ?? string.Empty,
            Title = GetString(item, "title") ?? string.Empty,
            OriginalTitle = GetString(item, "original_title"),
            Year = GetInt(item, "original_release_year"),
            ObjectType = GetString(item, "object_type"),
            PosterUrl = NormalizePoster(GetString(item, "poster"))
        };

        if (item.TryGetProperty("full_path", out var fullPath) && fullPath.ValueKind == JsonValueKind.String)
        {
            result.JustWatchUrl = "https://www.justwatch.com" + fullPath.GetString();
        }

        if (item.TryGetProperty("offers", out var offers) && offers.ValueKind == JsonValueKind.Array)
        {
            result.Providers = offers.EnumerateArray()
                .Select(offer => GetString(offer, "provider_id"))
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();
        }

        return result;
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

        return result.Title.Contains(title, StringComparison.OrdinalIgnoreCase)
            || title.Contains(result.Title, StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.OriginalTitle, title, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static string? NormalizePoster(string? poster)
    {
        if (string.IsNullOrWhiteSpace(poster))
        {
            return null;
        }

        return poster.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? poster
            : "https://images.justwatch.com" + poster.Replace("{profile}", "s592", StringComparison.OrdinalIgnoreCase);
    }
}
