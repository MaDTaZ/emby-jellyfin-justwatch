using System.Net;
using System.Text;
using System.Text.Json;
using JustWatch.Plugin.Core;

static class SeerrSmoke
{
    public static async Task Run()
    {
        var handler = new FakeSeerr();
        using var http = new HttpClient(handler);
        var client = new SeerrClient(http, new JustWatchOptions { SeerrUrl = "https://example.com/seerr/login", SeerrApiKey = "test-api-key" });
        var movie = await client.RequestAsync(new SeerrRequest { TmdbId = 329865, MediaType = "movie" }, default);
        Check(movie.Success && movie.Message.Contains("approval"), "movie pending approval");
        Check(handler.LastPath == "/seerr/api/v1/request" && handler.Key == "test-api-key", "API path and server-side key header");
        using (var body = JsonDocument.Parse(handler.LastBody!))
        {
            Check(body.RootElement.GetProperty("mediaId").GetInt32() == 329865, "exact media ID");
            Check(!body.RootElement.TryGetProperty("apiKey", out _), "API key absent from body");
        }
        var tv = await client.RequestAsync(new SeerrRequest { TmdbId = 1396, MediaType = "tv", Seasons = new[] { 1, 2, 2 } }, default);
        using (var body = JsonDocument.Parse(handler.LastBody!)) Check(body.RootElement.GetProperty("seasons").GetArrayLength() == 2, "explicit distinct TV seasons");
        Check(tv.Success, "TV request submitted");
        var before = handler.Posts;
        await Invalid(() => client.RequestAsync(new SeerrRequest { TmdbId = 1396, MediaType = "tv" }, default));
        await Invalid(() => client.RequestAsync(new SeerrRequest { TmdbId = 1396, MediaType = "tv", Seasons = new[] { 99 } }, default));
        Check(handler.Posts == before, "invalid seasons never submitted");
        handler.Available = true;
        Check(!(await client.RequestAsync(new SeerrRequest { TmdbId = 1, MediaType = "movie" }, default)).Success, "available title not requested");
        Check(handler.Posts == before, "available preflight avoids POST");
        handler.Available = false;
        handler.PostStatus = HttpStatusCode.Conflict;
        Check((await client.RequestAsync(new SeerrRequest { TmdbId = 1, MediaType = "movie" }, default)).Message.Contains("already"), "duplicate status");
        handler.PostStatus = HttpStatusCode.Unauthorized;
        var rejected = await client.RequestAsync(new SeerrRequest { TmdbId = 1, MediaType = "movie" }, default);
        Check(!rejected.Success && !rejected.Message.Contains("test-api-key"), "bad key sanitized");
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    static async Task Invalid(Func<Task<SeerrResult>> action)
    {
        try { await action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected validation failure");
    }
    sealed class FakeSeerr : HttpMessageHandler
    {
        public bool Available;
        public HttpStatusCode PostStatus = HttpStatusCode.Created;
        public string? LastBody, LastPath, Key;
        public int Posts;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Key = request.Headers.GetValues("X-Api-Key").Single();
            LastPath = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, "{\"mediaInfo\":{\"status\":" + (Available ? 5 : 1) + "},\"seasons\":[{\"seasonNumber\":1,\"name\":\"Season 1\"},{\"seasonNumber\":2,\"name\":\"Season 2\"}]}");
            Posts++;
            LastBody = await request.Content!.ReadAsStringAsync(token);
            return Json(PostStatus, "{\"status\":1}");
        }
        static HttpResponseMessage Json(HttpStatusCode code, string text) => new(code) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }
}
