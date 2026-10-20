using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Model.Drawing;
using Emby.Plugin.JustWatch.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.JustWatch
{
    public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin? Instance { get; private set; }

        public override string Name => "Where to Watch";

        public override Guid Id => Guid.Parse("4df372d4-8179-4f05-9312-419434bd835d");

        public override string Description => "Find streaming providers and request movies and shows through Seerr.";

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public Stream GetThumbImage() => GetType().Assembly.GetManifestResourceStream("Emby.Plugin.JustWatch.Images.thumb.png")
            ?? throw new InvalidOperationException("The embedded Where to Watch thumbnail is missing.");

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "JustWatch",
                    DisplayName = "Where to Watch",
                    MenuIcon = "search",
                    MenuSection = "server",
                    EnableInMainMenu = true,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "JustWatchConfigJs",
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.js"
                }
            };
        }
    }
}
