using System;
using System.Globalization;

namespace JustWatch.Plugin.Core
{
    public static class SeerrLinks
    {
        public static string? ForTitle(string? baseUrl, int? tmdbId, string? objectType)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || !tmdbId.HasValue || tmdbId.Value <= 0) return null;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http")
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
            var type = string.Equals(objectType, "MOVIE", StringComparison.OrdinalIgnoreCase) ? "movie"
                : string.Equals(objectType, "SHOW", StringComparison.OrdinalIgnoreCase) ? "tv" : null;
            if (type == null) return null;
            var path = uri.AbsolutePath.TrimEnd('/');
            // Accept a copied login URL while preserving reverse-proxy subpaths.
            if (path.EndsWith("/login", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 6);
            return uri.GetLeftPart(UriPartial.Authority) + path + "/" + type + "/" + tmdbId.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
