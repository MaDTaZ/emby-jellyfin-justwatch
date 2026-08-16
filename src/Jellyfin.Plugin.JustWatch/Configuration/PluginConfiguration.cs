using JustWatch.Plugin.Core;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JustWatch.Configuration
{
    public sealed class PluginConfiguration : BasePluginConfiguration
    {
        public JustWatchOptions Options { get; set; } = new JustWatchOptions();
    }
}
