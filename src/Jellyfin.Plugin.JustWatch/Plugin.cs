using System;
using System.Collections.Generic;
using Jellyfin.Plugin.JustWatch.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JustWatch
{
    public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin? Instance { get; private set; }

        public override string Name => "JustWatch";

        public override Guid Id => Guid.Parse("fd91b02d-7551-46f1-a205-2ef7607c25d8");

        public override string Description => "Adds JustWatch availability lookup endpoints for Jellyfin.";

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "JustWatch",
                    DisplayName = "JustWatch",
                    MenuIcon = "search",
                    MenuSection = "server",
                    EnableInMainMenu = true,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
                }
            };
        }
    }
}
