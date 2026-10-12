# Handler Hiring Integration

Status: Distinct option statically verified; awaiting in-game hiring and load evidence

Target: Schedule I 0.4.7f12 IL2CPP

## Strategy

Schedule I's existing player-facing **Handler** is its packaging/moving-items worker, internally labelled `Packager`. Vehicle Handlers therefore adds a separate fifth fixer choice: **Vehicle Handler (moving owned vehicles)**. The vanilla Handler choice and every existing unmarked Handler remain unchanged.

The new choice clones the vanilla Packager dialogue route so property selection, confirmation, fees, and the surrounding conversation remain base-game behavior. It temporarily uses enum value `4` as a wire discriminator; the compatibility gate proves that value is unused in the pinned game. `GetEmployeePrefab` supplies the vanilla Packager prefab for price/confirmation display, and the server maps the discriminator back to `EEmployeeType.Handler` before normal construction.

The server prefixes the generated employee ID with `vehiclehandler_`. Employee IDs already cross the base FishNet initialization call and are included in base employee save data, so this marker identifies the subtype on the server, clients, and reload without inventing a network prefab or replacing the employee. Two exact-signature Harmony postfixes attach the injected `HandlerEmployee` marker only when that identity is present:

- `EmployeeManager.CreateEmployee_Server(Property, EEmployeeType, string, string, string, bool, int, Vector3, Quaternion, string)` attaches on the authoritative creation result when the custom discriminator or persistent ID marker is present;
- `Employee.Initialize(NetworkConnection, string, string, string, string, string, bool, int)` attaches when the initialized employee ID contains the reserved marker, covering replicas and save restoration.

`HandlerEmployee.EnsureAttached` first reuses an existing marker and only calls `GameObject.AddComponent<HandlerEmployee>` when needed. Both postfixes therefore converge safely if they run for the same employee. Failure logs a warning and preserves the vanilla employee instead of interfering with hiring. Appearance checks the marker component rather than the broad Handler enum, preventing ordinary Packagers from receiving Vehicle Handler colors.

## Preserved base behavior

Because the original employee object remains in place, the game continues to own its FishNet identity, NPC behavior, property assignment, wage, bed, locker, save, firing, and leave/despawn lifecycle. This milestone adds identity and visual-role attachment only; later milestones add work scheduling and vehicle delivery behavior.

## Static evidence

The compatibility gate pins the fixer dialogue, discriminator availability, prefab lookup, both game lifecycle signatures, and the Unity `Component.gameObject` / generic `GameObject.AddComponent<T>` surface. The compiled-runtime verifier confirms that:

- marker attachment checks for an existing `HandlerEmployee`;
- new attachment uses the vanilla employee's own `GameObject`;
- the fifth choice has distinct player-facing text while retaining the vanilla route;
- server creation translates and marks only Vehicle Handler hires;
- employee initialization reads the persistent ID marker and attaches through the same helper;
- unmarked vanilla Handlers are excluded from attachment and appearance changes.

The 0.4.7f12 build passes 135 API checks, 110 contract assertions, and 39 compiled-runtime checks with zero compiler warnings or errors.

## Required runtime proof

1. Install the exact `0.4.7f12-dev3` test DLL and launch Schedule I 0.4.7f12 IL2CPP.
2. Confirm the fixer shows both the unchanged vanilla Handler option and a separate fifth Vehicle Handler option.
3. Hire the Vehicle Handler through the new option.
4. Confirm one attachment log naming the concrete runtime employee wrapper type and no Vehicle Handlers exception.
5. Confirm the worker remains a normal manageable employee and visibly receives the orange worn-object cue without skin/body recoloring.
6. Save, reload, and confirm attachment remains idempotent and the employee lifecycle still works.
