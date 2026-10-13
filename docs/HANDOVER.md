# Current Handover

## Active claim

`VH-M3-003` is owned by Codex on `feat/vh-m3-003-assignment-persistence` and is awaiting in-game save/reload verification.

## Current outcome

Foundation PR #1, exact API-verifier PR #2, contract PR #3, Handler runtime PR #4, hiring PR #5, lifecycle PR #6, assignment-model PR #7, and clipboard-controls PR #8 are merged. All M2 work is complete and runtime verified on `dev5`. `VH-M3-001` supplies the in-memory assignment authority, `VH-M3-002` supplies the runtime-verified Vehicle Handler clipboard panel, and `VH-M3-003` now adds versioned per-save persistence for those assignments. Reservations, live-object validation, and physical movement remain unchanged.

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
- Compiled Handler runtime verification passed all 64 checks for hiring plus Handler-only work suppression, base readiness, stable lifecycle identity, one-shot diagnostics, managed/native injection isolation, idempotent cleanup, sink forwarding, and mod shutdown.
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
- The first `0.4.7f12-dev4` menu launch registered the component and initialized the mod, but IL2CPP logged an assembly-registration error while scanning managed-only component helpers. The game then exited abruptly during save loading immediately after `Load start!`, without a managed exception. `dev5` decorates those helpers with `HideFromIl2Cpp` and adds verifier coverage for the boundary.
- Installed `0.4.7f12-dev5` SHA-256: `0CB84F9E34F971B36F0AD8E73437FA8EBDE26BA5F07BD696557D8A4ACB9FEE74`.
- On 2026-10-12, `dev5` registered without the `dev4` assembly error, reached the main menu, loaded the existing save, and attached exactly one marker to Michelle. She retained the orange cue and normal interaction prompt; Vehicle Handlers emitted no warning or exception. The full mod set's HWE and DeliverySpotsPlus errors were unrelated.
- A second Vehicle Handler was hired and fired on the test save. GUID `64baf792-6d7a-4b60-91f3-037ffac126ae` emitted exactly one `Fired` lifecycle event, the later leave/despawn path produced no duplicate cleanup, and no Vehicle Handlers exception followed.
- During the active work period, Michelle remained idle while an ordinary unmarked Handler continued normal Packager work. This runtime-verifies both sides of the narrow work filter.
- `0.4.7f12-dev5` blocks only marked Vehicle Handlers from `ShouldIdle`, packaging, pressing, and both Packager item-moving entry points. Fire, leave/despawn, and mod shutdown enter the frozen lifecycle sink boundary. `VH-M2-003` is complete.
- `VH-M3-001` adds a dependency-free assignment verifier with 51 assertions covering one-vehicle exclusivity, immutable snapshots, stale-write rejection, active-trip reconfiguration/removal blocks, exact trip ownership, legal transitions, revision tombstones, and restore conflicts.
- The complete `0.4.7f12-dev6` gate passes 141 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, and 64 compiled Handler runtime checks; release compilation has zero warnings and zero errors.
- Built `0.4.7f12-dev6` DLL SHA-256: `0D325D4D978D30E49D5AFE5623C36EA20AFA3222F1A9B2989AC1EACF25C69FC0`. It is a build artifact only and has not been installed; this milestone adds no player-facing control surface.
- PR #7 merged `VH-M3-001` into `main` as `c7d2f5f3c04d947be24e8b0af89a92f5d40e6e28`.
- Before claiming `VH-M3-002`, the preserved out-of-tree `0.4.7f12` IL2CPP reference set passed all 141 pinned API checks. The live game's `Assembly-CSharp.dll` is currently modified at startup by Polyfill.Boot and has SHA-256 `2FE6C19C6A14703EB2FD8171C6217F64804D61FE64EC372173796E5417E76971`; it must not replace the canonical preserved build reference.
- `VH-M3-002` adds 46 dependency-free clipboard-model assertions for stable preselection, cycling, immutable choices, revision updates, control-flag preservation, and unavailable-choice failures.
- The pinned API gate now includes `UnityEngine.IMGUIModule.dll` SHA-256 `5B8CBAEB25F4F8024675805B819AE0A26DBC44CEE3EE027969390DBFE4670D20` plus exact management-interface, IMGUI, vehicle-name, property, and dock-name members.
- The complete `0.4.7f12-dev7` gate passes 159 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 46 clipboard assertions, and 81 compiled Handler runtime checks; release compilation has zero warnings and zero errors.
- Built `0.4.7f12-dev7` DLL SHA-256: `1234E161053D443D4AD6DD5995197012D4EDFC65A01B05EEDFD68E7A34C47DF9`.
- With the game closed, `dev7` was installed to `Mods/VehicleHandlers.dll` and its installed SHA-256 matched the build. The previous runtime-verified `dev5` DLL was retained in the ignored local install-backup artifacts with SHA-256 `0CB84F9E34F971B36F0AD8E73437FA8EBDE26BA5F07BD696557D8A4ACB9FEE74`.
- In-game `dev7` verification passed Vehicle Handler panel rendering, stable vehicle/property/dock resolution, apply and reopen persistence at revision 1, disable and reopen persistence at revision 2, and enabled reconfiguration at revision 3. No Vehicle Handlers exception appeared; the observed `DeliverySpotsPlus` coroutine exception was unrelated.
- Player feedback identified that short GUIDs alone do not make same-model vehicles easy to distinguish. `dev8` labels vehicles recognised as a `LoadingDock.DynamicOccupant` or `StaticOccupant` with the dock and property, and omits vehicles claimed by another Handler while retaining the current Handler's own claim.
- The `dev8` gate passes 159 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, and 86 compiled runtime checks; release compilation has zero warnings and zero errors. Built DLL SHA-256: `AB760DA82377CF80107D06E1CA6E8C1811A1F4B3D7F83C34E1199ADDFE7720B3`.
- With the game closed, `dev8` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. The replaced `dev7` DLL was retained in ignored local install-backup artifacts with SHA-256 `1234E161053D443D4AD6DD5995197012D4EDFC65A01B05EEDFD68E7A34C47DF9`.
- In-game `dev8` verification recognised a manually parked Veeper as `Veeper (Loading Dock 1, Storage Unit)`. After Michelle claimed it, the Veeper remained visible in Michelle's panel but disappeared from another Vehicle Handler's chooser. The live log contained no Vehicle Handlers warning or exception; observed HWE and DeliverySpotsPlus errors were unrelated.
- Removing Michelle's idle assignment in `dev8` released its vehicle claim immediately; the vehicle returned to the other Vehicle Handler's available chooser entries.
- Assigning a vehicle to an expendable second Vehicle Handler and then firing that Handler released the idle registry claim; the vehicle immediately returned to Michelle's available chooser entries.
- Selecting an ordinary unmarked vanilla Handler still opened the standard base-game Packager management panel. The Vehicle Handler overlay remained isolated to marked workers.
- PR #8 merged the runtime-verified `VH-M3-002` branch into `main` as `fdcef4f`. Before claiming `VH-M3-003`, the preserved out-of-tree 0.4.7f12 IL2CPP reference set passed all 159 pinned API checks and no overlapping pull request was open.
- `VH-M3-003` adds schema-version-1 per-save assignment documents at `<save folder>/UserData/VehicleHandlers.json`, atomic temporary-file replacement, previous-valid backup recovery, strict stable-ID/state decoding, fresh-registry load isolation, and exact `StartGame`/`onLoadComplete` plus `Save()`/`Save(string)`/`onSaveComplete` lifecycle integration.
- The `dev9` gate passes 165 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, 28 persistence assertions, and 99 compiled runtime checks; release compilation has zero warnings and zero errors. Restore runs after `StartGame` returns so synchronous base-game cleanup cannot erase the fresh registry. Built DLL SHA-256: `6A409B1915EF3970F5FE82D3BA6DBDCD72C363B6F736AA0861CD19E03963F544`.
- With the game closed, `dev9` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. The replaced `dev8` DLL was retained in ignored local install-backup artifacts with SHA-256 `AB760DA82377CF80107D06E1CA6E8C1811A1F4B3D7F83C34E1199ADDFE7720B3`.
- Runtime testing rejected `dev9`: its save prefix logged `Saved 1 Vehicle Handler assignment(s).`, but the base game's remaining save cleanup removed the mod-owned directory before save completion, leaving no `VehicleHandlers.json` to reload.
- `dev10` moves persistence to the exact `SaveManager.onSaveComplete` event and attaches idempotently both during mod initialization and from an exact `SaveManager.Awake` postfix. The complete gate passes 168 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, 28 persistence assertions, and 103 compiled runtime checks; release compilation has zero warnings and zero errors. Built DLL SHA-256: `6DAAD15621AEF061543577D760760FFF98DFE618749C8429F7AE798DF6F1773F`.
- With the game closed, `dev10` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. Rejected `dev9` was retained in ignored local install-backup artifacts with SHA-256 `6A409B1915EF3970F5FE82D3BA6DBDCD72C363B6F736AA0861CD19E03963F544`.
- Runtime testing rejected `dev10`: it loaded and restored safely, but the game's `onSaveComplete` event did not invoke the managed listener during the tested save, so no save log or assignment document was produced.
- `dev11` replaces that event dependency with Harmony postfixes on both exact `SaveManager.Save` overloads. The complete gate passes 165 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, 28 persistence assertions, and 99 compiled runtime checks; release compilation has zero warnings and zero errors. Built DLL SHA-256: `CEAE6815D540E9A16BD1F684ACCBCAB82E5E7BF5C4FBFE7CC8B9F20DDA0A807C`.
- With the game closed, `dev11` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. Rejected `dev10` was retained in ignored local install-backup artifacts with SHA-256 `6DAAD15621AEF061543577D760760FFF98DFE618749C8429F7AE798DF6F1773F`.
- Runtime testing rejected `dev11`: its postfix logged one saved assignment, but the game continued asynchronous cleanup after `Save()` returned and removed the document. No `VehicleHandlers.json` survived under the save tree.
- `dev12` attaches the `onSaveComplete` listener from the loaded-game integration and again idempotently at both exact save entry points, where the live `SaveManager` is guaranteed to exist. It also emits positive evidence when persistence is armed. The complete gate passes 167 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, 28 persistence assertions, and 104 compiled runtime checks; release compilation has zero warnings and zero errors. Built DLL SHA-256: `46BE57EE5F2E4390B23B07FB57DF262BAE75BE41DA950D5296C045D4800A1663`.
- With the game closed, `dev12` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. Rejected `dev11` was retained in ignored local install-backup artifacts with SHA-256 `CEAE6815D540E9A16BD1F684ACCBCAB82E5E7BF5C4FBFE7CC8B9F20DDA0A807C`.
- Runtime testing confirmed `dev12` save persistence: the listener armed after load, `onSaveComplete` wrote one enabled revision-1 assignment, and the 436-byte schema-v1 document survived at the exact save path. Reload then logged `Restored 1`, and the saved Handler GUID matched Michelle's game-owned GUID, but restoration ran roughly 20 seconds before employee reconstruction completed; the later load phase erased the process-local registry and the panel showed no assignment.
- `dev13` stages the selected save at `StartGame`, subscribes idempotently to the live `LoadManager.onLoadComplete` event before load begins, and restores only after the game's final load boundary. It no longer clears the registry from the game's mid-load `CleanUp` route; starting another save and mod shutdown remain isolation boundaries. The complete gate passes 167 pinned API checks, 110 frozen-contract assertions, 51 assignment assertions, 51 clipboard assertions, 28 persistence assertions, and 107 compiled runtime checks; release compilation has zero warnings and zero errors. Built DLL SHA-256: `122E9206D8B696E289AA36C24B0E1742F1EAE24A53CAB5E7C2EF705CECCCE175`.
- With the game closed, `dev13` was installed to `Mods/VehicleHandlers.dll`; its installed SHA-256 matched the verified build. Runtime-verified save-writing build `dev12` was retained in ignored local install-backup artifacts with SHA-256 `46BE57EE5F2E4390B23B07FB57DF262BAE75BE41DA950D5296C045D4800A1663`.

Commands:

```powershell
.\scripts\verify-game-api.ps1 -MelonLoaderRoot "<out-of-tree-MelonLoader>"
.\scripts\verify-contracts.ps1 -DotNet "<dotnet-8.0.425>"
.\scripts\verify-assignments.ps1 -DotNet "<dotnet-8.0.425>"
.\scripts\verify-clipboard.ps1 -DotNet "<dotnet-8.0.425>"
.\scripts\verify-persistence.ps1 -DotNet "<dotnet-8.0.425>"
.\scripts\build-il2cpp.ps1 -MelonLoaderRoot "<out-of-tree-MelonLoader>" -DotNet "<dotnet-8.0.425>"
```

## Next work

1. Launch `0.4.7f12-dev13`, load the existing schema-v1 document, and verify Michelle's assignment/revision appears after final load completion.
2. Verify another save receives an independent empty document and no vehicle moves, hides, duplicates, or changes identity.
