#!/usr/bin/env python3
"""Prepare T15 through HTTP, then run separate k6 warm-up and measured windows."""

import json
import os
from pathlib import Path
import secrets
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import date, timedelta


ROOT = Path(__file__).resolve().parents[2]
IDENTITY = os.environ.get("CASHFLOW_E2E_IDENTITY_URL", "http://127.0.0.1:8080")
TRANSACTIONS = os.environ.get("CASHFLOW_E2E_TRANSACTIONS_URL", "http://127.0.0.1:8081")
CONSOLIDATION = os.environ.get("CASHFLOW_E2E_CONSOLIDATION_URL", "http://127.0.0.1:8082")
SERVICES = (
    "identity", "postgres", "transactions-api", "transactions-worker",
    "rabbitmq", "postgres-consolidation", "consolidation-worker",
    "consolidation-api",
)


def request(url, token=None, *, method="GET", body=None, headers=None):
    request_headers = dict(headers or {})
    if token:
        request_headers["Authorization"] = f"Bearer {token}"
    payload = json.dumps(body).encode() if body is not None else None
    if payload is not None:
        request_headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=payload, headers=request_headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def token(client, secret):
    if not secret:
        raise RuntimeError(f"Set the {client} secret from the ignored .env file.")
    req = urllib.request.Request(
        f"{IDENTITY}/realms/cashflow/protocol/openid-connect/token",
        data=urllib.parse.urlencode({
            "grant_type": "client_credentials",
            "client_id": client,
            "client_secret": secret,
        }).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=10) as response:
        value = json.load(response)["access_token"]
        print(f"TOKEN {client}: HTTP {response.status}", flush=True)
        return value


def assert_healthy():
    result = subprocess.run(
        ["docker", "compose", "ps", "--format", "json"], cwd=ROOT,
        capture_output=True, text=True, check=True,
    )
    rows = {row["Service"]: row for line in result.stdout.splitlines()
            if (row := json.loads(line))}
    for name in SERVICES:
        row = rows.get(name)
        if not row or row["State"] != "running" or row["Health"] != "healthy":
            raise AssertionError(f"Compose service {name} is not healthy: {row}")
    print("COMPOSE: 8 required services healthy", flush=True)


def isolated_date(jwt):
    first = date(1900, 1, 1)
    for _ in range(20):
        day = (first + timedelta(days=secrets.randbelow(36_525))).isoformat()
        tx_status, tx = request(f"{TRANSACTIONS}/transactions?from={day}&to={day}", jwt)
        co_status, co = request(f"{CONSOLIDATION}/consolidations/{day}", jwt)
        if tx_status != 200 or co_status != 200:
            raise AssertionError(f"Isolation check failed: Transactions {tx_status}, Consolidation {co_status}")
        if not tx["items"] and co["transactionCount"] == 0:
            return day
    raise AssertionError("Could not find an isolated business date in 20 attempts.")


def create(jwt, day, kind, amount):
    status, body = request(
        f"{TRANSACTIONS}/transactions", jwt, method="POST",
        headers={"Idempotency-Key": str(uuid.uuid4()),
                 "X-Correlation-Id": str(uuid.uuid4())},
        body={"type": kind, "amount": amount,
              "occurredAt": f"{day}T12:00:00-03:00", "description": "T15 static load data"},
    )
    print(f"PREPARE {kind} {amount}: HTTP {status}", flush=True)
    if status != 201 or body.get("businessDate") != day:
        raise AssertionError(f"Transaction preparation failed: {status}: {body}")


def converged(body, day):
    return (body.get("businessDate") == day and body.get("currency") == "BRL"
            and body.get("totalCredits") == "100.00"
            and body.get("totalDebits") == "20.00"
            and body.get("balance") == "80.00"
            and body.get("transactionCount") == 2
            and body.get("consistency") == "EVENTUAL"
            and body.get("projectionState") == "AVAILABLE"
            and body.get("completeness") == "UNKNOWN")


def await_projection(jwt, day):
    deadline = time.monotonic() + 60
    last = None
    while time.monotonic() < deadline:
        status, body = request(f"{CONSOLIDATION}/consolidations/{day}", jwt)
        last = (status, body)
        if status == 200 and isinstance(body, dict) and converged(body, day):
            print(f"STATIC MASS READY: businessDate={day}, credits=100.00, debits=20.00, "
                  "balance=80.00, count=2", flush=True)
            return
        time.sleep(0.25)
    raise AssertionError(f"Projection did not converge before load test: {last}")


def k6(phase, jwt, day):
    env = dict(os.environ)
    env.update(T15_PHASE=phase, T15_TOKEN=jwt, T15_BUSINESS_DATE=day,
               T15_BASE_URL="http://consolidation-api:8080")
    command = [
        "docker", "run", "--rm", "--network", "cash-flow-architecture_default",
        "-e", "T15_PHASE", "-e", "T15_TOKEN", "-e", "T15_BUSINESS_DATE",
        "-e", "T15_BASE_URL", "-v",
        f"{ROOT / 'tests/performance/t15_consolidation_50rps.js'}:/work/t15.js:ro",
        "grafana/k6:1.5.0", "run", "/work/t15.js",
    ]
    print(f"K6 {phase}: {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=ROOT, env=env, check=True)


def main():
    assert_healthy()
    demo = token("cashflow-demo", os.environ.get("DEMO_CLIENT_SECRET"))
    day = isolated_date(demo)
    print(f"ISOLATED businessDate={day}", flush=True)
    create(demo, day, "CREDIT", "100.00")
    create(demo, day, "DEBIT", "20.00")
    reader = token("cashflow-reader", os.environ.get("READER_CLIENT_SECRET"))
    await_projection(reader, day)
    k6("warmup", reader, day)
    k6("measured", reader, day)


if __name__ == "__main__":
    main()
