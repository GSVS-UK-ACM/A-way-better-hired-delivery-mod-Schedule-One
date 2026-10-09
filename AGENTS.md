# Agent Instructions

This repository is the canonical project memory for Vehicle Handlers. Keep it usable without chat history.

## Before changing code

1. Read `docs/HANDOVER.md`, then the claimed item in `docs/BACKLOG.md`.
2. Read `docs/PRODUCT_SPEC.md`, `docs/ARCHITECTURE.md`, and `docs/IMPLEMENTATION_PLAN.md`.
3. Fetch `origin` and inspect active branches and pull requests for overlapping claims.
4. Confirm the exact Schedule I backend and version represented by the local reference assemblies.
5. Claim one backlog item on a task branch before implementation.

## Live task lock

- A task is claimed only when `docs/BACKLOG.md` names its owner and status on a pushed branch or draft pull request.
- Use one branch per backlog outcome: `<type>/vh-mN-NNN-short-description`.
- Do not edit another worker's claimed scope without an explicit split recorded in the backlog and handover.
- The coordinating worker owns shared-document conflict resolution.

## Safety and compatibility

- Never commit game assemblies, generated IL2CPP assemblies, MelonLoader binaries, logs, saves, credentials, local paths, or toolchains.
- Keep game references outside the repository and pass their root through `MelonLoaderRoot`.
- Treat IL2CPP and Mono as separate targets. Version 0.1 targets Schedule I 0.4.7f12 IL2CPP only.
- Resolve Harmony targets by exact type, method name, and parameter signature.
- A successful compile is not runtime verification.
- Never spawn a replacement vehicle when an assignment references an existing owned vehicle.

## Handover

Update `docs/HANDOVER.md` whenever status, architecture, setup, API evidence, verification, risks, or next work changes. Record exact commands and hashes. Do not publish a release or merge without owner approval.

