# Architecture

## Runtime ownership

The server is authoritative for assignments, work state, bay reservations, and physical vehicle transitions. Clients render replicated employee, vehicle, and dock state and may request configuration changes through validated server paths.

The versioned cross-milestone types and mutation boundaries are frozen in [Internal Contracts](INTERNAL_CONTRACTS.md). M2, M3, and M4 depend on those contracts rather than each other's concrete implementations.

## Components

### Handler employee

`HandlerEmployee` is an injected IL2CPP role component attached beside a genuine base-game `Employee`; it does not subclass or replace the game's FishNet employee component. This composition is required because the pinned interop runtime cannot inject through one inherited `Employee` virtual signature. The component applies the existing `EEmployeeType.Handler` role, while a Handler-filtered Harmony postfix adds a fail-safe high-visibility outer-layer color without mutating shared appearance assets. An existing employee prefab may be used as a construction donor, but role-specific behavior is replaced and the resulting worker is identified and displayed as Handler. See [Handler Runtime Worker](HANDLER_RUNTIME.md).

### Assignment registry

`HandlerAssignmentRegistry` owns versioned assignments keyed by Handler GUID. Each assignment records vehicle GUID, destination property code/GUID, dock index, enabled state, manual hidden state, and recoverable trip state.

### Reservation registry

`HandlerReservationRegistry` owns destination reservations keyed by stable property and dock identity. Reservation acquisition and release are atomic and owner-specific. A Harmony postfix on `DeliveryManager.IsLoadingBayFree(Property, int)` combines vanilla availability with Handler reservations.

### Movement coordinator

`HandlerMovementCoordinator` validates an assignment, detaches the exact vehicle from its current dock or parking spot, advances a timed trip, waits for destination availability, aligns the vehicle to the dock, and restores normal visibility/physics.

### Management UI

The Handler configuration panel follows the delivery application's location/bay selection pattern. Vehicle-side management is added only if a safe `IConfigurable` adapter can be attached without replacing base vehicle interaction; otherwise all controls remain on the Handler panel.

## Persistence

Base employee and vehicle data remains owned by the game. Mod-only assignment and in-progress state is stored in `UserData/VehicleHandlers.json`. Loading resolves GUIDs against existing runtime objects and never spawns a missing vehicle.

## Failure handling

- Missing Handler: discard its orphan assignment after load stabilization.
- Missing vehicle: disable assignment and release reservation.
- Missing property/bay: disable assignment and release reservation.
- Interrupted trip: restore the known physical vehicle to a safe visible state or preserve its valid base-game saved position.
- Occupied target: remain waiting; do not displace the occupant.

