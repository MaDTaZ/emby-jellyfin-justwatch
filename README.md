# JustWatch Plugins for Jellyfin and Emby

This repository contains a shared JustWatch lookup library plus thin host plugins for Jellyfin and Emby.

## Projects

- `JustWatch.Plugin.Core`: shared HTTP client and result models.
- `Jellyfin.Plugin.JustWatch`: Jellyfin plugin with `GET /JustWatch/Search`.
- `Emby.Plugin.JustWatch`: Emby plugin with `GET /JustWatch/Search`.

## Build

Install a .NET SDK that matches your server plugin target. Current Jellyfin plugin development expects a newer SDK than .NET 5. Then run:

```bash
dotnet restore JustWatch.Plugins.sln
dotnet build JustWatch.Plugins.sln -c Release
```

For Jellyfin, keep the `Jellyfin.Controller` and `Jellyfin.Model` package versions aligned with your Jellyfin Server version.

For Emby, keep the `MediaBrowser.*` package versions aligned with your Emby Server version.

## Endpoint

```text
GET /JustWatch/Search?title=Arrival&year=2016&mediaType=movie
```

`mediaType` can be `movie`, `series`, or omitted.

## Notes

JustWatch does not offer a stable public server-plugin API. The shared client uses the web API endpoint commonly used by JustWatch clients, so treat it as best-effort and expect that it may need updates if JustWatch changes its private API.
