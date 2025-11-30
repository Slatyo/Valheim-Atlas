# Atlas

A Valheim mod that enables real-time map sharing between all players on a server.

## Features

- **Shared Fog of War** - Exploration is automatically shared between all players
- **Shared Map Pins** - Pins placed by any player are visible to everyone
- **Configurable** - Extensive configuration options for server admins
- **Persistent** - Map data is saved and persists across server restarts
- **Efficient** - Uses chunked data with compression for minimal network overhead

## Configuration

### BepInEx Config (`BepInEx/config/com.atlas.valheim.cfg`)

| Setting | Default | Description |
|---------|---------|-------------|
| EnableExplorationSharing | true | Toggle fog of war sharing |
| EnablePinSharing | true | Toggle pin sharing |
| ForcePublicPosition | false | Force all players visible on map |

### JSON Config (`BepInEx/config/Atlas.json`)

Server-enforced settings:

| Setting | Default | Description |
|---------|---------|-------------|
| ExplorationRadiusMultiplier | 100 | Exploration radius percentage |

## Installation

### Manual
1. Install BepInEx 5.4.x
2. Install Jotunn 2.26.1+
3. Copy `Atlas.dll` to `BepInEx/plugins/`

### Thunderstore (r2modman)
Install via r2modman or Thunderstore Mod Manager

**Note:** All players and the server must have the mod installed.

## Changelog

### 1.0.0
- Initial release
- Shared fog of war exploration
- Shared map pins
- Server-enforced configuration
- Persistent storage

## Requirements
- BepInEx 5.4.2333+
- Jotunn 2.26.1+
