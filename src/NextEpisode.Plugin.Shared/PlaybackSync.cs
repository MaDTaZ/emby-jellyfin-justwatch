using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
#if EMBY
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
namespace Emby.Plugin.NextEpisode
#else
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Jellyfin.Plugin.NextEpisode
#endif
{
#if !EMBY
    public sealed class Registration : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection services, MediaBrowser.Controller.IServerApplicationHost host)
            => services.AddHostedService<PlaybackSync>();
    }
#endif
    public sealed class PlaybackSync :
#if EMBY
        IServerEntryPoint
#else
        IHostedService, IDisposable
#endif
    {
        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri("https://next-episode.net/api/kodi/v1/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        private readonly ISessionManager sessions;
        private readonly ILogger log;
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly object gate = new object();
        private readonly HashSet<string> sent = new HashSet<string>();
        private readonly HashSet<string> pending = new HashSet<string>();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1);
        private DateTimeOffset retryAfter;
        private Settings? connected;
        private string? token;

#if EMBY
        public PlaybackSync(ISessionManager sessions, ILogManager logs)
        {
            this.sessions = sessions;
            log = logs.GetLogger("Next Episode Sync");
        }

        public void Run() => Subscribe();
#else
        public PlaybackSync(ISessionManager sessions, ILogger<PlaybackSync> log)
        {
            this.sessions = sessions;
            this.log = log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Subscribe();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }
#endif
        private void Subscribe()
        {
            sessions.PlaybackProgress += Progress;
            sessions.PlaybackStopped += Stopped;
        }

        public void Dispose()
        {
            sessions.PlaybackProgress -= Progress;
            sessions.PlaybackStopped -= Stopped;
            shutdown.Cancel();
        }

        private void Progress(object? sender, PlaybackProgressEventArgs args) => Check(args);
        private void Stopped(object? sender, PlaybackStopEventArgs args) => Check(args);

        private void Check(PlaybackProgressEventArgs args)
        {
            var settings = Plugin.Instance?.Configuration;
            if (settings == null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.PlaybackUserId)
                || string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrEmpty(settings.Password)
                || args.Item is not Episode episode || !(args.PlaybackPositionTicks is long position)
                || !(episode.RunTimeTicks is long runtime) || runtime <= 0 || position < runtime * 0.9
                || !(episode.ParentIndexNumber is int season) || season < 0
                || !(episode.IndexNumber is int number) || number < 1
                || (episode.IndexNumberEnd is int end && end != number)
                || !args.Users.Any(user => SameId(user.Id.ToString(), settings.PlaybackUserId)))
                return;

            var series = episode.Series;
            if (series == null || !series.ProviderIds.TryGetValue("Tvdb", out var tvdb)
                || !int.TryParse(tvdb, NumberStyles.None, CultureInfo.InvariantCulture, out var seriesId) || seriesId < 1)
                return;

            var key = settings.Username.ToUpperInvariant() + ":" + seriesId + ":" + season + ":" + number;
            lock (gate)
            {
                if (shutdown.IsCancellationRequested || DateTimeOffset.UtcNow < retryAfter
                    || sent.Contains(key) || pending.Count >= 32 || !pending.Add(key))
                    return;
            }
            _ = SendAsync(settings, key, seriesId, season, number);
        }

        private static bool SameId(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
                || (Guid.TryParse(left, out var l) && Guid.TryParse(right, out var r) && l == r);

        private async Task SendAsync(Settings settings, string key, int tvdb, int season, int episode)
        {
            var entered = false;
            try
            {
                await serial.WaitAsync(shutdown.Token).ConfigureAwait(false);
                entered = true;
                lock (gate)
                    if (DateTimeOffset.UtcNow < retryAfter) return;
                if (!ReferenceEquals(Plugin.Instance?.Configuration, settings)) return;
                if (!ReferenceEquals(connected, settings) || token == null)
                {
                    using var login = await PostAsync("login", new { username = settings.Username, password = settings.Password }).ConfigureAwait(false);
                    if (HasError(login.RootElement) || !login.RootElement.TryGetProperty("hash", out var hash)
                        || hash.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(hash.GetString()))
                        throw new InvalidOperationException("Next Episode login failed.");
                    token = hash.GetString();
                    connected = settings;
                }

                using var update = await PostAsync("update_data", new
                {
                    user = new { username = settings.Username, hash = token },
                    tvshows = new[] { new {
                        thetvdb_id = tvdb.ToString(CultureInfo.InvariantCulture),
                        season = season.ToString(CultureInfo.InvariantCulture),
                        episode = episode.ToString(CultureInfo.InvariantCulture),
                        watched = "1"
                    } }
                }).ConfigureAwait(false);
                var response = update.RootElement;
                if (HasError(response)
                    || (response.TryGetProperty("tv_shows", out var shows) && HasError(shows))
                    || (response.TryGetProperty("tvshows", out var otherShows) && HasError(otherShows)))
                    throw new InvalidOperationException("Next Episode rejected the update.");

                lock (gate)
                {
                    if (sent.Count >= 10000) sent.Clear();
                    sent.Add(key);
                }
            }
            catch (Exception) when (shutdown.IsCancellationRequested) { }
            catch (Exception)
            {
                token = null;
                lock (gate) retryAfter = DateTimeOffset.UtcNow.AddMinutes(1);
#if EMBY
                log.Warn("Next Episode sync failed. Check your Next Episode profile credentials and server connectivity.");
#else
                log.LogWarning("Next Episode sync failed. Check your Next Episode profile credentials and server connectivity.");
#endif
            }
            finally
            {
                lock (gate) pending.Remove(key);
                if (entered) serial.Release();
            }
        }

        private static bool HasError(JsonElement element)
            => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("error", out var error)
                && error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.False;

        private async Task<JsonDocument> PostAsync(string path, object payload)
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(path, content, shutdown.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }
}
