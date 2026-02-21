-- DevMemory: Enhanced Search Stored Procedure
-- Replaces sp_search_observations to add type and tags filters.
-- Idempotent (CREATE OR REPLACE).

-- Drop the old 3-parameter version from 003 to avoid ambiguous function calls
DROP FUNCTION IF EXISTS sp_search_observations (TEXT, VARCHAR, INT);

CREATE OR REPLACE FUNCTION sp_search_observations(
    p_query   TEXT,
    p_project VARCHAR(200) DEFAULT NULL,
    p_limit   INT          DEFAULT 10,
    p_type    VARCHAR(100) DEFAULT NULL,
    p_tags    TEXT[]       DEFAULT NULL
)
RETURNS TABLE(
    id              UUID,
    session_id      UUID,
    title           VARCHAR(500),
    type            VARCHAR(100),
    content_preview TEXT,
    project         VARCHAR(200),
    rank            REAL,
    created_at      TIMESTAMP WITH TIME ZONE
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.id,
        o.session_id,
        o.title,
        o.type,
        LEFT(o.content, 200)                                                AS content_preview,
        o.project,
        ts_rank(o.search_vector, websearch_to_tsquery('english', p_query))  AS rank,
        o.created_at
    FROM observations o
    WHERE o.search_vector @@ websearch_to_tsquery('english', p_query)
      AND (p_project IS NULL OR o.project = p_project)
      AND (p_type    IS NULL OR o.type    = p_type)
      AND (p_tags    IS NULL OR o.tags    @> p_tags)
    ORDER BY rank DESC, o.created_at DESC
    LIMIT p_limit;
END;
$$;