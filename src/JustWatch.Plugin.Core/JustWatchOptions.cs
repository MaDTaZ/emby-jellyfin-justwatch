namespace JustWatch.Plugin.Core;

public sealed class JustWatchOptions
{
    public string Country { get; set; } = "US";

    public string Language { get; set; } = "en";

    public int ResultLimit { get; set; } = 8;

    public string BaseUrl { get; set; } = "https://apis.justwatch.com";
}
