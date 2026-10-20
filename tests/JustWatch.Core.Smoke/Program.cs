using System.Net;
using System.Text;
using System.Text.Json;
using JustWatch.Plugin.Core;

var handler = new StubHandler();
using var http = new HttpClient(handler);
var client = new JustWatchClient(http, new JustWatchOptions { Country = "ca", Language = "en" });
handler.Body = """
{"data":{"popularTitles":{"edges":[{"node":{"id":"tm205861","objectType":"MOVIE","content":{"title":"Arrival","originalReleaseYear":2016,"fullPath":"/ca/movie/arrival-2016"},"offers":[{"monetizationType":"RENT","presentationType":"HD","retailPrice":"CA$ 4.99","currency":"CAD","standardWebURL":"https://example.com/watch","package":{"packageId":2,"clearName":"Example"}},{"monetizationType":"BUY","standardWebURL":"javascript:alert(1)","package":{"packageId":3,"clearName":"Another"}}]}}]}}}
""";
var results = await client.SearchAsync("Arrival", 2016, "movie", default);
Check(results.Count == 1 && results[0].Country == "CA", "title and normalized country");
Check(results[0].JustWatchUrl == "https://www.justwatch.com/ca/movie/arrival-2016", "title link");
Check(results[0].Offers[0].ProviderId == "2" && results[0].Offers[0].Price == "CA$ 4.99", "numeric provider and price");
Check(results[0].Offers[1].Url == null, "unsafe offer URL rejected");
using (var request = JsonDocument.Parse(handler.Request!))
{
    Check(request.RootElement.GetProperty("variables").GetProperty("filter").GetProperty("objectTypes")[0].GetString() == "MOVIE", "movie filter");
}
Check((await client.SearchAsync("Arrival", 1990, "movie", default)).Count == 0, "year mismatch");
Check((await client.SearchAsync(" ", null, null, default)).Count == 0, "blank title");
handler.Body = "{\"errors\":[{\"message\":\"failed\"}]}";
await ExpectFailure(() => client.SearchAsync("Arrival", null, null, default));
handler.Body = "{}";
await ExpectFailure(() => client.SearchAsync("Arrival", null, null, default));
Console.WriteLine("Shared client smoke checks passed.");
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
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Request = await request.Content!.ReadAsStringAsync(token);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
    }
}
