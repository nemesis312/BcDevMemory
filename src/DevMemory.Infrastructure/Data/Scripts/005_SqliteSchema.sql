-- DevMemory: SQLite Schema
-- All types stored as TEXT for UUIDs/dates, INTEGER for booleans, TEXT(JSON) for arrays.
-- Run once at startup — all statements are idempotent.

CREATE TABLE IF NOT EXISTS sessions (
    id          TEXT    PRIMARY KEY,
    project     TEXT,
    goal        TEXT,
    summary     TEXT,
    files_changed TEXT,  -- JSON array: ["path/a","path/b"]
    started_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    ended_at    TEXT,
    is_active   INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_sessions_project  ON sessions(project);
CREATE INDEX IF NOT EXISTS idx_sessions_active   ON sessions(is_active) WHERE is_active = 1;
CREATE INDEX IF NOT EXISTS idx_sessions_started  ON sessions(started_at DESC);

CREATE TABLE IF NOT EXISTS observations (
    id          TEXT PRIMARY KEY,
    session_id  TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    title       TEXT NOT NULL,
    type        TEXT NOT NULL,
    content     TEXT NOT NULL,
    project     TEXT,
    tags        TEXT,  -- JSON array: ["tag1","tag2"]
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE INDEX IF NOT EXISTS idx_observations_session ON observations(session_id);
CREATE INDEX IF NOT EXISTS idx_observations_project ON observations(project);
CREATE INDEX IF NOT EXISTS idx_observations_type    ON observations(type);
CREATE INDEX IF NOT EXISTS idx_observations_created ON observations(created_at DESC);

CREATE TABLE IF NOT EXISTS prompts (
    id          TEXT PRIMARY KEY,
    session_id  TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    content     TEXT NOT NULL,
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE INDEX IF NOT EXISTS idx_prompts_session ON prompts(session_id);

-- FTS5 virtual table for full-text search
CREATE VIRTUAL TABLE IF NOT EXISTS observations_fts USING fts5(
    observation_id UNINDEXED,
    title,
    content,
    project
);

-- Keep FTS in sync via triggers
CREATE TRIGGER IF NOT EXISTS trig_obs_fts_insert
AFTER INSERT ON observations
BEGIN
    INSERT INTO observations_fts(observation_id, title, content, project)
    VALUES (new.id, new.title, new.content, COALESCE(new.project, ''));
END;

CREATE TRIGGER IF NOT EXISTS trig_obs_fts_delete
AFTER DELETE ON observations
BEGIN
    DELETE FROM observations_fts WHERE observation_id = old.id;
END;

CREATE TRIGGER IF NOT EXISTS trig_obs_fts_update
AFTER UPDATE ON observations
BEGIN
    DELETE FROM observations_fts WHERE observation_id = old.id;
    INSERT INTO observations_fts(observation_id, title, content, project)
    VALUES (new.id, new.title, new.content, COALESCE(new.project, ''));
END;

-- bc-agentic-os: Run artifacts (PRD, design, plan, report, etc.)
-- Artifacts are immutable; re-saving creates a new version.
CREATE TABLE IF NOT EXISTS artifacts (
    id           TEXT    PRIMARY KEY,
    run_id       TEXT    NOT NULL,
    project_name TEXT    NOT NULL,
    type         TEXT    NOT NULL,
    content      TEXT    NOT NULL,
    version      INTEGER NOT NULL DEFAULT 1,
    metadata     TEXT,   -- JSON object: { "goal": "...", "agentRole": "...", ... }
    created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE INDEX IF NOT EXISTS idx_artifacts_run_id  ON artifacts(run_id);
CREATE INDEX IF NOT EXISTS idx_artifacts_project ON artifacts(project_name);
CREATE INDEX IF NOT EXISTS idx_artifacts_type    ON artifacts(type);
CREATE INDEX IF NOT EXISTS idx_artifacts_created ON artifacts(created_at DESC);

-- bc-agentic-os: Run state checkpoints
-- Upserted on each bc-agentic next / approve / handoff call.
-- mode tracks whether the run is in safe | apply | god mode.
CREATE TABLE IF NOT EXISTS run_states (
    run_id               TEXT PRIMARY KEY,
    project_name         TEXT NOT NULL,
    goal                 TEXT NOT NULL,
    current_phase        TEXT NOT NULL,
    approvals            TEXT NOT NULL DEFAULT '{}',  -- JSON: { "prd": true, "design": false, ... }
    last_handoff_summary TEXT,
    open_risks           TEXT NOT NULL DEFAULT '[]',  -- JSON array of strings
    mode                 TEXT NOT NULL DEFAULT 'safe',
    updated_at           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE INDEX IF NOT EXISTS idx_run_states_project ON run_states(project_name);
CREATE INDEX IF NOT EXISTS idx_run_states_updated ON run_states(updated_at DESC);
