using System;

namespace JustWatch.Plugin.Core
{
    public static class LegacyPluginPolicy
    {
        public static bool CanArchive(Guid id, Guid activeId, Version version, Version activeVersion, string name, string status, bool hasInstance)
            => id == activeId && version < activeVersion && !hasInstance
                && (status == "Superseded" || status == "Disabled")
                && (name == "JustWatch" || name == "Where to Watch");
    }
}
