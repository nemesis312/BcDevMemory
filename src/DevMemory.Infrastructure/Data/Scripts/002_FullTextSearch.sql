-- DevMemory: Full-Text Search column + GIN index
-- Run after 001_InitialSchema.sql

-- Add the generated tsvector column for weighted FTS
-- (title = A, content = B, project = C)
ALTER TABLE observations
    ADD COLUMN IF NOT EXISTS search_vector tsvector
        GENERATED ALWAYS AS (
            setweight(to_tsvector('english', coalesce(title, '')),   'A') ||
            setweight(to_tsvector('english', coalesce(content, '')), 'B') ||
            setweight(to_tsvector('english', coalesce(project, '')), 'C')
        ) STORED;

CREATE INDEX IF NOT EXISTS idx_observations_search ON observations USING GIN(search_vector);
