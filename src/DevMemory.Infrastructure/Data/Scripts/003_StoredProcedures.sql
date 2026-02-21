-- DevMemory: Stored Procedures
-- Run after 002_FullTextSearch.sql

-- ─────────────────────────────────────────────
-- sp_save_observation
-- Returns the full inserted row so the caller can map directly.
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_save_observation(
    p_session_id UUID,
    p_title      VARCHAR(500),
    p_type       VARCHAR(100),
    p_content    TEXT,
    p_project    VARCHAR(200),
    p_tags       TEXT[]
)
RETURNS TABLE(
    id         UUID,
    session_id UUID,
    title      VARCHAR(500),
    type       VARCHAR(100),
    content    TEXT,
    project    VARCHAR(200),
    tags       TEXT[],
    created_at TIMESTAMP WITH TIME ZONE
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    INSERT INTO observations (session_id, title, type, content, project, tags)
    VALUES (p_session_id, p_title, p_type, p_content, p_project, p_tags)
    RETURNING
        observations.id,
        observations.session_id,
        observations.title,
        observations.type,
        observations.content,
        observations.project,
        observations.tags,
        observations.created_at;
END;
$$;

-- ─────────────────────────────────────────────
-- sp_search_observations
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_search_observations(
    p_query TEXT,
    p_project VARCHAR DEFAULT NULL,
    p_limit INT DEFAULT 10
)
RETURNS TABLE(
    id UUID,
    session_id UUID,
    title VARCHAR,
    type VARCHAR,
    content_preview TEXT,
    project VARCHAR,
    rank REAL,
    created_at TIMESTAMP WITH TIME ZONE
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
        LEFT(o.content, 200) AS content_preview,
        o.project,
        ts_rank(o.search_vector, plainto_tsquery('english', p_query)) AS rank,
        o.created_at
    FROM observations o
    WHERE o.search_vector @@ plainto_tsquery('english', p_query)
      AND (p_project IS NULL OR o.project = p_project)
    ORDER BY rank DESC, o.created_at DESC
    LIMIT p_limit;
END;
$$;

-- ─────────────────────────────────────────────
-- sp_get_session_context
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_get_session_context(
    p_project VARCHAR(200),
    p_limit   INT DEFAULT 5
)
RETURNS TABLE(
    session_id          UUID,
    session_goal        TEXT,
    session_summary     TEXT,
    started_at          TIMESTAMP WITH TIME ZONE,
    ended_at            TIMESTAMP WITH TIME ZONE,
    observation_count   BIGINT,
    recent_observations JSONB
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        s.id                                                        AS session_id,
        s.goal                                                      AS session_goal,
        s.summary                                                   AS session_summary,
        s.started_at,
        s.ended_at,
        COUNT(o.id)                                                 AS observation_count,
        JSONB_AGG(
            JSONB_BUILD_OBJECT(
                'id',         o.id,
                'title',      o.title,
                'type',       o.type,
                'created_at', o.created_at
            ) ORDER BY o.created_at DESC
        )                                                           AS recent_observations
    FROM sessions s
    LEFT JOIN observations o ON o.session_id = s.id
    WHERE s.project = p_project
      AND s.is_active = FALSE
    GROUP BY s.id
    ORDER BY s.started_at DESC
    LIMIT p_limit;
END;
$$;

-- ─────────────────────────────────────────────
-- sp_get_timeline
-- Fetch observations before and after a given observation (by created_at).
-- Uses subqueries so LIMIT applies per branch before UNION ALL.
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_get_timeline(
    p_observation_id UUID,
    p_before         INT DEFAULT 3,
    p_after          INT DEFAULT 3
)
RETURNS TABLE(
    id         UUID,
    session_id UUID,
    title      VARCHAR(500),
    type       VARCHAR(100),
    content    TEXT,
    project    VARCHAR(200),
    tags       TEXT[],
    created_at TIMESTAMP WITH TIME ZONE,
    timeline_pos TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_anchor_time TIMESTAMP WITH TIME ZONE;
    v_project     VARCHAR(200);
BEGIN
    SELECT o.created_at, o.project
      INTO v_anchor_time, v_project
      FROM observations o
     WHERE o.id = p_observation_id;

    RETURN QUERY
    SELECT combined.id, combined.session_id, combined.title, combined.type,
           combined.content, combined.project, combined.tags,
           combined.created_at, combined.timeline_pos
    FROM (
        -- anchor
        SELECT o.id, o.session_id, o.title, o.type, o.content, o.project,
               o.tags, o.created_at, 'anchor'::TEXT AS timeline_pos
          FROM observations o
         WHERE o.id = p_observation_id

        UNION ALL

        -- before: nearest N observations preceding the anchor
        SELECT b.id, b.session_id, b.title, b.type, b.content, b.project,
               b.tags, b.created_at, 'before'::TEXT AS timeline_pos
          FROM (
            SELECT obs.* FROM observations obs
             WHERE obs.project = v_project
               AND obs.created_at < v_anchor_time
               AND obs.id <> p_observation_id
             ORDER BY obs.created_at DESC
             LIMIT p_before
          ) b

        UNION ALL

        -- after: nearest N observations following the anchor
        SELECT a.id, a.session_id, a.title, a.type, a.content, a.project,
               a.tags, a.created_at, 'after'::TEXT AS timeline_pos
          FROM (
            SELECT obs.* FROM observations obs
             WHERE obs.project = v_project
               AND obs.created_at > v_anchor_time
               AND obs.id <> p_observation_id
             ORDER BY obs.created_at ASC
             LIMIT p_after
          ) a
    ) combined
    ORDER BY combined.created_at;
END;
$$;

-- ─────────────────────────────────────────────
-- sp_get_stats
-- ─────────────────────────────────────────────
CREATE OR REPLACE FUNCTION sp_get_stats()
RETURNS TABLE(
    total_observations BIGINT,
    total_sessions     BIGINT,
    active_sessions    BIGINT,
    projects           BIGINT,
    oldest_observation TIMESTAMP WITH TIME ZONE,
    newest_observation TIMESTAMP WITH TIME ZONE
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT COUNT(*)           FROM observations)                          AS total_observations,
        (SELECT COUNT(*)           FROM sessions)                             AS total_sessions,
        (SELECT COUNT(*)           FROM sessions WHERE is_active = TRUE)      AS active_sessions,
        (SELECT COUNT(DISTINCT project) FROM observations WHERE project IS NOT NULL) AS projects,
        (SELECT MIN(created_at)    FROM observations)                         AS oldest_observation,
        (SELECT MAX(created_at)    FROM observations)                         AS newest_observation;
END;
$$;