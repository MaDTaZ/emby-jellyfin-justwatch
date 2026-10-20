# JustWatch Plugins for Jellyfin and Emby

This repository contains a shared JustWatch lookup library plus thin host plugins for Jellyfin and Emby.

## Downloads and installation

Preview builds: [GitHub Releases](https://github.com/MaDTaZ/emby-jellyfin-justwatch/releases/tag/v0.1.1).

### Jellyfin repository

For Jellyfin 10.10.7, go to Dashboard → Plugins → Repositories, add a repository named **MaDTaZ JustWatch**, and use:

```text
https://raw.githubusercontent.com/MaDTaZ/emby-jellyfin-justwatch/main/manifest.json
```

Refresh the plugin catalog, install **JustWatch**, then restart Jellyfin. This package targets the 10.10 server line; other server generations need their own builds. Repository and package downloads are verified separately from live server installation, which still needs testing.

### Emby stable and beta

Download **JustWatch-Emby-Stable-4.8.10.zip** or **JustWatch-Emby-Beta-4.11.0.8.zip** from the release. Stop Emby, extract both DLLs directly into its plugins directory, replace older JustWatch copies, then restart. Install only one Emby variant.

Emby catalog installation requires [Emby's developer onboarding and catalog publication](https://dev.emby.media/doc/plugins/dev/Getting-your-plug-in-in-the-catalog.html). This GitHub repository URL cannot be added to Emby as a custom catalog. The plugin has not been submitted to Emby's catalog.

## Projects

- `JustWatch.Plugin.Core`: shared HTTP client and result models.
- `Jellyfin.Plugin.JustWatch`: Jellyfin plugin with `GET /JustWatch/Search`.
- `Emby.Plugin.JustWatch`: Emby plugin with `GET /JustWatch/Search`.

## Build

Install the .NET 8 SDK. This version targets Jellyfin 10.10.7 (net8.0) and Emby 4.8.10 (netstandard2.0). Other server versions need matching host packages and potentially a different target framework. Then run:

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

## Admin page

Both host plugins expose a JustWatch admin page in the server dashboard. The page lets you:

- Set country, language, and result limit.
- Search JustWatch by title, optional year, and media type.
- View named providers grouped into subscription, rent, buy, free, and ad-supported offers.
- See prices and video quality when supplied by JustWatch.
- Open provider watch links or the matched title on JustWatch.

Save country and language settings before searching. Lookup uses the saved server configuration. This first version is a server dashboard tool; it does not inject buttons into movie pages or TV/mobile clients, compare missing seasons, or download media.

## Install a build

Build in Release mode. For Jellyfin, create a JustWatch folder inside the server plugin directory and copy the Jellyfin plugin DLL plus `JustWatch.Plugin.Core.dll` from its build output. For Emby, copy both DLLs directly into the plugins directory. Do not copy Jellyfin/Emby host assemblies. Restart the server, open the JustWatch dashboard page, save your region, and search. Keep separate installation folders for the two server builds. Runtime installation and dashboard loading still need verification on a real server.

## Verification

```bash
node --test tests/dashboard.test.cjs
dotnet run --project tests/JustWatch.Core.Smoke
```

The shared-client checks use a simulated HTTP response and exercise offer parsing, numeric provider IDs, year filtering, unsafe links, and upstream errors. Dashboard checks cover both JSON naming formats, categories, escaping, and empty/error states.

## Notes

JustWatch does not offer a stable public server-plugin API. The shared client uses JustWatch’s unofficial GraphQL web endpoint, so treat it as best-effort and expect that it may need updates if JustWatch changes its private API.

## Emby stable and beta packages

- `dist/JustWatch-Emby-Stable-4.8.10.zip`: existing stable build against SDK 4.8.10.
- `dist/JustWatch-Emby-Beta-4.11.0.8.zip`: built against MediaBrowser.Common and MediaBrowser.Model from the official Emby 4.11.0.8 release archive. Compiles with zero warnings; live server loading is not yet tested.

For Emby, stop the server and place both DLLs directly in its plugins directory, replacing older copies. Install only one Emby variant at a time. These packages contain only the plugin and shared lookup library, not server assemblies.

The default build keeps SDK 4.8.10. To build against a different published SDK, pass `-p:EmbySdkVersion=VERSION`. For an exact server build, extract `MediaBrowser.Common.dll` and `MediaBrowser.Model.dll` from that official release into a local directory, then pass `-p:EmbyReferenceDirectory=/absolute/path/to/references`. Exact-release builds also reference System.Memory 4.6.3 to match the 4.11 assemblies.

## Release packaging

All plugin assemblies use version 0.1.1.0. Build the stable Emby project into `artifacts/emby-stable`, Jellyfin into `artifacts/jellyfin`, and the exact-release Emby beta into `artifacts/emby-4.11.0.8` using `dotnet build -c Release -o DIRECTORY` with the corresponding project and properties described above. Then run:

```bash
python3 scripts/package-release.py
```

This creates three flat ZIP archives, SHA-256 checksums, and a Jellyfin manifest containing the ZIP's MD5 checksum as required by Jellyfin. Upload the archives to release `v0.1.1` and commit the matching manifest. Update the script's version, tag, and timestamp for later releases.

## 0.1.1 dashboard fix

Jellyfin now initializes its controls with an inline script in the configuration HTML, following Jellyfin's plugin template. The previous Emby-style external controller left the settings and Search controls unbound in Jellyfin. The save response is now forwarded to the dashboard's confirmation handler on both hosts.

Eight dashboard checks pass, including initialization from the actual Jellyfin HTML and settings submission. A local browser check with simulated server responses verifies default settings, Save, and Search. A live Jellyfin server test is still pending. After upgrading from 0.1.0, restart Jellyfin and hard-refresh the configuration page to clear the old page.
