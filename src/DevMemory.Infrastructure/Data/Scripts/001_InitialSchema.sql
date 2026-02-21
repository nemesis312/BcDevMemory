-- DevMemory: Initial Schema
-- Run this first against a fresh PostgreSQL database.

CREATE TABLE IF NOT EXISTS sessions (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project      VARCHAR(200),
    goal         TEXT,
    summary      TEXT,
    files_changed TEXT[],
    started_at   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ended_at     TIMESTAMP WITH TIME ZONE,
    is_active    BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_sessions_project ON sessions(project);
CREATE INDEX IF NOT EXISTS idx_sessions_active  ON sessions(is_active) WHERE is_active = TRUE;
CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions(started_at DESC);

CREATE TABLE IF NOT EXISTS observations (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id   UUID NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    title        VARCHAR(500) NOT NULL,
    type         VARCHAR(100) NOT NULL,
    content      TEXT NOT NULL,
    project      VARCHAR(200),
    tags         TEXT[],
    created_at   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_observations_session ON observations(session_id);
CREATE INDEX IF NOT EXISTS idx_observations_project ON observations(project);
CREATE INDEX IF NOT EXISTS idx_observations_type    ON observations(type);
CREATE INDEX IF NOT EXISTS idx_observations_created ON observations(created_at DESC);

CREATE TABLE IF NOT EXISTS prompts (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    content    TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_prompts_session ON prompts(session_id);
