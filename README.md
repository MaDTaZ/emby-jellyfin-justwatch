# Where to Watch for Jellyfin and Emby

This repository contains a shared JustWatch lookup library plus thin host plugins for Jellyfin and Emby.

## Downloads and installation

Preview builds: [GitHub Releases](https://github.com/MaDTaZ/emby-jellyfin-justwatch/releases).

### Jellyfin repository

For Jellyfin 10.10.7 or 12.0-rc7, go to Dashboard → Plugins → Repositories, add a repository named **MaDTaZ JustWatch**, and use:

```text
https://raw.githubusercontent.com/MaDTaZ/emby-jellyfin-justwatch/main/manifest.json
```

Refresh the plugin catalog, install **Where to Watch**, then restart Jellyfin. The feed includes separate builds: 0.1.6.1 for Jellyfin 10.10.7 and 0.1.7.0 for Jellyfin 12.0-rc7. The 12.0 build targets .NET 10 and the exact 12.0.0-rc7 SDK. Other server generations need matching builds. Repository and package downloads are verified separately from live server installation, which still needs testing.

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

Build in Release mode. For Jellyfin, create a JustWatch folder inside the server plugin directory and copy the Jellyfin plugin DLL plus `JustWatch.Plugin.Core.dll` from its build output. For Emby, copy both DLLs directly into the plugins directory. Do not copy Jellyfin/Emby host assemblies. Restart the server, open the Where to Watch dashboard page, save your region, and search. Keep separate installation folders for the two server builds. Runtime installation and dashboard loading still need verification on a real server.

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

Current Emby and Jellyfin 12 assemblies use version 0.1.7.0; Jellyfin 10.10.7 uses 0.1.6.1 so newer servers select the .NET 10 build. Build the stable Emby project into `artifacts/emby-stable`, Jellyfin into `artifacts/jellyfin`, and the exact-release Emby beta into `artifacts/emby-4.11.0.8` using `dotnet build -c Release -o DIRECTORY` with the corresponding project and properties described above. Then run:

```bash
python3 scripts/package-release.py
```

This creates four flat ZIP archives, SHA-256 checksums, and a Jellyfin manifest containing the ZIP's MD5 checksum as required by Jellyfin. Upload the archives to release `v0.1.7` and commit the matching manifest. Update the script's version, tag, and timestamp for later releases.

## 0.1.1 dashboard fix

Jellyfin now initializes its controls with an inline script in the configuration HTML, following Jellyfin's plugin template. The previous Emby-style external controller left the settings and Search controls unbound in Jellyfin. The save response is now forwarded to the dashboard's confirmation handler on both hosts.

Eight dashboard checks pass, including initialization from the actual Jellyfin HTML and settings submission. A local browser check with simulated server responses verifies default settings, Save, and Search. A live Jellyfin server test is still pending. After upgrading from 0.1.0, restart Jellyfin and hard-refresh the configuration page to clear the old page.

## Jellyfin 12.0-rc7 build

This server release uses .NET 10. The original 10.10.7 plugin is not the build to use on 12.0-rc7. With the .NET 10 SDK installed, build and package the matching version:

```bash
dotnet build src/Jellyfin.Plugin.JustWatch/Jellyfin.Plugin.JustWatch.csproj -c Release -p:JellyfinSdkVersion=12.0.0-rc7 -p:JellyfinTargetFramework=net10.0 -p:Version=0.1.2 -p:AssemblyVersion=0.1.2.0 -p:FileVersion=0.1.2.0 -o artifacts/jellyfin-12.0-rc7
python3 scripts/package-jellyfin12.py
```

The manifest URL stays unchanged. The 12.0 entry has a higher plugin version and `targetAbi` 12.0.0.0, so 10.10 servers filter it out while 12.0 servers select it. Do not run the older `package-release.py` to publish a 12.0 update: that older script packages the 0.1.1 release. It is retained for reproducing that release.

### Jellyfin 12.0-rc7 integration verification

The 0.1.2.0 build was loaded into the official portable Jellyfin 12.0-rc7 server with isolated temporary data and a loopback-only port. The server completed startup and logged JustWatch 0.1.2.0 as loaded. Authenticated API checks saved Canadian configuration, reloaded it successfully, and performed a live Arrival (2016) search returning a title. The test server also fetched the original repository URL through its own package catalog endpoint. These checks exercise the running server in addition to the eight dashboard tests; they do not imply every server platform or web client has been tested.

## 0.1.3 provider cards

Results now use responsive provider cards, clear price rows, theme-aware links, and a separate Disc & Blu-ray section. SD, HD, and 4K are grouped only when the provider, category, price, and destination match, so different prices and watch links remain available. Ten dashboard checks pass, including grouping without losing price or destination variants. The redesigned configuration page, saved settings, and live lookup were verified on the isolated Jellyfin 12.0-rc7 server.

For the current four-package release, use the default version for Emby and Jellyfin 12, and override `Version`, `AssemblyVersion`, and `FileVersion` to 0.1.2.1 when building Jellyfin 10.10.7. Run `python3 scripts/package-release.py`. The separate `package-jellyfin12.py` script is retained for reproducing the historical 0.1.2 release.

## Seerr integration (0.1.4)

In JustWatch settings, enter your Seerr base URL in the optional **Seerr URL** field and Save. A copied `/login` URL also works; reverse-proxy subpaths are preserved. Search again after saving. Results with a TMDB ID show **Request in Seerr**, which opens that exact movie or TV page in a new tab. Sign in to Seerr and submit the request there using your own account and its existing permissions. No API key is required, no requests are submitted automatically, and no credentials are stored in the plugin. Leave the URL blank to disable the buttons. Titles without a TMDB ID do not get a guessed link.

The release has 12 passing dashboard checks and shared-client checks for exact IDs, TV links, proxy paths, disabled integration, and unsafe URLs. On an isolated running Jellyfin 12.0-rc7 server, settings persisted and live lookups generated the expected Arrival movie and Breaking Bad TV links. Actual request submission remains in Seerr.

## Direct Seerr requests (0.1.5)

In JustWatch settings, save your Seerr base URL and the API key from Seerr's settings. The password-style key field is stored in the server's administrator-only plugin configuration. Search again: movies show **Request**, while TV shows show **Choose seasons**, followed by **Request selected seasons**. No Seerr web login is needed. **Open in Seerr** remains available, and leaving the API key blank keeps the previous link-only behavior.

Direct requests use the API key account, Seerr's default server/profile, and that account's approval rules. They may therefore be automatically approved. Direct request and season-list endpoints require server administrator privileges. The key is sent only to the configured Seerr API in an HTTP header, never in title links or search results. Redirects are disabled for key-bearing connections; save Seerr's final URL. Requests for fully available titles are stopped before submission, and duplicate/conflict responses are displayed without automatically retrying requests.

Validation: 16 dashboard checks, shared-client request contract checks, and four builds pass. An isolated running Jellyfin 12.0-rc7 server was tested against a loopback simulated Seerr service: movie and selected-season requests succeeded, duplicates were handled, saved keys stayed out of search results, anonymous requests returned 401, and non-admin requests returned 403. No requests were submitted to the user's actual Seerr instance. The Emby services compile against stable and exact beta SDK references; live Emby API execution still needs testing.

## Seerr status badges and posters (0.1.6)

When a Seerr API key is configured, results check Seerr in the background (at most three concurrent lookups) and display Available, Pending approval, Requested, Partially available, Blocked in Seerr, or Not requested. Movies that are already available, pending, requested, or blocked cannot be submitted again. TV shows can still request new seasons: season-level availability and active standard-resolution requests disable only the affected checkboxes. Submit rechecks the status server-side to handle changes after the page loaded. Failed status lookups say Status unavailable; they never claim a title is available, and a later request still performs the server preflight.

Posters come from JustWatch, load lazily, and have a fallback when missing or broken. Status lookups never submit requests automatically. The existing Seerr URL and API key settings are retained.

Validation: 22 dashboard tests and expanded shared-client checks cover status labels, disabled movie requests, partially available TV seasons, unknown status, and poster URLs/fallbacks. All four builds pass. An isolated running Jellyfin 12.0-rc7 server with simulated Seerr responses verified each non-requestable movie state and new-season availability; a live JustWatch search verified a poster URL. The browser preview uses a real poster and simulated Seerr availability. No requests were submitted to the user's real Seerr server.

## Where to Watch rename (0.1.7)

The displayed plugin name and dashboard label are now **Where to Watch**. The catalog uses the approved cinema mascot. Plugin GUIDs, assembly names, internal configuration page identifiers, routes, and repository URL stay unchanged so existing installs update in place and keep their configuration, including Seerr settings. Older filenames and internal identifiers still use JustWatch for compatibility. Jellyfin 12 uses 0.1.7.0; Jellyfin 10.10.7 uses 0.1.6.1.

## Search-first layout (0.1.8)

Search now appears first, with title, optional year, type, and Search arranged in a compact responsive row on desktop and stacked on smaller screens. Region, language, and Seerr settings are inside a keyboard-accessible Settings disclosure below results, collapsed by default. Existing settings are retained. Jellyfin 12 uses 0.1.8.0; Jellyfin 10.10.7 uses 0.1.7.1.

## Provider filters (0.1.9)

Search results show selectable provider chips. Select one or more services to narrow offers; All providers clears the filter. Choices are remembered in this browser when local storage is available. Saved selections remain selectable even when absent from the next search. Titles and Seerr actions remain visible when no offers match. Filtering updates offer visibility and provider counts without reloading results or resetting season/request controls. Jellyfin 12 uses 0.1.9.0; Jellyfin 10.10.7 uses 0.1.8.1.

## Anywhere across chosen countries (0.1.10)

Search region now offers My country and Anywhere (chosen countries). Configure Anywhere countries in Settings as one to six comma-separated two-letter codes; the default is CA,US,GB,AU. Anywhere searches only this list, at most two countries concurrently. Each country retains separate title results, regional links and prices; currency codes are shown beside prices. Result limit applies per country. A failed country fails the search rather than silently displaying incomplete coverage. My country remains the default, and existing settings are retained. Jellyfin 12 uses 0.1.10.0; Jellyfin 10.10.7 uses 0.1.9.1.

## Clear searches on navigation (0.1.11)

Search title, year, type, results, and result-derived provider chips clear on leaving and reopening the page. In-flight search responses are ignored after navigation so they cannot restore the previous results. Title autocomplete is disabled. Region/Seerr settings and saved provider preferences remain intact. Jellyfin 12 uses 0.1.11.0; Jellyfin 10.10.7 uses 0.1.10.1.

A Clear button beside Search resets fields and results immediately and returns focus to the title. It also invalidates pending responses so an earlier search cannot repopulate the page after clearing.

The catalog image uses a landscape mascot banner with the full character inside the card framing. The original square artwork remains in assets/where-to-watch-mascot-v1.png.

## Per-search region dropdown (0.1.12)

Search region defaults to All chosen countries and lists each configured Anywhere country separately with a readable name where available. Pick a single country without opening Settings or changing the saved default country. All chosen countries searches only the configured list. Jellyfin 12 uses 0.1.12.0; Jellyfin 10.10.7 uses 0.1.11.1.
