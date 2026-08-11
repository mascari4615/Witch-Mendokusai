# WM.Server

`WM.Server` is the deployable world host for WitchMendokusai.

The core rule is simple: this server does not reimplement game rules. It references the same `DomainSDK` that Unity uses, so server, Unity, and web stay on one ruleset.

## What it serves

- `GET /` - built-in web client from `wwwroot/`
- `GET /health` - public liveness/status probe
- `GET /ws` - WebSocket world connection

The browser client uses same-origin WebSocket:

- `https://host/...` -> `wss://host/ws`
- `http://host/...` -> `ws://host/ws`

That means the simplest public deployment shape is one process behind one hostname.

## Run locally

```powershell
dotnet run --project Server\WM.Server\WM.Server.csproj --urls http://127.0.0.1:5199
```

Then open:

```text
http://127.0.0.1:5199/
```

## Production shape

Recommended:

1. Publish `WM.Server`
2. Run it as a long-lived Windows service on the laptop build/ops machine
3. Put one public hostname in front of it through the existing tunnel

There is a ready-made service install script for the laptop shape:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Server\WM.Server\scripts\install-service.ps1
```

Minimal example:

```powershell
dotnet publish Server\WM.Server\WM.Server.csproj -c Release -o C:\wm-world\app
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5199'
$env:WM_WORLD_FILE = 'C:\wm-world\data\world.json'
C:\wm-world\app\WM.Server.exe
```

Recommended laptop flow:

1. Run `scripts/install-service.ps1` once on the laptop
2. Bind one public hostname to `http://127.0.0.1:5199`
3. Point the web entry and Unity default server URL at that hostname

Current default public hostname:

```text
https://wm.mascari4615.com/
```

Example with explicit paths:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Server\WM.Server\scripts\install-service.ps1 `
  -ServiceName wm-world `
  -PublishDir C:\wm-world\app `
  -DataDir C:\wm-world\data `
  -ListenUrl http://127.0.0.1:5199
```

## Environment

`ASPNETCORE_URLS`
: Bind address. Example: `http://127.0.0.1:5199`

`WM_WORLD_FILE`
: Persistent world save file. Put this outside build output.

`WM_ITEMS_FILE`
: Item catalog json override.

`WM_BUILDINGS_FILE`
: Building catalog json override.

`WM_CRAFTS_FILE`
: Craft catalog json override.

`WM_GATHERABLES_FILE`
: Gatherable seed json override.

`WM_INGREDIENTS_FILE`
: Ingredient seed json override.

`WM_RECIPES_FILE`
: Recipe book json override.

`WM_KARMOLAB_API`
: Base URL for KarmoLab account verification. Default is `https://yawnbot.mascari4615.com`.

`WM_KARMOLAB_VERIFY`
: Override path for the KarmoLab code verification endpoint. Default is `/kl/link/verify`.

## Core deployment rules

- Keep `world.json` outside `bin/` and outside the publish folder.
- Serve the web client and `/ws` from the same origin.
- Treat KarmoLab account lookup as optional. If it fails, the world still opens as guest.
- Do not make Unity builds depend on localhost-only server addresses.
- For Unity, the effective server URL order is: `WM_WORLD_SERVER` env -> saved per-device URL -> serialized build fallback.
- The current serialized Unity fallback should point at `wss://wm.mascari4615.com/ws`.

## Core multiplayer rules

- `WorldHost` is authoritative for world state. Unity and Web send requests; neither client decides the final result.
- A WebSocket frame is not necessarily a complete message. The server reads through `EndOfMessage` before parsing JSON.
- Client prediction is presentation only. The next authoritative `world` snapshot remains the correction source.
- `Protocol.cs` is the source of truth for the wire contract; `wwwroot/protocol.d.ts` is generated from it and must stay in sync.
- Identity secrets belong to a player identity, not a temporary connection. A reconnecting device should reuse its secret and recover the same persistent state.
- Persistent world data must stay outside the publish directory and be saved atomically with a backup.

## Current gap

This server is deployable as a service now, but "public release" still needs the last outer links:

- public hostname/tunnel route
- a stable player default URL for Unity builds
- a public KarmoLab entry point that links people into the world
