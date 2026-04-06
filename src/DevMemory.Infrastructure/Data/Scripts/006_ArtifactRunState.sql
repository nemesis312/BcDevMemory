-- DevMemory: bc-agentic-os Artifact and Run State tables
-- Idempotent (IF NOT EXISTS / CREATE OR REPLACE).
-- Run after 004_EnhancedSearch.sql

-- ─────────────────────────────────────────────
-- artifacts
-- Stores bc-agentic-os run artifacts (prd, design, plan, report, etc.)
-- Artifacts are immutable: re-saving increments version.
-- run_id is a bc-agentic string slug (not a UUID).
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS artifacts (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id       VARCHAR(200) NOT NULL,
    project_name VARCHAR(200) NOT NULL,
    type         VARCHAR(100) NOT NULL,
    content      TEXT         NOT NULL,
    version      INTEGER      NOT NULL DEFAULT 1,
    metadata     JSONB,
    created_at   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_artifacts_run_id  ON artifacts(run_id);
CREATE INDEX IF NOT EXISTS idx_artifacts_project ON artifacts(project_name);
CREATE INDEX IF NOT EXISTS idx_artifacts_type    ON artifacts(type);
CREATE INDEX IF NOT EXISTS idx_artifacts_created ON artifacts(created_at DESC);

-- ─────────────────────────────────────────────
-- run_states
-- Stores the live state of bc-agentic-os runs.
-- Upserted on every bc-agentic checkpoint event.
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS run_states (
    run_id               VARCHAR(200) PRIMARY KEY,
    project_name         VARCHAR(200) NOT NULL,
    goal                 TEXT         NOT NULL,
    current_phase        VARCHAR(100) NOT NULL,
    approvals            JSONB        NOT NULL DEFAULT '{}',
    last_handoff_summary TEXT,
    open_risks           TEXT[]       NOT NULL DEFAULT '{}',
    mode                 VARCHAR(50)  NOT NULL DEFAULT 'safe',
    updated_at           TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_run_states_project ON run_states(project_name);
CREATE INDEX IF NOT EXISTS idx_run_states_updated ON run_states(updated_at DESC);

-- ─────────────────────────────────────────────
-- sp_save_artifact
-- Inserts a new artifact version and returns the full row.
-- Version is auto-incremented per run_id + type.
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_save_artifact(
    p_run_id       VARCHAR(200),
    p_project_name VARCHAR(200),
    p_type         VARCHAR(100),
    p_content      TEXT,
    p_metadata     JSONB DEFAULT NULL
)
RETURNS TABLE(
    id           UUID,
    run_id       VARCHAR(200),
    project_name VARCHAR(200),
    type         VARCHAR(100),
    content      TEXT,
    version      INTEGER,
    metadata     JSONB,
    created_at   TIMESTAMP WITH TIME ZONE
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_version INTEGER;
BEGIN
    SELECT COALESCE(MAX(a.version), 0) + 1
      INTO v_version
      FROM artifacts a
     WHERE a.run_id = p_run_id AND a.type = p_type;

    RETURN QUERY
    INSERT INTO artifacts (run_id, project_name, type, content, version, metadata)
    VALUES (p_run_id, p_project_name, p_type, p_content, v_version, p_metadata)
    RETURNING
        artifacts.id,
        artifacts.run_id,
        artifacts.project_name,
        artifacts.type,
        artifacts.content,
        artifacts.version,
        artifacts.metadata,
        artifacts.created_at;
END;
$$;

-- ─────────────────────────────────────────────
-- sp_upsert_run_state
-- Inserts or updates the run state checkpoint.
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_upsert_run_state(
    p_run_id               VARCHAR(200),
    p_project_name         VARCHAR(200),
    p_goal                 TEXT,
    p_current_phase        VARCHAR(100),
    p_approvals            JSONB,
    p_last_handoff_summary TEXT,
    p_open_risks           TEXT[],
    p_mode                 VARCHAR(50)
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO run_states
        (run_id, project_name, goal, current_phase, approvals, last_handoff_summary, open_risks, mode, updated_at)
    VALUES
        (p_run_id, p_project_name, p_goal, p_current_phase, p_approvals, p_last_handoff_summary, p_open_risks, p_mode, NOW())
    ON CONFLICT (run_id) DO UPDATE SET
        project_name         = EXCLUDED.project_name,
        goal                 = EXCLUDED.goal,
        current_phase        = EXCLUDED.current_phase,
        approvals            = EXCLUDED.approvals,
        last_handoff_summary = EXCLUDED.last_handoff_summary,
        open_risks           = EXCLUDED.open_risks,
        mode                 = EXCLUDED.mode,
        updated_at           = NOW();
END;
$$;
