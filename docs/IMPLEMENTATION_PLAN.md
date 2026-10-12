# Approved Implementation Plan

Status: Approved  
Target: Schedule I 0.4.7f12 IL2CPP

## Milestones

1. **M0 Repository foundation** — canonical documentation, contributor workflow, project scaffold, and safety rules.
2. **M1 Compatibility layer** — exact game API verification and frozen internal contracts.
3. **M2 Handler employee** — distinct runtime employee, hiring, lifecycle, and appearance.
4. **M3 Configuration and persistence** — one-vehicle assignment, destination selectors, controls, and versioned save data.
5. **M4 Movement and reservations** — timed physical vehicle transfer and shared loading-bay availability.
6. **M5 Verification and release** — isolated/full-set runtime testing and distributable package.

## Parallel work boundary

M2, M3, and M4 may proceed in parallel only after M1 freezes identifiers, state transitions, validation results, and reservation ownership. Shared documents are coordinated through the handover to avoid conflicting edits.

## Release gates

- Exact API verification passes before compilation.
- Release build has zero warnings and errors.
- No duplicate vehicle is created in any tested state.
- Store deliveries and Handler reservations mutually respect loading-bay availability.
- Save/reload, firing, despawn, and interruption leave no stale reservations.
- Runtime evidence is recorded first with only Vehicle Handlers enabled, then with the normal mod set.

