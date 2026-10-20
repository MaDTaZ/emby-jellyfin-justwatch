using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JustWatch.Plugin.Core
{
    public sealed class SeerrRequest
    {
        public int TmdbId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public int[] Seasons { get; set; } = Array.Empty<int>();
    }

    public sealed class SeerrResult
    {
        public string? State { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SeerrSeason
    {
        public int Number { get; set; }
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = "notRequested";
        public bool CanRequest { get; set; } = true;
    }

    public sealed class SeerrDetails
    {
        public string? ErrorMessage { get; set; }
        public bool Available { get; set; }
        public string State { get; set; } = "notRequested";
        public bool CanRequest { get; set; }
        public IReadOnlyList<SeerrSeason> Seasons { get; set; } = Array.Empty<SeerrSeason>();
    }

    public sealed class SeerrClient
    {
        private readonly HttpClient _http;
        private readonly JustWatchOptions _options;

        public SeerrClient(HttpClient http, JustWatchOptions options) { _http = http; _options = options; }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            var titleUrl = SeerrLinks.ForTitle(_options.SeerrUrl, 1, "MOVIE");
            if (titleUrl == null || string.IsNullOrWhiteSpace(_options.SeerrApiKey))
                throw new InvalidOperationException("Save a valid Seerr URL and API key in JustWatch settings first.");
            if (_options.SeerrApiKey.Any(c => c <= 32 || c >= 127))
                throw new InvalidOperationException("The Seerr API key contains invalid characters. Copy it again from Seerr settings.");
            var baseUrl = titleUrl.Substring(0, titleUrl.Length - "movie/1".Length);
            var request = new HttpRequestMessage(method, baseUrl + "api/v1/" + path);
            request.Headers.Add("X-Api-Key", _options.SeerrApiKey.Trim());
            return request;
        }

        private static void Validate(int id, string type)
        {
            if (id <= 0 || (type != "movie" && type != "tv")) throw new ArgumentException("Select a valid movie or TV show.");
        }

        public async Task<SeerrDetails> DetailsAsync(int id, string type, CancellationToken token)
        {
            Validate(id, type);
            using var request = CreateRequest(HttpMethod.Get, type + "/" + id);
            using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException(Failure(response.StatusCode));
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var root = doc.RootElement;
            var state = "notRequested";
            var seasonStates = new Dictionary<int, string>();
            if (root.TryGetProperty("mediaInfo", out var media) && media.ValueKind == JsonValueKind.Object)
            {
                state = MediaState(media);
                if (media.TryGetProperty("seasons", out var storedSeasons) && storedSeasons.ValueKind == JsonValueKind.Array)
                    foreach (var season in storedSeasons.EnumerateArray())
                        if (TryNumber(season, "seasonNumber", out var n)) seasonStates[n] = MediaState(season);
                if (media.TryGetProperty("requests", out var requests) && requests.ValueKind == JsonValueKind.Array)
                    foreach (var entry in requests.EnumerateArray())
                    {
                        if (entry.TryGetProperty("is4k", out var is4k) && is4k.ValueKind == JsonValueKind.True) continue;
                        if (!TryNumber(entry, "status", out var requestStatus) || (requestStatus != 1 && requestStatus != 2)) continue;
                        var requestState = requestStatus == 1 ? "pending" : "requested";
                        if (state == "notRequested") state = requestState;
                        if (entry.TryGetProperty("seasons", out var requestedSeasons) && requestedSeasons.ValueKind == JsonValueKind.Array)
                            foreach (var season in requestedSeasons.EnumerateArray())
                                if (TryNumber(season, "seasonNumber", out var n)
                                    && (!seasonStates.TryGetValue(n, out var old) || old == "notRequested")) seasonStates[n] = requestState;
                    }
            }
            var seasons = new List<SeerrSeason>();
            if (root.TryGetProperty("seasons", out var array) && array.ValueKind == JsonValueKind.Array)
                foreach (var season in array.EnumerateArray())
                    if (TryNumber(season, "seasonNumber", out var n) && n >= 0)
                    {
                        var seasonState = seasonStates.TryGetValue(n, out var stored) ? stored : "notRequested";
                        seasons.Add(new SeerrSeason {
                            Number = n, Name = season.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() ?? "Season " + n : "Season " + n,
                            State = seasonState, CanRequest = state != "available" && state != "blocked" && Requestable(seasonState)
                        });
                    }
            return new SeerrDetails {
                Available = state == "available", State = state,
                CanRequest = type == "tv" ? seasons.Any(season => season.CanRequest) : Requestable(state),
                Seasons = seasons.OrderBy(season => season.Number).ToList()
            };
        }

        private static bool TryNumber(JsonElement item, string name, out int number)
        {
            number = 0;
            return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number);
        }

        private static string MediaState(JsonElement media)
        {
            if (!TryNumber(media, "status", out var status)) return "notRequested";
            switch (status)
            {
                case 2: return "pending";
                case 3: return "requested";
                case 4: return "partial";
                case 5: return "available";
                case 6: return "blocked";
                default: return "notRequested";
            }
        }

        private static bool Requestable(string state) => state == "notRequested" || state == "partial";

        public async Task<SeerrResult> RequestAsync(SeerrRequest input, CancellationToken token)
        {
            Validate(input.TmdbId, input.MediaType);
            var seasons = (input.Seasons ?? Array.Empty<int>()).Distinct().ToArray();
            if (input.MediaType == "tv" && (seasons.Length == 0 || seasons.Any(s => s < 0) || seasons.Length > 200))
                throw new ArgumentException("Choose at least one season to request.");
            // A preflight also avoids submitting fully available titles.
            var details = await DetailsAsync(input.TmdbId, input.MediaType, token).ConfigureAwait(false);
            if (!details.CanRequest) return new SeerrResult { Success = false, State = details.State,
                Message = details.Available ? "Already available in Seerr." : details.State == "blocked" ? "This title is blocklisted in Seerr." : "Already requested or waiting for approval in Seerr." };
            if (input.MediaType == "tv" && seasons.Any(n => !details.Seasons.Any(s => s.Number == n)))
                throw new ArgumentException("One of the selected seasons is no longer available. Choose seasons again.");
            if (input.MediaType == "tv" && seasons.Any(n => !details.Seasons.First(s => s.Number == n).CanRequest))
                return new SeerrResult { Success = false, State = details.State, Message = "Some selected seasons are already available or requested. Choose seasons again." };
            var payload = new Dictionary<string, object> { ["mediaType"] = input.MediaType, ["mediaId"] = input.TmdbId };
            if (input.MediaType == "tv") payload["seasons"] = seasons;
            using var request = CreateRequest(HttpMethod.Post, "request");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new SeerrResult { Success = false, Message = Failure(response.StatusCode) };
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var pending = doc.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Number && status.GetInt32() == 1;
            return new SeerrResult { Success = true, State = pending ? "pending" : "requested", Message = pending ? "Requested — waiting for approval in Seerr." : "Request accepted by Seerr." };
        }

        private static string Failure(HttpStatusCode status)
        {
            if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden) return "Seerr rejected the API key or request permission. Check the saved settings.";
            if (status == HttpStatusCode.Conflict) return "This title or these seasons have already been requested.";
            if ((int)status >= 300 && (int)status < 400) return "Seerr redirected the connection. Save its final URL in settings.";
            return "Seerr could not complete the request. Check whether it is already requested or available, then try again.";
        }
    }
}
