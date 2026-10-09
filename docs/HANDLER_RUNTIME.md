# Handler Runtime Worker

Status: Startup verified; awaiting worker-construction and appearance verification

Target: Schedule I 0.4.7f12 IL2CPP

## Runtime type

`HandlerEmployee` is an injected `MonoBehaviour` role component with the native-pointer constructor required when IL2CPP creates it. It binds to the real game `Employee` component on the same object and sets that component's `Type` to the existing `EEmployeeType.Handler` value. The game-owned Employee remains responsible for NPC, FishNet, lifecycle, and save behavior.

`HandlerRuntimeRegistration.Register` idempotently registers the role component with `ClassInjector` during mod initialization and fails mod initialization if registration cannot be confirmed.

The first runtime attempt used direct inheritance from `Employee`. Schedule I's pinned interop runtime rejected that injected type while resolving the inherited `NPCMovement.WalkResult` virtual signature. Composition avoids that unsupported injection boundary while retaining a genuine base-game Employee as the networked worker.

M2-001 does not manually construct or spawn an employee. The hiring integration in M2-002 must attach the registered role component to its employee construction donor before spawn.

## Appearance

A Harmony postfix observes the game's normal `Employee.InitializeAppearance` completion. It ignores every non-Handler employee, then recolors the highest-priority colorable worn object with a high-visibility orange role color:

- only runtime objects in `AvatarAppearance._wornObjects` are considered, so naked/body objects and skin are never recolored;
- the first color property on the last suitable worn object is changed, then the avatar layers are repainted;
- no shared outfit or appearance asset is mutated;
- if there is no suitable worn object or the override throws, the generated base appearance remains intact and a warning is logged.

The worker therefore keeps the game's gender, body, face, hair, voice, and mugshot selection while gaining a visible Handler role cue when the selected employee appearance contains a colorable worn object.

## Scope boundary

This task supplies the injected runtime class and its appearance behavior only. It does not:

- patch `EmployeeManager.GetEmployeePrefab` or either creation method;
- clone or register a FishNet spawnable prefab;
- change hiring, wages, beds, lockers, firing, saving, or leave/despawn;
- implement assignments, management UI, reservations, or vehicle movement.

Those behaviors remain assigned to M2-002 and later backlog items.

## Verification

The pinned API verifier now includes `Il2CppInterop.Runtime.dll`, `Il2CppScheduleOne.Core.dll`, the injection methods, employee role field, avatar access, appearance initialization, runtime worn-object collection, color-property mutation, and layer repaint APIs.

After compilation, `scripts/verify-handler-runtime.ps1` inspects the produced assembly and verifies:

- public `MonoBehaviour` injection boundary;
- the native-pointer constructor;
- Unity `Awake` binding to the real Employee component;
- Handler role assignment;
- Handler-filtered Harmony appearance dispatch, worn-object color mutation, and repaint;
- idempotent IL2CPP registration.

## Runtime evidence

On 2026-09-25, the exact built DLL was installed into the game Mods directory and Schedule I was launched twice:

1. Direct `Employee` inheritance failed during mod initialization with `Couldn't find System.Type for Il2Cpp type: NPCMovement+WalkResult`. No worker was created and the game remained responsive at the main menu.
2. After changing to the composed role component, MelonLoader logged `Registered mono type VehicleHandlers.Employees.HandlerEmployee in il2cpp domain`, followed by the Vehicle Handlers initialization banner. No Vehicle Handlers exception occurred.

On 2026-10-09, Schedule I 0.4.7f12 replaced the avatar settings/body-layer runtime API with `AvatarAppearance` and runtime avatar objects. The appearance implementation and verifier were migrated to that API. The updated DLL registered the Handler component and printed the initialization banner on v0.4.7f12 without a Vehicle Handlers exception.

The current successful installed DLL SHA-256 is `EE5631C6D06CF1DBC74B2A32E5625F608F85E4011EC65A7A0F32FC99A9A19000`. Superseded DLLs are retained outside the repository with disabled extensions and cannot be loaded by MelonLoader.

Startup registration is now proven. A future FishNet prefab, component attachment, role persistence, and rendered appearance still require isolated in-game evidence through M2-002 before this item moves from `VERIFYING` to `DONE`.
