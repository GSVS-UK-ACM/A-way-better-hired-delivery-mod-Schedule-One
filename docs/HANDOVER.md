# Current Handover

## Active claim

- Backlog: `VH-M2-003`
- Owner: Codex
- Branch: `feat/vh-m2-003-lifecycle`
- Status: `IN_PROGRESS`

## Current outcome

Foundation PR #1, exact API-verifier PR #2, contract PR #3, Handler runtime PR #4, and hiring PR #5 are merged. `VH-M2-002` is complete and runtime verified. `VH-M2-003` now has a compiling `dev4` lifecycle implementation: marked Vehicle Handlers are excluded from vanilla Packager work, expose base work readiness, and forward fire/despawn/unload cleanup through `IHandlerLifecycleSink`. Assignments, reservations, and movement remain unchanged.

## Compatibility target

- Schedule I: `0.4.7f12`
- Backend: IL2CPP
- MelonLoader build references: `0.7.0 Open-Beta` preserved reference layout; the installed DLL was runtime verified under active MelonLoader `0.7.3` on 2026-10-12
- `Assembly-CSharp.dll` SHA-256: `CEC1F3D02B38CEBB59A977FB8C23400094203C63FF7AAA5094143925475BB2CC`
- `Il2CppFishNet.Runtime.dll` SHA-256: `E8972F81F848077A0A8B337D343D724A44E6B312A1C8B92E3892097175B59C80`
- `Il2CppScheduleOne.Core.dll` SHA-256: `A4932C4750637F901F122DF90AD0101F09DDFC3E8F204B1F3C3B5BD77F462926`
- `Il2CppInterop.Runtime.dll` SHA-256: `A1154A31EC72D4097A0A1DB50E046D97AA6C9B60BCFEB4D0489DF6230960F674`
- `UnityEngine.CoreModule.dll` SHA-256: `E993B0A6388D5A21748B28E5E40EBEC1BC43B01DFD550AEBBCB79F3072A48E87`
- `0Harmony.dll` SHA-256: `6C898933B52149E8BCF6722305C7E4D94ADD47C9E53B364096F91721624C0877`

Game references remain outside this repository.

## Verification status

- Strict static API verification passed all 141 checks against the v0.4.7f12 out-of-tree reference set.
- Dependency-free contract verification passed all 110 assertions, including the complete 9-by-9 trip transition matrix.
- Compiled Handler runtime verification passed all 58 checks for hiring plus Handler-only work suppression, base readiness, stable lifecycle identity, idempotent cleanup, sink forwarding, and mod shutdown.
- An intentionally incorrect `Assembly-CSharp.dll` hash was rejected before API inspection.
- Release build completed with zero warnings and zero errors using .NET SDK `8.0.425`.
- Verified and installed Handler-worker DLL SHA-256: `6271DB9D892DF71A83E90C3A656C6BD9CAA2A646847FA4AD8AA9DC1A013955E4`.
- Rejected `0.4.7f12-dev2` DLL SHA-256: `B54FB9EEF53B0BA217E44C8F075A77DEF23DB8095B38D5B737743F24ED6619DC`; startup passed, but its player-facing choice was the vanilla Handler/Packager. It must not be used for hiring.
- Runtime-verified corrected `0.4.7f12-dev3` DLL SHA-256: `65F4C312643A2C2D7B3382651F3963186E74863E7F56CEA3038BF618178A59A2`; tested on 2026-10-12 with rejected `dev2` retained under a disabled extension.
- The production project now compiles only `Properties/AssemblyInfo.cs` and `Source/**/*.cs`; test build artifacts cannot leak into the mod DLL.
- First in-game launch rejected direct `Employee` subclass injection because the interop runtime could not map inherited `NPCMovement.WalkResult`; the game remained responsive and no worker was created.
- Second launch with component composition registered `VehicleHandlers.Employees.HandlerEmployee` successfully and printed the mod initialization banner with no Vehicle Handlers exception.
- On 2026-10-09, MelonLoader identified game version `0.4.7f12`, regenerated changed interop assemblies, and exposed removal of the old `Avatar.CurrentSettings`/`SetBodyLayer` path. The Handler appearance now mutates the first color property on an instance-owned worn object and calls `AvatarAppearance.RepaintLayers`; the strict v0.4.7 verifier and build pass.
- The v0.4.7-compatible DLL was installed and launched. MelonLoader again registered `VehicleHandlers.Employees.HandlerEmployee` and printed the Vehicle Handlers initialization banner with no Vehicle Handlers exception. Other installed mods logged unrelated v0.4.7 patch/type failures.
- On 2026-10-10, the orange GSVS `CREATED BY` banner was added after successful runtime registration. A live v0.4.7f12 launch printed the mod version, target/backend, `0.4.7f12-dev1` build identity, `GSVS UK ACM`, `xboxnuker-rgb`, and `Hire. Assign. Deliver.` before the normal initialization message. The game test process was stopped afterward.
- MelonLoader logged a missing `MethodXrefScanCache.db` while finalizing the first post-update interop generation, but emitted the new assemblies and completed game/mod startup. Treat this as a local MelonLoader generation warning to watch, not Vehicle Handlers runtime evidence.
- The corrected M2-002 implementation deliberately does not introduce a FishNet prefab. The distinct choice uses a reserved wire discriminator and `vehiclehandler_` employee-ID marker; `CreateEmployee_Server` and `Employee.Initialize` attach only marked Vehicle Handlers to the vanilla employee object.
- On 2026-10-12 under MelonLoader 0.7.3, the fixer showed vanilla Handler `[3]` and Vehicle Handler `[5]`; hiring created Michelle Martin, attached `HandlerEmployee` to `Il2CppScheduleOne.Employees.Packager`, and rendered the orange waist cue without changing her skin/body.
- The save stored `vehiclehandler_michelle_martin`. After returning to the menu and reloading, `Employee.Initialize` produced one fresh attachment log and Michelle retained the cue and normal dialogue. No Vehicle Handlers warning or exception occurred.
- Worker Collision/HWE emitted physical-capsule warnings for all loaded employee types, including Michelle and ordinary vanilla workers. These are unrelated full-mod-set noise, not a Vehicle Handlers failure.
- `0.4.7f12-dev4` compiles with zero warnings/errors and blocks only marked Vehicle Handlers from `ShouldIdle`, packaging, pressing, and both Packager item-moving entry points. Fire, leave/despawn, and mod shutdown enter the frozen lifecycle sink boundary. Runtime evidence is pending.
- Installed `0.4.7f12-dev4` DLL SHA-256: `DA099C336EC4EAE8E22DC8647EADBB27AC37874EC133BE0F967BAB60ED543383`. The proven `dev3` DLL is preserved as `VehicleHandlers.dll.disabled.65F4C312643A2C2D7B3382651F3963186E74863E7F56CEA3038BF618178A59A2` for rollback.

Commands:

```powershell
.\scripts\verify-game-api.ps1 -MelonLoaderRoot "<out-of-tree-MelonLoader>"
.\scripts\verify-contracts.ps1 -DotNet "<dotnet-8.0.425>"
.\scripts\build-il2cpp.ps1 -MelonLoaderRoot "<out-of-tree-MelonLoader>" -DotNet "<dotnet-8.0.425>"
```

## Next work

1. Install `0.4.7f12-dev4` and launch under MelonLoader 0.7.3.
2. Confirm Michelle reloads, remains idle, retains normal dialogue, and does not enter vanilla Packager work.
3. Confirm at least one unmarked vanilla Handler still performs its configured work.
4. Use an expendable save for a firing/leave test, then inspect for a clean base despawn and no Vehicle Handlers exception.
5. Keep job assignment UI, assignment persistence, reservations, and vehicle movement in their later backlog items.
