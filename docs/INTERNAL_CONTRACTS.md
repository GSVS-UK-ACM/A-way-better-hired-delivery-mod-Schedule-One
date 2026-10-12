# Internal Contracts

Status: Frozen for parallel M2-M4 implementation

Contract version: `1`

Target: Schedule I 0.4.7f12 IL2CPP

These contracts are internal to the mod assembly. Changes to names, enum values, identity rules, transition rules, or service semantics require a coordinated contract-version change and a handover update. New enum values are appended; existing numeric values are not reordered or reused.

## Authority

Assignment writes, validation for actions, reservation mutations, trip transitions, and physical vehicle movement run on the server. A client may request an action, but it cannot directly mutate a registry or advance a trip. Implementations reject non-server execution with the stable `NotServer` result.

## Identities

| Type | Stable source | Rule |
| --- | --- | --- |
| `HandlerId` | Handler/NPC GUID | Non-empty GUID; never an employee index. |
| `VehicleId` | `LandVehicle.GUID` | Non-empty GUID of the existing physical vehicle. |
| `PropertyKey` | `Property.PropertyCode` | Trimmed and compared ordinally without case. |
| `DockId` | `LoadingDock.GUID` | Non-empty GUID. |
| `LoadingBayKey` | Property code + dock GUID + dock index | All three values must resolve to the same runtime dock. |
| `TripId` | Server-created GUID | New for every accepted trip attempt. |

GUIDs cross the IL2CPP boundary through canonical `D` strings and are parsed into the mod-owned `System.Guid` wrappers. Persistence and UI code never substitute an employee index, list position, property display name, or dock index for a stable identity. The redundant dock index is retained because the game availability API uses an index; a GUID/index mismatch is a validation failure rather than permission to target whichever dock currently occupies that index.

## Assignment snapshot

`HandlerAssignmentConfiguration` is the immutable configuration input. `HandlerAssignmentSnapshot` is the immutable current value exchanged between M2, M3, and M4; it adds current trip identity/state and a non-negative revision.

- Idle assignments have no `TripId`; every non-idle state has one.
- One Handler has at most one snapshot.
- One vehicle may appear in at most one Handler snapshot. The registry returns `DuplicateVehicle` instead of silently reassigning it.
- Configuration, transitions, and removals use compare-and-swap revision semantics. `expectedRevision` must match the current snapshot; each successful mutation increments the stored revision.
- Configuration changes are rejected while a trip is active.
- `TryTransition` is the only normal path for changing trip state. It enforces `HandlerTripTransitionPolicy`; the first `Idle -> Reserved` transition binds the new `TripId`, subsequent transitions require that exact trip, and a transition to `Idle` clears it.
- `TryRestore` is used only while rebuilding an empty server registry from saved data. Conflicting Handler/vehicle entries return `RestoreConflict`; restored active states enter validation/recovery before actions are accepted.
- `ManuallyHidden` is configuration state, not a trip state. An idle manually hidden vehicle has no bay reservation.
- Loading never creates a vehicle when a saved `VehicleId` cannot be resolved. The assignment is disabled and any reservation is released.

## Validation

`AssignmentValidationResult` has a stable machine-readable `AssignmentValidationCode` and optional diagnostic detail. UI and logs may localize or elaborate the detail, but branching uses the code. Validation is performed again on the server immediately before every state-changing action and after reservation acquisition before the source vehicle is detached.

The operation identifies the required strictness: configuration, trip start/resume, placement, manual load, hide/release, or save restoration. `Valid` is the only success code. Missing, unowned, occupied, moving, destroyed, delivery-controlled, unsupported, mismatched, stale, and foreign-reservation conditions remain distinct failures.

## Trip state machine

| State | Meaning | Holds reservation | Vehicle visibility |
| --- | --- | --- | --- |
| `Idle` | No active trip. | No | Visible unless `ManuallyHidden`. |
| `Reserved` | Destination lease acquired; source not yet changed. | Yes | Unchanged. |
| `InTransit` | Exact source vehicle detached and timer running. | Yes | Hidden/safely held. |
| `WaitingForBay` | Timer complete but physical or vanilla availability blocks placement. | Yes | Hidden/safely held. |
| `Placing` | Revalidating and aligning the exact vehicle. | Yes | Transitional. |
| `Placed` | Vehicle is the physical dock occupant. | Yes | Visible. |
| `Releasing` | Removing/hiding the placed vehicle before lease release. | Yes | Transitional. |
| `Recovering` | Restoring a known vehicle to a safe state after interruption. | Yes until cleanup finishes. | Transitional. |
| `Faulted` | Recovery failed and cleanup is complete. | No | Must never remain silently hidden. |

Legal normal transitions are:

```text
Idle -> Reserved -> InTransit -> WaitingForBay -> Placing -> Placed -> Releasing -> Idle
                               \-> Placing
                         Placing -> WaitingForBay
```

`Reserved` may return directly to `Idle` only before the source vehicle changes. Every active state may enter `Recovering`; recovery ends at `Idle` or, after reservation cleanup and an explicit visible/safe-state attempt, `Faulted`. `Faulted` may re-enter recovery or be administratively cleared to idle. Other jumps are illegal.

## Reservation ownership

A reservation is keyed by `LoadingBayKey` and owned by the pair `(HandlerId, TripId)`, not by a player, vehicle, property, or Handler alone. A successful acquisition returns a `ReservationLease` containing an opaque positive token.

- Acquisition is atomic and idempotent for the same owner.
- A different owner receives `OwnedByAnother` and cannot displace the lease.
- Release requires the exact bay, owner, trip, and token. A stale lease cannot release a later acquisition.
- `ReleaseForHandler` is lifecycle cleanup for firing, deletion, despawn, missing-after-load recovery, and mod unload; it never grants ownership to another trip.
- The registry makes its bays unavailable through the `DeliveryManager.IsLoadingBayFree(Property, int)` postfix.
- A lease is necessary but not sufficient for placement. Vanilla availability and physical dock occupancy are rechecked, and an occupant is never displaced.
- The lease is acquired before detaching the source vehicle and released only after placement cleanup, cancellation before movement, recovery, assignment removal, or lifecycle cleanup.

## Service boundaries

- `IHandlerAssignmentRegistry` is the single assignment/revision authority and snapshot source. It separates configuration, load restoration, legal trip transition, and removal operations so callers cannot write arbitrary active state.
- `IHandlerAssignmentValidator` resolves current game objects and returns stable validation results; it does not mutate them.
- `IHandlerReservationRegistry` is the atomic lease authority. Its snapshot exists for save/recovery diagnostics, not client-side mutation.
- `IHandlerMovementCoordinator` owns trip transitions and physical vehicle operations. It consumes the other services rather than duplicating their state.
- `IHandlerLifecycleSink` funnels firing, deletion, despawn, load recovery, and unload into movement recovery and reservation cleanup.

M2 owns runtime Handler construction and lifecycle signals, M3 owns assignment UI/storage and persistence adapters, and M4 owns reservation and movement implementations. Cross-milestone code depends on these contracts instead of concrete implementations.

## Verification

The dependency-free verifier locks identity rules, assignment invariants, legal transitions, reservation ownership, and validation result behavior:

```powershell
.\scripts\verify-contracts.ps1 -DotNet "<dotnet>"
```

`scripts/build-il2cpp.ps1` runs both the pinned game API verifier and this contract verifier before compiling the mod.
