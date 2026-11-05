using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

#if EMBY
namespace Emby.Plugin.NextEpisode
#else
namespace Jellyfin.Plugin.NextEpisode
#endif
{
    public sealed class Settings : BasePluginConfiguration
    {
        public bool Enabled { get; set; }
        public string PlaybackUserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public sealed class Plugin : BasePlugin<Settings>, IHasWebPages
    {
        public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer)
            => Instance = this;

        internal static Plugin? Instance { get; private set; }
        public override string Name => "Next Episode Sync";
        public override string Description => "Send watched TV episodes to Next Episode at 90% playback.";
#if EMBY
        public override Guid Id => new Guid("e1b3f411-61b9-4fd0-947b-36936228005d");
#else
        public override Guid Id => new Guid("f48e5e15-081c-4081-b680-1513264c584b");
#endif
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "NextEpisodeSync",
                DisplayName = Name,
                EmbeddedResourcePath = GetType().Namespace + ".config.html"
            };
#if EMBY
            yield return new PluginPageInfo
            {
                Name = "NextEpisodeSyncConfig",
                EmbeddedResourcePath = GetType().Namespace + ".config.js"
            };
#endif
        }
    }
}
