namespace JustWatch.Plugin.Core
{
    public sealed class JustWatchOptions
    {
        public string Country { get; set; } = "US";

        public string AnywhereCountries { get; set; } = "CA,US,GB,AU";

        public string Language { get; set; } = "en";

        public int ResultLimit { get; set; } = 8;

        public string SeerrUrl { get; set; } = string.Empty;

        public string SeerrApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://apis.justwatch.com";
    }
}
