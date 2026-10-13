# Architecture

## Runtime ownership

The server is authoritative for assignments, work state, bay reservations, and physical vehicle transitions. Clients render replicated employee, vehicle, and dock state and may request configuration changes through validated server paths.

The versioned cross-milestone types and mutation boundaries are frozen in [Internal Contracts](INTERNAL_CONTRACTS.md). M2, M3, and M4 depend on those contracts rather than each other's concrete implementations.

## Components

### Handler employee

`HandlerEmployee` is an injected IL2CPP role component attached beside a genuine base-game `Employee`; it does not subclass or replace the game's FishNet employee component. This composition is required because the pinned interop runtime cannot inject through one inherited `Employee` virtual signature. The fixer receives a distinct **Vehicle Handler** choice. An unused wire discriminator selects that route, then the server maps construction to the existing `EEmployeeType.Handler`/Packager prefab and writes a save-safe `vehiclehandler_` employee-ID marker. Only marked employees receive the component and high-visibility worn-object color, so vanilla Handlers remain unchanged. The marker crosses the existing FishNet initialization call and survives base employee save/load data. See [Handler Runtime Worker](HANDLER_RUNTIME.md) and [Hiring Integration](HIRING_INTEGRATION.md).

Marked Vehicle Handlers are excluded from the vanilla Packager work entry points while retaining the base employee lifecycle. Their game-owned GUID is bound as the stable `HandlerId`, and firing, leave/despawn, and mod shutdown are forwarded through `IHandlerLifecycleSink` for later movement and reservation cleanup. Generic scene destruction is not treated as deletion because menu transitions destroy loaded objects without deleting saved employees. See [Handler Lifecycle](HANDLER_LIFECYCLE.md).

### Assignment registry

`HandlerAssignmentRegistry` owns versioned assignments keyed by Handler GUID. Each assignment records vehicle GUID, destination property code/GUID, dock index, enabled state, manual hidden state, and recoverable trip state.

The registry maintains a reverse vehicle index, compare-and-swap revisions, removal tombstones, and a restore-only loading phase. It is deliberately independent of live game-object validation and runtime movement. See [Handler Assignment Model](ASSIGNMENT_MODEL.md).

### Reservation registry

`HandlerReservationRegistry` owns destination reservations keyed by stable property and dock identity. Reservation acquisition and release are atomic and owner-specific. A Harmony postfix on `DeliveryManager.IsLoadingBayFree(Property, int)` combines vanilla availability with Handler reservations.

### Movement coordinator

`HandlerMovementCoordinator` validates an assignment, detaches the exact vehicle from its current dock or parking spot, advances a timed trip, waits for destination availability, aligns the vehicle to the dock, and restores normal visibility/physics.

### Management UI

The management-interface patch recognizes exactly one marked, Packager-backed Vehicle Handler and hides only that employee's misleading vanilla Packager panel. A dedicated clipboard overlay enumerates player-owned vehicles and owned properties with loading docks, carries stable GUID/code/index identities into a pure draft model, and applies compare-and-swap mutations through `IHandlerAssignmentRegistry`. Ordinary Handlers and other configurables keep the base UI. Writes are host/server-only; movement actions remain locked until M4. See [Vehicle Handler Clipboard Controls](CLIPBOARD_CONTROLS.md).

## Persistence

Base employee and vehicle data remains owned by the game. Each save stores mod-only assignment and in-progress state in `<save folder>/UserData/VehicleHandlers.json`. Loading rebuilds a fresh assignment registry from stable IDs and never spawns a missing vehicle. See [Assignment Persistence](ASSIGNMENT_PERSISTENCE.md).

## Failure handling

- Missing Handler: discard its orphan assignment after load stabilization.
- Missing vehicle: disable assignment and release reservation.
- Missing property/bay: disable assignment and release reservation.
- Interrupted trip: restore the known physical vehicle to a safe visible state or preserve its valid base-game saved position.
- Occupied target: remain waiting; do not displace the occupant.

