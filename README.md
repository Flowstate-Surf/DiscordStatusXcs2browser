# DiscordStatus × cs2browser

A [SwiftlyS2](https://github.com/swiftly-solution/SwiftlyS2) plugin that posts and live-updates a CS2 server's status in a Discord channel via a webhook, with the live cs2browser.net player-history banner.

The plugin posts a single embed message once, then edits that same message in place whenever the server's state changes (map load, hostname change, player join/disconnect). The embed contains:

- Server name (from `sv_hostname` or override) and a clickable link to the cs2browser.net server page
- Online/offline status, current map, player count and slot count
- Server IP, a clickable "Connect" link, and a copy-pasteable `connect <ip>` command
- A banner image from cs2browser.net that includes the live player histogram

## Requirements

- [SwiftlyS2](https://github.com/swiftly-solution/SwiftlyS2) `1.4.0-beta.7` or newer
- A Discord webhook URL
- Your server listed on [cs2browser.net](https://cs2browser.net/) (see below)

## Get your cs2browser.net server id

The plugin uses cs2browser.net for the connect link and the banner image, so your server has to be listed there first.

1. Go to <https://cs2browser.net/> and sign in.
2. Add your server (the site will scan its public `ip:port`).
3. Open your server's page. The URL will look like:
   ```
   https://cs2browser.net/server/0cb82697-8ac2-4825-bea7-b6a6d625b071
   ```
   The trailing value (`0cb82697-...` or a numeric id) is your `CS2BrowserServerId`. Either form is accepted.

## Installation

1. Download `DiscordStatus.zip` from the [Releases](https://github.com/Flowstate-Surf/DiscordStatusXcs2browser/releases) page.
2. Extract it into your server's `addons/swiftly/plugins/` directory so you end up with `addons/swiftly/plugins/DiscordStatus/DiscordStatus.dll`.
3. Start the server once so the plugin generates its config file at `addons/swiftly/configs/plugins/DiscordStatus/DiscordStatus.json`.

## Configuration

Edit `addons/swiftly/configs/plugins/DiscordStatus/DiscordStatus.json`:

```jsonc
{
  "ConfigVersion": 2,
  "ServerIp": "64.40.8.21:27015",
  "ServerName": "",
  "CS2BrowserServerId": "0cb82697-8ac2-4825-bea7-b6a6d625b071",
  "CS2BrowserBannerSize": "Big",
  "StatusInfo": {
    "MessageId": "",
    "WebhookUri": "https://discord.com/api/webhooks/.../..."
  }
}
```

| Field | Description |
| --- | --- |
| `ServerIp` | `ip:port` of your server. Used in the embed and the `connect` command. |
| `ServerName` | Override for the embed title. Leave empty to use `sv_hostname`. |
| `CS2BrowserServerId` | The id from your cs2browser.net server URL (UUID or numeric). Required for the banner. |
| `CS2BrowserBannerSize` | `Big` (550×95, includes player histogram) or `Small` (350×20). |
| `StatusInfo.WebhookUri` | Discord webhook URL. The channel must allow webhooks. |
| `StatusInfo.MessageId` | Leave empty on first run — the plugin posts a new message and saves its id here. On later reloads it edits that message in place. |

Reload with `sw plugins reload DiscordStatus` (or restart the server). The first run creates the message; subsequent updates edit it.

## Building from source

```powershell
cd src
dotnet build -c Release
```

Copy the published plugin folder under `src/build/` to your server's `addons/swiftly/plugins/` directory.

## Notes

- The cs2browser banner URL includes a per-minute cache-buster (`?t=...`), so Discord's image proxy refetches it at most once per minute even when the embed is edited more often.
- The plugin only edits its own message. If you delete the Discord message manually, clear `MessageId` in the config and reload — a fresh message will be posted.
- The connect link points at `https://cs2browser.net/server/{CS2BrowserServerId}`. There is no separate Steam-protocol option.