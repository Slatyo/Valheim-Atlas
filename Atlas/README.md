# SharedMap - Development

Server-side shared map exploration and pins for Valheim.

## Building

1. Ensure you have the Valheim game installed
2. Create `Environment.props` with your paths (see `Environment.props.example`)
3. Build with `dotnet build` or Visual Studio

### Debug Build
```bash
dotnet build -c Debug
```
Copies to your configured BepInEx plugins folder.

### Release Build
```bash
dotnet build -c Release
```
Creates a Thunderstore-ready package in `bin/Release/`.

## Project Structure

```
SharedMap/
├── Plugin.cs              # Main entry point
├── Config/
│   └── ConfigManager.cs   # Configuration handling
├── Managers/
│   ├── ExplorationManager.cs  # Fog-of-war sharing
│   └── PinManager.cs          # Pin sharing
├── Network/
│   └── NetworkManager.cs      # RPC communication
├── Storage/
│   └── StorageManager.cs      # File persistence
├── Patches/
│   ├── MinimapPatches.cs      # Minimap hooks
│   ├── ZNetPatches.cs         # Network hooks
│   └── PlayerPatches.cs       # Player hooks
├── Models/
│   ├── SharedPin.cs           # Pin data model
│   ├── ExplorationChunk.cs    # Exploration data model
│   └── MapSyncData.cs         # Full sync payload
└── Utils/
    ├── CompressionHelper.cs   # GZip compression
    └── CoordinateHelper.cs    # Coordinate conversion
```

## Architecture

### Data Flow

1. **Local Action** → Harmony Patch intercepts
2. **Manager** processes and validates
3. **NetworkManager** sends RPC to server
4. **Server** stores and broadcasts to all clients
5. **Clients** apply to their local map

### Network Protocol

- `SharedMap_RequestFullSync` - Client requests full map on join
- `SharedMap_FullSyncResponse` - Server sends complete map state
- `SharedMap_ExplorationUpdate` - Incremental exploration update
- `SharedMap_BroadcastExploration` - Server broadcasts to clients
- `SharedMap_PinAdded/Removed/Updated` - Pin change events
- `SharedMap_BroadcastPin` - Server broadcasts pin changes

## Testing

1. Start a local multiplayer game
2. Have second player join
3. Explore on one client, verify it appears on other
4. Add/remove pins, verify sync
5. Restart server, verify persistence
