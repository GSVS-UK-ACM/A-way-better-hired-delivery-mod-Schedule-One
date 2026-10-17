# Vehicle Handler Assignment Persistence

Status: Implemented and runtime verified  
Target: Schedule I `0.4.7f12` IL2CPP  
Build: `0.4.7f12-dev13`

## Storage

Each Schedule I save owns its own document:

```text
<save folder>/UserData/VehicleHandlers.json
```

The mod writes only its stable assignment state. It never serializes game objects, vehicle contents, transforms, or local machine paths. The version-1 document contains:

- schema version;
- Handler GUID;
- existing vehicle GUID;
- property code, dock GUID, and dock index;
- enabled and manually-hidden flags;
- trip GUID/state when a recoverable trip exists;
- compare-and-swap revision.

## Save behavior

The loaded-game integration and prefixes on both pinned `SaveManager.Save` routes idempotently attach persistence to the live manager's `onSaveComplete` event. The event snapshots the single assignment registry only after the base game finishes its asynchronous cleanup and write cycle. Writes use a temporary file followed by an atomic replacement. A changed document preserves the previous primary document as `VehicleHandlers.json.bak`; an unchanged snapshot is not rewritten.

## Load behavior

At `StartGame`, the mod creates a fresh empty registry, resolves the selected save's exact `SavePath`, and arms restoration on the live manager's `onLoadComplete` event. Only after the game finishes rebuilding the save does the callback read the document. Valid entries are restored only through `IHandlerAssignmentRegistry.TryRestore`, retaining duplicate Handler/vehicle rejection and revision invariants. A corrupt primary document may recover from its previous valid backup. Missing, corrupt, future-version, or conflicting entries fail safely and never create a replacement vehicle.

Starting another save or unloading the mod clears process-local assignment state so assignments cannot leak between save slots.

## Current boundary

M3 persists stable assignment and recoverable trip state. Live Handler/vehicle/property validation, interrupted-trip recovery, reservations, and physical movement remain owned by M4.

## Verification

Run the dependency-free schema and file verifier:

```powershell
.\scripts\verify-persistence.ps1 -DotNet "<dotnet>"
```

Runtime verification must confirm:

1. applying an assignment and saving creates the per-save document;
2. returning to the menu clears process-local state;
3. loading the same save restores the assignment, flags, destination, and revision;
4. loading a different save does not import the first save's assignments;
5. a missing document behaves like an empty assignment set;
6. no vehicle is spawned, moved, hidden, or given a different save identity.

All six checks passed on `0.4.7f12-dev13`. `SaveGame_1` retained its enabled revision-1 assignment document unchanged while `SaveGame_2` restored zero assignments and created an independent empty schema-v1 document on save.
