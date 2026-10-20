using System.Net;
using System.Text;
using System.Text.Json;
using JustWatch.Plugin.Core;

var handler = new StubHandler();
using var http = new HttpClient(handler);
var client = new JustWatchClient(http, new JustWatchOptions { Country = "ca", Language = "en", SeerrUrl = "https://seerr.example.com/login" });
handler.Body = """
{"data":{"popularTitles":{"edges":[{"node":{"id":"tm205861","objectType":"MOVIE","content":{"title":"Arrival","originalReleaseYear":2016,"fullPath":"/ca/movie/arrival-2016","externalIds":{"tmdbId":"329865"}},"offers":[{"monetizationType":"RENT","presentationType":"HD","retailPrice":"CA$ 4.99","currency":"CAD","standardWebURL":"https://example.com/watch","package":{"packageId":2,"clearName":"Example"}},{"monetizationType":"BUY","standardWebURL":"javascript:alert(1)","package":{"packageId":3,"clearName":"Another"}}]}}]}}}
""";
var results = await client.SearchAsync("Arrival", 2016, "movie", default);
Check(results.Count == 1 && results[0].Country == "CA", "title and normalized country");
Check(results[0].JustWatchUrl == "https://www.justwatch.com/ca/movie/arrival-2016", "title link");
Check(results[0].Offers[0].ProviderId == "2" && results[0].Offers[0].Price == "CA$ 4.99", "numeric provider and price");
Check(results[0].Offers[1].Url == null, "unsafe offer URL rejected");
Check(results[0].TmdbId == 329865 && results[0].SeerrUrl == "https://seerr.example.com/movie/329865", "exact Seerr movie link");
Check(SeerrLinks.ForTitle("https://example.com/seerr/login/", 1396, "SHOW") == "https://example.com/seerr/tv/1396", "TV and reverse-proxy base path");
Check(SeerrLinks.ForTitle("javascript:alert(1)", 1, "MOVIE") == null, "reject non-HTTP Seerr URLs");
Check(SeerrLinks.ForTitle("https://user:secret@example.com", 1, "MOVIE") == null, "reject credentials in Seerr URL");
Check(SeerrLinks.ForTitle("https://example.com", null, "MOVIE") == null, "no link without matched TMDB ID");
Check(SeerrLinks.ForTitle("", 1, "MOVIE") == null, "optional integration disabled by default");
Check(SeerrLinks.ForTitle("https://example.com", 1, "UNKNOWN") == null, "no incorrect type fallback");
using (var request = JsonDocument.Parse(handler.Request!))
{
    Check(request.RootElement.GetProperty("variables").GetProperty("filter").GetProperty("objectTypes")[0].GetString() == "MOVIE", "movie filter");
}
var anywhereClient = new JustWatchClient(http, new JustWatchOptions { AnywhereCountries = "ca,US,ca,gb", SeerrUrl = "https://seerr.example.com", SeerrApiKey = "test-key" });
var overrideResults = await anywhereClient.SearchCountryAsync("GB", "Arrival", 2016, "movie", default);
Check(overrideResults.Count == 1 && overrideResults[0].Country == "GB" && overrideResults[0].CanRequestInSeerr, "single-country override preserves Seerr integration");
Check((await anywhereClient.SearchAsync("Arrival", 2016, "movie", default))[0].Country == "US", "country override does not mutate saved default");
handler.Countries.Clear();
var anywhere = await anywhereClient.SearchAcrossCountriesAsync("Arrival", 2016, "movie", default);
Check(string.Join(",", anywhere.Select(result => result.Country)) == "CA,US,GB", "Anywhere retains distinct normalized country results in chosen order");
Check(handler.Countries.OrderBy(value => value).SequenceEqual(new[]{"CA","GB","US"}), "one request per distinct chosen country");
Check(anywhere.All(result => result.Offers[0].Currency == "CAD" && result.CanRequestInSeerr), "Anywhere preserves upstream currencies and Seerr integration");
try {
    await new JustWatchClient(http, new JustWatchOptions { AnywhereCountries = "USA" }).SearchAcrossCountriesAsync("Arrival", null, null, default);
    throw new Exception("Expected invalid country rejection");
} catch (ArgumentException) { }
try {
    await new JustWatchClient(http, new JustWatchOptions { AnywhereCountries = "CA,US,GB,AU,FR,DE,IT" }).SearchAcrossCountriesAsync("Arrival", null, null, default);
    throw new Exception("Expected country limit rejection");
} catch (ArgumentException) { }
Check((await client.SearchAsync("Arrival", 1990, "movie", default)).Count == 0, "year mismatch");
Check((await client.SearchAsync(" ", null, null, default)).Count == 0, "blank title");
handler.Body = "{\"errors\":[{\"message\":\"failed\"}]}";
await ExpectFailure(() => client.SearchAsync("Arrival", null, null, default));
handler.Body = "{}";
await ExpectFailure(() => client.SearchAsync("Arrival", null, null, default));
await SeerrSmoke.Run();
if (args.Contains("--live"))
{
    using var liveHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    var live = await new JustWatchClient(liveHttp, new JustWatchOptions { AnywhereCountries = "CA,US", ResultLimit = 2 })
        .SearchAcrossCountriesAsync("Arrival", 2016, "movie", default);
    Check(live.Any(result => result.Country == "CA") && live.Any(result => result.Country == "US"), "live CA and US results");
    Check(live.All(result => result.TmdbId > 0), "live matched TMDB IDs");
    Console.WriteLine("Live Anywhere CA/US search passed: " + live.Count + " country-labeled results.");
}
Console.WriteLine("Shared client and direct Seerr request checks passed.");
static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
static async Task ExpectFailure(Func<Task<IReadOnlyList<JustWatchSearchResult>>> action)
{
    try { await action(); } catch (HttpRequestException) { return; }
    throw new Exception("Expected upstream failure");
}
sealed class StubHandler : HttpMessageHandler
{
    public string Body = "{}";
    public string? Request;
    public System.Collections.Concurrent.ConcurrentBag<string> Countries = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Request = await request.Content!.ReadAsStringAsync(token);
        using var document = JsonDocument.Parse(Request);
        Countries.Add(document.RootElement.GetProperty("variables").GetProperty("country").GetString()!);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
    }
}
