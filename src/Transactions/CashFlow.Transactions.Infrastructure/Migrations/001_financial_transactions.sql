CREATE TABLE financial_transactions (
    id uuid PRIMARY KEY,
    creation_event_id uuid NOT NULL UNIQUE,
    type text NOT NULL CHECK (type IN ('CREDIT', 'DEBIT')),
    amount numeric(14,2) NOT NULL CHECK (amount BETWEEN 0.01 AND 999999999.99),
    description text NULL CHECK (char_length(description) <= 500),
    occurred_at timestamptz NOT NULL,
    business_date date NOT NULL,
    created_at timestamptz NOT NULL,
    actor_issuer text NULL,
    actor_client_id text NULL
);
CREATE INDEX ix_financial_transactions_date_id ON financial_transactions (business_date, id);
CREATE INDEX ix_financial_transactions_type_date_id ON financial_transactions (type, business_date, id);
