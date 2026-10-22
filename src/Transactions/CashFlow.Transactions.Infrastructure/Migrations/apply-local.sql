\set ON_ERROR_STOP on
BEGIN;
SELECT pg_advisory_xact_lock(710031002);
CREATE TABLE IF NOT EXISTS local_schema_migrations (version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());

SELECT (to_regclass('public.financial_transactions') IS NULL)::text AS apply_001 \gset
\if :apply_001
\i /migrations/001_financial_transactions.sql
\endif
INSERT INTO local_schema_migrations (version) VALUES (1) ON CONFLICT DO NOTHING;

SELECT (to_regclass('public.idempotency_records') IS NULL OR to_regclass('public.outbox_messages') IS NULL)::text AS apply_002 \gset
\if :apply_002
\i /migrations/002_idempotency_outbox.sql
\endif
INSERT INTO local_schema_migrations (version) VALUES (2) ON CONFLICT DO NOTHING;
COMMIT;
