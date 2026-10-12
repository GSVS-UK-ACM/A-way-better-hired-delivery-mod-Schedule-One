# Handler Runtime Worker

Status: Hiring, appearance, and save/load restoration runtime verified; lifecycle `dev5` awaiting runtime proof

Target: Schedule I 0.4.7f12 IL2CPP

## Runtime type

`HandlerEmployee` is an injected `MonoBehaviour` role component with the native-pointer constructor required when IL2CPP creates it. It binds to the real game `Employee` component on the same object and sets that component's `Type` to the existing `EEmployeeType.Handler` value. The game-owned Employee remains responsible for NPC, FishNet, lifecycle, and save behavior.

`HandlerRuntimeRegistration.Register` idempotently registers the role component with `ClassInjector` during mod initialization and fails mod initialization if registration cannot be confirmed.

The first runtime attempt used direct inheritance from `Employee`. Schedule I's pinned interop runtime rejected that injected type while resolving the inherited `NPCMovement.WalkResult` virtual signature. Composition avoids that unsupported injection boundary while retaining a genuine base-game Employee as the networked worker.

M2-001 does not manually construct or spawn an employee. M2-002 adds a distinct Vehicle Handler fixer choice while preserving the vanilla construction path. A reserved, verifier-guarded wire value is mapped to the base Handler prefab on the server, and a save-safe employee-ID marker identifies only Vehicle Handler hires. The registered role component attaches idempotently after server creation and normal employee initialization; the latter covers client replicas and workers restored from base save data.

## Appearance

A Harmony postfix observes the game's normal `Employee.InitializeAppearance` completion. It ignores every non-Handler employee, then recolors the highest-priority colorable worn object with a high-visibility orange role color:

- only runtime objects in `AvatarAppearance._wornObjects` are considered, so naked/body objects and skin are never recolored;
- the first color property on the last suitable worn object is changed, then the avatar layers are repainted;
- no shared outfit or appearance asset is mutated;
- if there is no suitable worn object or the override throws, the generated base appearance remains intact and a warning is logged.

The worker therefore keeps the game's gender, body, face, hair, voice, and mugshot selection while gaining a visible Handler role cue when the selected employee appearance contains a colorable worn object.

## Scope boundary

The runtime worker and M2-002 hiring integration do not:

- replace either employee creation method (the custom discriminator is translated by narrow prefix/postfix patches around the vanilla lookup and creation path);
- clone or register a FishNet spawnable prefab;
- replace or bypass hiring, wages, beds, lockers, firing, saving, or leave/despawn;
- implement assignments, management UI, reservations, or vehicle movement.

The base hiring path is described in [Hiring Integration](HIRING_INTEGRATION.md). Work lifecycle and later behavior remain assigned to subsequent backlog items.

M2-003 now provides the work and cleanup boundary described in [Handler Lifecycle](HANDLER_LIFECYCLE.md). Marked Vehicle Handlers remain idle instead of entering vanilla Packager jobs; the later movement coordinator will consume their base readiness state independently.

## Verification

The pinned API verifier now includes `Il2CppInterop.Runtime.dll`, `Il2CppScheduleOne.Core.dll`, the injection methods, employee role field, avatar access, appearance initialization, runtime worn-object collection, color-property mutation, and layer repaint APIs.

After compilation, `scripts/verify-handler-runtime.ps1` inspects the produced assembly and verifies:

- public `MonoBehaviour` injection boundary;
- the native-pointer constructor;
- idempotent marker reuse and `GameObject.AddComponent<HandlerEmployee>` attachment;
- Unity `Awake` binding to the real Employee component;
- Handler role assignment;
- server-creation and normal-initialization postfix dispatch;
- Handler-filtered Harmony appearance dispatch, worn-object color mutation, and repaint;
- idempotent IL2CPP registration.

## Runtime evidence

On 2026-09-25, the exact built DLL was installed into the game Mods directory and Schedule I was launched twice:

1. Direct `Employee` inheritance failed during mod initialization with `Couldn't find System.Type for Il2Cpp type: NPCMovement+WalkResult`. No worker was created and the game remained responsive at the main menu.
2. After changing to the composed role component, MelonLoader logged `Registered mono type VehicleHandlers.Employees.HandlerEmployee in il2cpp domain`, followed by the Vehicle Handlers initialization banner. No Vehicle Handlers exception occurred.

On 2026-10-09, Schedule I 0.4.7f12 replaced the avatar settings/body-layer runtime API with `AvatarAppearance` and runtime avatar objects. The appearance implementation and verifier were migrated to that API. The updated DLL registered the Handler component and printed the initialization banner on v0.4.7f12 without a Vehicle Handlers exception.

The mod also prints an orange GSVS startup banner after successful Handler runtime registration. The corrected hiring-integration test build reports Vehicle Handlers `v0.1.0`, Schedule I `0.4.7f12 | IL2CPP`, build `0.4.7f12-dev3`, creator `GSVS UK ACM`, project owner `xboxnuker-rgb`, and the `Hire. Assign. Deliver.` tagline.

The runtime-verified `0.4.7f12-dev3` DLL SHA-256 is `65F4C312643A2C2D7B3382651F3963186E74863E7F56CEA3038BF618178A59A2`. Superseded DLLs are retained outside the repository with disabled extensions and cannot be loaded by MelonLoader.

Startup registration is proven. The first `dev2` gameplay inspection exposed that the broad Handler filter reused the vanilla Handler/packager choice; that build was rejected before hiring. On 2026-10-12, `dev3` displayed the independent fifth Vehicle Handler choice, hired Michelle Martin through the concrete `Il2CppScheduleOne.Employees.Packager` wrapper, rendered the orange worn-object cue, saved the `vehiclehandler_michelle_martin` identity, and attached the role exactly once again after a menu/save reload. Ordinary Handlers remained unmarked. `VH-M2-002` is complete; assignments and vehicle work remain deliberately out of scope.
