# Sync Roadmap - Phase 1

Branch: `feat/sync-phase1-roadmap`

## Context

DevMemory will support an Engram-style Git sync flow for local SQLite users while keeping shared SSE + PostgreSQL deployments unchanged.

Decision for this implementation track:

- Use one global Git-backed memory repository.
- Sync target path is explicit and configurable (not tied to current project repo).
- Phase 1 focuses on foundations only (contracts, path resolution, basic status wiring).

## Phase 1 Goals

1. Define sync contracts and folder conventions for a global repo.
2. Add deterministic sync path resolution (`--sync-path` > env > config > default).
3. Add `sync --status` command skeleton for SQLite mode.
4. Leave chunk export/import execution for Phase 2.

## Scope (In)

- New sync models:
  - `Manifest`
  - `ChunkEntry`
  - `SyncStatus`
  - `SyncOptions`
- New sync interfaces:
  - `ISyncTransport`
  - `ISyncService`
- `FileSyncTransport` for filesystem-based manifest/chunk metadata reads.
- Sync path resolver with precedence:
  1) CLI `--sync-path`
  2) `DEVMEMORY_SYNC_PATH`
  3) `DevMemory:Sync:Path`
  4) default `~/.devmemory-sync`
- CLI command contract for `sync --status` and guardrails for non-SQLite providers.
- Initial docs for setup and expected directory structure.

## Scope (Out)

- Chunk creation/export delta.
- Chunk import + dedupe (`sync_chunks` table).
- Git commands automation.
- API/MCP sync tools.

## Proposed Directory Structure (Global Sync Repo)

```text
<sync-path>/
  manifest.json
  chunks/
  projects/
    <project-slug>/
      state.json
```

## Work Breakdown

1. Architecture + Contracts
   - Create sync domain models and interface definitions.
   - Define manifest schema versioning (`version: 1`).
2. Configuration + Resolution
   - Add sync path config keys to CLI/app settings binding.
   - Implement path expansion (`~`) and directory checks.
3. CLI Skeleton
   - Add `sync` command with `--status`, `--project`, `--all`, `--sync-path`.
   - For now, `--status` returns repository readiness + provider compatibility.
4. Documentation
   - Add a quickstart section for global sync repo usage.
   - Document Postgres/Neo4j behavior (`sync` unavailable in Phase 1).
5. Tests
   - Unit tests for path precedence and normalization.
   - Unit tests for manifest read behavior when file does not exist.

## Acceptance Criteria

- `devmemory-mcp sync --status` runs without throwing in SQLite mode.
- Sync path is resolved exactly by precedence rules.
- Missing sync repo is reported with actionable guidance.
- Non-SQLite storage returns clear message: sync chunks not enabled for this provider.
- Added tests cover path resolver and basic manifest loading behavior.

## Risks and Mitigations

- Risk: ambiguity between local DB path and sync repo path.
  - Mitigation: separate config names and explicit CLI output.
- Risk: users expect full sync after Phase 1.
  - Mitigation: command output marks status as "foundation only".
- Risk: path permissions on shared folders.
  - Mitigation: preflight checks in `--status` and clear error messages.

## Phase 1 Deliverables

- Sync contracts and filesystem transport skeleton.
- `sync --status` command integrated in CLI.
- Config/env support for global sync path.
- Docs update and baseline tests.

## Next Phase Preview (Phase 2)

- Implement chunk generation and manifest updates.
- Add SQLite `sync_chunks` table and idempotent import.
- Support `sync` (export) and `sync --import` with deterministic ordering.
