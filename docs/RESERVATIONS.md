# Loading-Bay Reservations

## Authority and identity

`HandlerReservationRegistry` is the process-local, server-owned authority for Vehicle Handler loading-bay leases. A bay is identified by the complete `LoadingBayKey`: property code, loading-dock GUID, and dock index. A lease is owned by the complete `(HandlerId, TripId)` pair and carries an opaque positive token.

The registry is deliberately independent of Unity objects, Harmony, persistence, and vehicle movement. `VH-M4-002` will perform live validation and call the registry from the server-side movement coordinator.

## Lease rules

- Acquisition is atomic. Exactly one owner can create a lease for a bay.
- Repeating acquisition for the same Handler trip is idempotent and returns the existing lease.
- A different Handler or trip receives `OwnedByAnother` and cannot displace the lease.
- Release requires the exact bay, owner, and token. Foreign owners and stale tokens cannot release a newer lease.
- Handler lifecycle cleanup releases every lease owned by that Handler.
- Load start and mod shutdown replace the process-local registry, preventing stale leases from crossing saves or surviving unload.
- Snapshots are detached, deterministically ordered copies for diagnostics and later recovery work.

## Base-game delivery integration

`HandlerLoadingBayAvailabilityPatch` is an exact postfix on `DeliveryManager.IsLoadingBayFree(Property, int)`. It resolves the same property code, dock GUID, and dock index used by the registry.

The postfix preserves every vanilla `false` result. When vanilla reports a bay free, an active Vehicle Handler lease changes the result to `false`, so normal deliveries cannot claim that bay. An invalid runtime property/dock identity fails closed for Handler lookup without changing the vanilla result.

The reciprocal rule—vanilla occupancy or delivery control preventing a Handler from acquiring and using a bay—belongs to the live movement validation in `VH-M4-002`. This milestone does not detach, hide, move, place, or create vehicles.

## Verification

The dependency-free reservation verifier covers invalid requests, idempotent acquisition, foreign-owner exclusion, exact and stale release, Handler cleanup, deterministic detached snapshots, and concurrent contention. The compiled-runtime verifier checks load/unload cleanup, exact stable-key resolution, and the delivery-availability integration.

```powershell
.\scripts\verify-reservations.ps1 -DotNet "<path-to-dotnet-8.0.425>"
.\scripts\build-il2cpp.ps1 -MelonLoaderRoot "<out-of-tree-MelonLoader>" -DotNet "<path-to-dotnet-8.0.425>"
```
