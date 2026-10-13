# Vehicle Handler Clipboard Controls

Status: Implemented; awaiting in-game verification  
Target: Schedule I `0.4.7f12` IL2CPP  
Build: `0.4.7f12-dev8`

## Player flow

1. Equip the management clipboard.
2. Select exactly one marked Vehicle Handler.
3. Open the management interface.
4. Choose an owned vehicle, an owned property with a loading dock, and a loading bay.
5. Choose whether the assignment is enabled and apply it.

The Vehicle Handler panel replaces the misleading vanilla Packager configuration panel only for a marked Vehicle Handler. Ordinary Handlers and every other configurable continue through the base-game panel.

## Controls and safety

- Vehicle and destination choices are rebuilt from the current game-owned lists.
- A vehicle currently detected as a loading-dock occupant is labelled with that dock and property; vehicles without a recognised dock retain their short stable-ID suffix.
- Vehicles claimed by another Vehicle Handler are omitted from the chooser. The selected Handler's own claimed vehicle remains visible for review and reconfiguration.
- Every selection stores the stable vehicle GUID, property code, dock GUID, and dock index; display names are never used as identity.
- Properties without a loading dock and docks whose parent property does not match are excluded.
- Apply and remove call the single `IHandlerAssignmentRegistry` authority with compare-and-swap revisions.
- Reconfiguration and removal remain blocked by the registry while a trip is active.
- The registry still rejects a vehicle already assigned to another Handler if a stale panel attempts to claim it.
- Mutations are accepted only when the selected employee reports server initialization. A non-host client receives a read-only rejection instead of mutating local state.
- Firing or despawning a Handler removes its idle assignment so the vehicle can be reused.

The current milestone intentionally does not move or hide vehicles. The panel explains that load and hide/release actions stay locked until M4 supplies reservation and movement safety.

## Current boundary

`VH-M3-003` persists assignments per save. Reservations, live-object recovery, and physical movement remain deferred to M4.

## Verification

Run the dependency-free clipboard model verifier:

```powershell
.\scripts\verify-clipboard.ps1 -DotNet "<dotnet>"
```

The IL2CPP build gate also verifies the exact management-interface, IMGUI, owned-vehicle, owned-property, loading-dock, and server-authority APIs before compiling.

Runtime verification should confirm:

1. selecting Michelle alone opens the Vehicle Handler panel rather than Packager fields;
2. ordinary Packagers retain their original panel;
3. owned vehicles and docked owned properties can be cycled without duplicate or missing entries;
4. apply, reopen, disable, reconfigure, and remove report the expected revision and state;
5. two Vehicle Handlers cannot claim the same vehicle;
6. firing an assigned idle Handler releases the assignment;
7. no vehicle moves, hides, duplicates, or changes save identity during these tests.
