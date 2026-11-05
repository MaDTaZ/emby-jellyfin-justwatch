# Next Episode Sync

Install one package that matches your server. Stop the server, extract the plugin DLL into its server plugins directory, and restart. In the server dashboard, open **Plugins → Next Episode Sync**, choose the playback user, and enter your Next Episode profile username and password. Enable sync and save. The password is stored in the server's plugin configuration; keep that file and its backups private.

Google- or Apple-linked accounts can use a Next Episode password if they have one. The direct Kodi login does not offer Google or Apple sign-in buttons. Use the profile username rather than the email address.

The plugin listens to server playback progress and stop events. It sends TV episodes at or above 90%, matched by series TVDB ID and season/episode number. It skips files spanning multiple episodes and does not import old watches. Server clients must report playback position. Updates go from the server to Next Episode only. Each server deduplicates successful sends for the current process. No separate program, browser session, media-server API key, or Kodi installation is needed.

The packages were compiled against the versions in their names. Playback and dashboard setup still need a live test on the target server. The Next Episode connection uses its undocumented Kodi interface.
