#!/bin/sh
set -eu

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from .env.example (local-only credentials)."
fi

set -a
# shellcheck disable=SC1091
. ./.env
set +a

: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required in .env}"
: "${CONSOLIDATION_POSTGRES_PASSWORD:?CONSOLIDATION_POSTGRES_PASSWORD is required in .env}"
: "${RABBITMQ_PASSWORD:?RABBITMQ_PASSWORD is required in .env}"
: "${KEYCLOAK_ADMIN_PASSWORD:?KEYCLOAK_ADMIN_PASSWORD is required in .env}"
: "${DEMO_CLIENT_SECRET:?DEMO_CLIENT_SECRET is required in .env}"
: "${READER_CLIENT_SECRET:?READER_CLIENT_SECRET is required in .env}"

export CASHFLOW_TEST_POSTGRES="Host=127.0.0.1;Port=5433;Database=cashflow;Username=cashflow;Password=${POSTGRES_PASSWORD}"
export CASHFLOW_TEST_CONSOLIDATION_POSTGRES="Host=127.0.0.1;Port=5434;Database=consolidation;Username=consolidation;Password=${CONSOLIDATION_POSTGRES_PASSWORD}"
export CASHFLOW_TEST_RABBITMQ="amqp://cashflow:${RABBITMQ_PASSWORD}@127.0.0.1:5672/"
export CASHFLOW_E2E_IDENTITY_URL="http://127.0.0.1:8080"
export CASHFLOW_E2E_TRANSACTIONS_URL="http://127.0.0.1:8081"
export CASHFLOW_E2E_CONSOLIDATION_URL="http://127.0.0.1:8082"

echo "==> Starting the complete local topology"
docker compose up -d --build --wait

echo "==> Verifying API readiness"
curl --fail --silent --show-error http://127.0.0.1:8081/health/ready >/dev/null
curl --fail --silent --show-error http://127.0.0.1:8082/health/ready >/dev/null

echo "==> Restoring locked dependencies"
dotnet restore --locked-mode

echo "==> Building"
dotnet build --no-restore

echo "==> Running unit, contract, integration and Compose E2E tests"
dotnet test --no-build

echo "==> Local validation completed successfully"
