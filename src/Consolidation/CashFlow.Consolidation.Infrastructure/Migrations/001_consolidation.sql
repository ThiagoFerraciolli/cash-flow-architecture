CREATE TABLE projection_generations (
    generation_id uuid PRIMARY KEY,
    status text NOT NULL CHECK (status IN ('BUILDING', 'ACTIVE', 'INACTIVE')),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    activated_at timestamptz,
    snapshot_manifest jsonb
);

CREATE TABLE projection_control (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    active_generation_id uuid NOT NULL REFERENCES projection_generations(generation_id),
    maintenance boolean NOT NULL DEFAULT false,
    maintenance_run_id uuid,
    maintenance_started_at timestamptz
);

CREATE TABLE daily_consolidations (
    generation_id uuid NOT NULL REFERENCES projection_generations(generation_id),
    business_date date NOT NULL,
    total_credits numeric(28,2) NOT NULL DEFAULT 0 CHECK (total_credits >= 0),
    total_debits numeric(28,2) NOT NULL DEFAULT 0 CHECK (total_debits >= 0),
    transaction_count bigint NOT NULL DEFAULT 0 CHECK (transaction_count >= 0),
    last_updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (generation_id, business_date)
);

CREATE TABLE processed_transactions (
    generation_id uuid NOT NULL REFERENCES projection_generations(generation_id),
    transaction_id uuid NOT NULL,
    event_id uuid NOT NULL,
    business_date date NOT NULL,
    type text NOT NULL CHECK (type IN ('CREDIT', 'DEBIT')),
    amount numeric(14,2) NOT NULL CHECK (amount BETWEEN 0.01 AND 999999999.99),
    fact_hash text NOT NULL,
    processed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (generation_id, transaction_id),
    UNIQUE (generation_id, event_id)
);
CREATE INDEX ix_processed_transactions_date ON processed_transactions (generation_id, business_date);

WITH initial AS (
    INSERT INTO projection_generations (generation_id, status, activated_at)
    VALUES (gen_random_uuid(), 'ACTIVE', clock_timestamp())
    RETURNING generation_id
)
INSERT INTO projection_control (singleton, active_generation_id, maintenance)
SELECT true, generation_id, false FROM initial;
