# Vehicle Handler Lifecycle

Status: Complete and runtime verified on `dev5`

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

The exact API gate now pins all six Packager lifecycle/work signatures in addition to the existing employee lifecycle surface. The compiled verifier confirms Handler-only filtering, all vanilla work guards, stable GUID validation, idempotent unavailable signaling, sink forwarding, and mod-unload cleanup. One-shot diagnostics report the first active work guard, unavailable reason, and lifecycle shutdown without per-tick log spam.

Current result:

- 141 exact game/API checks;
- 110 dependency-free contract assertions;
- 64 compiled-runtime checks;
- zero compiler warnings and zero compiler errors.

The first `dev4` menu launch registered `HandlerEmployee` and initialized the mod, but IL2CPP logged `Assembly VehicleHandlers.dll is not registered in il2cpp` while scanning a managed-only helper whose signature referenced the mod-owned lifecycle enum. The game then exited abruptly during save loading immediately after `Load start!`, without a managed exception. Those helpers are now explicitly decorated with `HideFromIl2Cpp`; the statically verified `dev5` candidate SHA-256 is `0CB84F9E34F971B36F0AD8E73437FA8EBDE26BA5F07BD696557D8A4ACB9FEE74`. The runtime-verified `dev3` DLL remains preserved under a disabled extension for rollback.

On 2026-10-12, `dev5` registered the component without the `dev4` assembly error, reached the main menu, loaded the existing save, and attached exactly one `HandlerEmployee` to Michelle. She retained the orange Vehicle Handler cue and normal interaction prompt. No Vehicle Handlers warning or exception occurred. HWE and DeliverySpotsPlus emitted unrelated errors in the full mod set.

An additional Vehicle Handler was then hired and fired on the test save. The runtime emitted exactly one `Fired` event for Handler GUID `64baf792-6d7a-4b60-91f3-037ffac126ae`; the later leave/despawn path did not emit duplicate cleanup, and no Vehicle Handlers exception followed.

During the active work period, the owner confirmed that Michelle remained idle while an ordinary unmarked Handler continued normal Packager work. This verifies both sides of the narrow filter: marked Vehicle Handlers are isolated from vanilla work without disabling the base role globally. The game subsequently exited without duplicate Vehicle Handlers cleanup or an exception; save restoration had already been demonstrated by the same `dev5` build.

## Required runtime proof

1. ~~Launch `0.4.7f12-dev5` under MelonLoader 0.7.3 and confirm clean startup plus load-time role attachment without the `dev4` assembly-registration error.~~ Passed on 2026-10-12.
2. ~~Confirm Michelle remains idle and keeps normal employee dialogue without performing any previously configured Packager job.~~ Passed on 2026-10-12.
3. ~~Confirm an ordinary vanilla Handler can still perform its configured Packager work.~~ Passed on 2026-10-12.
4. ~~Fire a disposable Vehicle Handler only on an expendable test save and confirm the base firing/leave path completes without a Vehicle Handlers exception.~~ Passed on 2026-10-12 with exactly one `Fired` notification and no duplicate cleanup.
5. ~~Return to the menu and exit once, confirming shutdown does not corrupt the saved Vehicle Handler or emit duplicate cleanup failures.~~ Exit produced no duplicate cleanup or exception; `dev5` save restoration was verified earlier in the same session.
