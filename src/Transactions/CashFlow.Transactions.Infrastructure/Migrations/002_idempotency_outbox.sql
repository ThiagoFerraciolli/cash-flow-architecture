CREATE UNIQUE INDEX ux_financial_transactions_id_event ON financial_transactions (id, creation_event_id);

CREATE TABLE idempotency_records (
    issuer text NOT NULL,
    client_id text NOT NULL,
    idempotency_key uuid NOT NULL,
    request_hash char(64) NOT NULL,
    canonical_version integer NOT NULL CHECK (canonical_version > 0),
    transaction_id uuid NOT NULL UNIQUE,
    response_status integer NOT NULL CHECK (response_status = 201),
    response_body jsonb NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (issuer, client_id, idempotency_key),
    CONSTRAINT fk_idempotency_transaction FOREIGN KEY (transaction_id)
        REFERENCES financial_transactions (id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE outbox_messages (
    event_id uuid PRIMARY KEY,
    aggregate_id uuid NOT NULL,
    payload jsonb NOT NULL,
    headers jsonb NOT NULL DEFAULT '{}'::jsonb,
    state text NOT NULL CHECK (state IN ('PENDING', 'PUBLISHED', 'SUSPENDED')),
    created_at timestamptz NOT NULL,
    published_at timestamptz NULL,
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at timestamptz NOT NULL,
    lease_token uuid NULL,
    lease_until timestamptz NULL,
    last_error text NULL,
    CONSTRAINT fk_outbox_fact FOREIGN KEY (aggregate_id, event_id)
        REFERENCES financial_transactions (id, creation_event_id)
);

CREATE INDEX ix_outbox_pending ON outbox_messages (next_attempt_at, lease_until)
    WHERE state = 'PENDING';
