namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchOffer
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string MonetizationType { get; set; } = string.Empty;
        public string? Quality { get; set; }
        public string? Price { get; set; }
        public string? Currency { get; set; }
        public string? Url { get; set; }
    }
}
