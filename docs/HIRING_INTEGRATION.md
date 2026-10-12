# Handler Hiring Integration

Status: Statically verified; awaiting in-game hiring and load evidence

Target: Schedule I 0.4.7f12 IL2CPP

## Strategy

Schedule I already exposes `EEmployeeType.Handler` and routes employee creation through `EmployeeManager.CreateEmployee_Server`. Vehicle Handlers leaves that route intact: the game chooses, creates, owns, spawns, and saves the networked employee. The mod does not clone a prefab, register a FishNet spawnable, replace the returned employee, or patch `GetEmployeePrefab`.

Two exact-signature Harmony postfixes attach the injected `HandlerEmployee` marker:

- `EmployeeManager.CreateEmployee_Server(Property, EEmployeeType, string, string, string, bool, int, Vector3, Quaternion, string)` attaches on the authoritative creation result when the requested type is Handler;
- `Employee.Initialize(NetworkConnection, string, string, string, string, string, bool, int)` attaches when the initialized employee reports `EmployeeType == Handler`, covering replicas and save restoration.

`HandlerEmployee.EnsureAttached` first reuses an existing marker and only calls `GameObject.AddComponent<HandlerEmployee>` when needed. Both postfixes therefore converge safely if they run for the same employee. Failure logs a warning and preserves the vanilla employee instead of interfering with hiring.

## Preserved base behavior

Because the original employee object remains in place, the game continues to own its FishNet identity, NPC behavior, property assignment, wage, bed, locker, save, firing, and leave/despawn lifecycle. This milestone adds identity and visual-role attachment only; later milestones add work scheduling and vehicle delivery behavior.

## Static evidence

The compatibility gate pins both game lifecycle signatures and the Unity `Component.gameObject` / generic `GameObject.AddComponent<T>` surface. The compiled-runtime verifier confirms that:

- marker attachment checks for an existing `HandlerEmployee`;
- new attachment uses the vanilla employee's own `GameObject`;
- server creation filters the requested Handler role and attaches the returned employee;
- employee initialization reads the runtime `EmployeeType` and attaches through the same helper.

The 0.4.7f12 build passes 125 API checks, 110 contract assertions, and 25 compiled-runtime checks with zero compiler warnings or errors.

## Required runtime proof

1. Install the exact `0.4.7f12-dev2` test DLL and launch Schedule I 0.4.7f12 IL2CPP.
2. Hire a Handler through the normal fixer flow, or load a save that already contains a vanilla Handler.
3. Confirm one attachment log naming the concrete runtime employee wrapper type and no Vehicle Handlers exception.
4. Confirm the worker remains a normal manageable employee and visibly receives the orange worn-object cue without skin/body recoloring.
5. Save, reload, and confirm attachment remains idempotent and the employee lifecycle still works.
