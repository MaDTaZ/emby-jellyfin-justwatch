using System;
using Emby.Plugin.JustWatch.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.JustWatch;

public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "JustWatch";

    public override Guid Id => Guid.Parse("4df372d4-8179-4f05-9312-419434bd835d");

    public override string Description => "Adds JustWatch availability lookup endpoints for Emby.";
}
