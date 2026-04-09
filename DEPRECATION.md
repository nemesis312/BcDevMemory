# Deprecation Notice — `.NET BcDevMemory`

Status: **Deprecated (controlled Phase 5)**  
Effective date: **2026-04-09**

## Strategic backend

The recommended and actively operated backend for `bc-agentic` workflows is now:

- **Go implementation:** `https://github.com/nemesis312/bc-dev-memory`

Use `bc-dev-memory` for all new setup and production-like workflows.

## Legacy policy (this repo)

This repository is now in **freeze mode**:

- ✅ Allowed:
  - critical bug fixes
  - security/compliance patches
  - migration-support documentation clarifications
- 🚫 Not allowed by default:
  - new features
  - non-essential refactors
  - expansion of legacy runtime surface

## Exception path

If a change is required outside the allowed set:

1. Open an issue with impact and urgency.
2. Link affected `bc-agentic` workflow and mitigation if not approved.
3. Obtain maintainer approval before implementation.

## Archival policy

Archival is **not immediate**. It is time-gated by stabilization criteria:

- observation window: **30–60 days** after official Go cutover completion
- no active runtime dependency for `bc-agentic` workflows
- readiness evidence reviewed and approved

Until these criteria are met, this repo remains available as deprecated legacy source.
