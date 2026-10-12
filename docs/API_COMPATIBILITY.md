# Verified Game API Surface

Status: Verified
Target: Schedule I 0.4.7f12 IL2CPP

## Pinned inputs

| Assembly | SHA-256 |
| --- | --- |
| `Assembly-CSharp.dll` | `CEC1F3D02B38CEBB59A977FB8C23400094203C63FF7AAA5094143925475BB2CC` |
| `Il2CppFishNet.Runtime.dll` | `E8972F81F848077A0A8B337D343D724A44E6B312A1C8B92E3892097175B59C80` |
| `Il2CppScheduleOne.Core.dll` | `A4932C4750637F901F122DF90AD0101F09DDFC3E8F204B1F3C3B5BD77F462926` |
| `Il2CppInterop.Runtime.dll` | `A1154A31EC72D4097A0A1DB50E046D97AA6C9B60BCFEB4D0489DF6230960F674` |
| `UnityEngine.CoreModule.dll` | `E993B0A6388D5A21748B28E5E40EBEC1BC43B01DFD550AEBBCB79F3072A48E87` |
| `0Harmony.dll` | `6C898933B52149E8BCF6722305C7E4D94ADD47C9E53B364096F91721624C0877` |

The verifier also requires every assembly referenced by `VehicleHandlers.Il2Cpp.csproj` to exist in the supplied MelonLoader root. Game assemblies and toolchains remain outside the repository.

## Compatibility gate

`scripts/verify-game-api.ps1` performs 135 exact checks before compilation:

- required MelonLoader and generated IL2CPP reference layout
- pinned game and FishNet assembly hashes
- public type existence and exact base types
- singleton and network-singleton accessors
- Handler enum value, unused custom wire discriminator, fixer choice mutation, hiring entry points, and virtual employee lifecycle methods
- GUID lookup and owned property/vehicle selection collections
- loading-bay availability, occupancy, parking, alignment, and vehicle transition APIs
- FishNet spawn, server, client, ownership, and network-object signals
- save and load events and active save-path identifiers
- IL2CPP class injection, Unity component composition, Handler role assignment, Harmony patch attributes, avatar access, and instance-owned worn-object appearance APIs
- method return types, parameter types, generic arity, static/instance status, and selected virtual flags
- property types, static/instance status, and getter/setter shape

Run:

```powershell
.\scripts\verify-game-api.ps1 -MelonLoaderRoot "<path-to-MelonLoader>"
```

A mismatched hash or changed signature fails immediately with the expected and observed values. The build script runs this gate before invoking the compiler.

## Verified result

- All 135 checks passed against the pinned 0.4.7f12 IL2CPP, Unity, MelonLoader interop, and Harmony reference set.
- A deliberately incorrect `Assembly-CSharp.dll` hash was rejected.
- The release build completed with zero warnings and zero errors using .NET SDK 8.0.425.
- Compiled Handler runtime verification passed all 39 checks, including the distinct fixer option, reserved discriminator, persistent ID marker, vanilla-Handler exclusion, client/load attachment, and startup-banner integration.
- The earlier broad-hook `dev2` build SHA-256 `B54FB9EEF53B0BA217E44C8F075A77DEF23DB8095B38D5B737743F24ED6619DC` was startup-tested but rejected after the UI revealed it reused the vanilla Handler option. The corrected, statically verified and installed `dev3` DLL SHA-256 is `65F4C312643A2C2D7B3382651F3963186E74863E7F56CEA3038BF618178A59A2`; runtime evidence remains pending.

## Boundary

This is static compatibility evidence, not runtime proof. It does not validate game-side behavior, multiplayer replication, save recovery, UI integration, or delivery conflicts. Those remain gated by M2-M5 runtime work. M1-002 freezes the mod-owned identifiers, state machine, validation results, service interfaces, and reservation ownership in [Internal Contracts](INTERNAL_CONTRACTS.md).
