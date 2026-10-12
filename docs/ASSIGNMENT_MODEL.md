# Handler Assignment Model

Status: Implemented by `VH-M3-001`
Target: Schedule I 0.4.7f12 IL2CPP

## Scope

`HandlerAssignmentRegistry` is the in-memory authority for the frozen `IHandlerAssignmentRegistry` contract. It contains no game-object lookup, clipboard UI, file I/O, reservation mutation, or vehicle movement. Those integrations remain owned by `VH-M3-002`, `VH-M3-003`, and M4.

## Invariants

- A Handler GUID has at most one current assignment.
- A vehicle GUID may be claimed by at most one Handler assignment.
- Configuration, trip transitions, and removal use compare-and-swap revisions.
- Initial configuration compares against revision `0` and stores revision `1`.
- Successful removal retains a revision tombstone, preventing a stale caller from recreating a deleted assignment.
- Configuration and removal are rejected while the assignment is outside `Idle`.
- The first `Idle -> Reserved` transition binds a non-empty trip GUID. Every later transition requires that exact GUID, and returning to `Idle` clears it.
- Snapshot values are immutable and returned in deterministic Handler-GUID order.

## Save restoration boundary

`TryRestore` accepts multiple distinct saved snapshots only during the registry's initial restore phase. The first normal configuration, transition, or removal closes that phase. Duplicate Handler identities, duplicate vehicle identities, late restores, and unknown saved trip states are rejected without replacing current state.

Restored active trip state and identity remain available for M4 validation and recovery. Loading never resolves or creates a physical vehicle in this layer.

## Authority and validation

The registry is a server-owned service, not a network request handler. The future configuration and movement gateways must establish server authority and run `IHandlerAssignmentValidator` immediately before calling it. Live Handler, ownership, vehicle-safety, property, dock, reservation, and payment checks therefore remain outside this pure state authority.

## Verification

Run the dependency-free assignment verifier:

```powershell
.\scripts\verify-assignments.ps1 -DotNet "<dotnet>"
```

The full IL2CPP build script runs this after the frozen-contract verifier and before compilation.
