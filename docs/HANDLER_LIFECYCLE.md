# Vehicle Handler Lifecycle

Status: Statically verified; awaiting in-game `0.4.7f12-dev4` evidence

Target: Schedule I 0.4.7f12 IL2CPP

## Work boundary

The base game constructs every Vehicle Handler as a genuine `Packager`, but a marked worker must never fall through to packaging, brick-press, or vanilla item-moving work. Narrow Harmony prefixes therefore apply only when the runtime object carries `HandlerEmployee`:

- `Packager.ShouldIdle()` returns `true` for a Vehicle Handler so the normal employee loop remains idle;
- `StartPackaging(PackagingStation)` is rejected;
- `StartPress(BrickPress)` is rejected;
- both `StartMoveItem` overloads are rejected.

Unmarked vanilla Handlers execute every original method unchanged. Base employee pay, home, dialogue, transfer, save, and management behavior is not replaced.

`HandlerEmployee.IsBaseWorkAvailable` exposes the base prerequisites needed by the later movement coordinator: a worker must still exist, must not be fired, must be paid for the day, and must have an assigned property. Assignment validity and vehicle/destination checks remain owned by M3 and M4.

## Stable identity

The employee initialization GUID is bound to `HandlerEmployee` when the role marker attaches. Lifecycle cleanup converts that canonical GUID into the frozen `HandlerId`; it never uses the employee index, name, or save-list position. If the GUID is unavailable, the event fails safely and leaves cleanup to load recovery.

## Cleanup boundary

`HandlerLifecycleBridge` forwards lifecycle signals through the frozen `IHandlerLifecycleSink` contract:

- `Packager.Fire()` reports `Fired` before the base firing path mutates or destroys the worker;
- `Employee.LeavePropertyAndDespawn()` reports `Despawned` before the base despawn path;
- `VehicleHandlersMod.OnDeinitializeMelon()` reports `ModUnloading` once.

The role component suppresses duplicate unavailable notifications, so a fire followed by leave/despawn cannot clean the same Handler twice. The bridge catches sink failures and preserves the base employee lifecycle. No concrete assignment, recovery, or reservation service is introduced early; M3/M4 will register the sink implementation.

Generic Unity destruction is deliberately not treated as deletion because returning to the menu destroys loaded employee objects without deleting their saved assignments. Missing/deleted Handler reconciliation belongs to the M3 load pass, which can compare saved identities with the stabilized live employee set.

## Static verification

The exact API gate now pins all six Packager lifecycle/work signatures in addition to the existing employee lifecycle surface. The compiled verifier confirms Handler-only filtering, all vanilla work guards, stable GUID validation, idempotent unavailable signaling, sink forwarding, and mod-unload cleanup.

Current result:

- 141 exact game/API checks;
- 110 dependency-free contract assertions;
- 58 compiled-runtime checks;
- zero compiler warnings and zero compiler errors.

The installed `0.4.7f12-dev4` test DLL SHA-256 is `DA099C336EC4EAE8E22DC8647EADBB27AC37874EC133BE0F967BAB60ED543383`. The runtime-verified `dev3` DLL is preserved under a disabled extension for rollback.

## Required runtime proof

1. Launch `0.4.7f12-dev4` under MelonLoader 0.7.3 and confirm normal startup plus load-time role attachment.
2. Confirm Michelle remains idle and keeps normal employee dialogue without performing any previously configured Packager job.
3. Confirm an ordinary vanilla Handler can still perform its configured Packager work.
4. Fire a disposable Vehicle Handler only on an expendable test save and confirm the base firing/leave path completes without a Vehicle Handlers exception.
5. Return to the menu and exit once, confirming shutdown does not corrupt the saved Vehicle Handler or emit duplicate cleanup failures.
