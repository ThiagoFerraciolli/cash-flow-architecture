\set ON_ERROR_STOP on
BEGIN;
SELECT pg_advisory_xact_lock(710031003);
CREATE TABLE IF NOT EXISTS local_schema_migrations (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
SELECT (to_regclass('public.projection_control') IS NULL)::text AS apply_001 \gset
\if :apply_001
\i /migrations/001_consolidation.sql
\endif
INSERT INTO local_schema_migrations (version) VALUES (1) ON CONFLICT DO NOTHING;
SELECT (NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'projection_control'
      AND column_name = 'maintenance_expires_at'
))::text AS apply_002 \gset
\if :apply_002
\i /migrations/002_recovery_lease.sql
\endif
INSERT INTO local_schema_migrations (version) VALUES (2) ON CONFLICT DO NOTHING;
COMMIT;
