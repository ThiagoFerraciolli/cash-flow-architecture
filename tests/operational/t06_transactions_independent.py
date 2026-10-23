#!/usr/bin/env python3
"""Run T06 against the real local Compose stack.

Usage: export DEMO_CLIENT_SECRET from the ignored .env, then run this script.
It reads business data only through the HTTP APIs and restores stopped services
even when an assertion fails.
"""

import json
import os
from pathlib import Path
from datetime import date, timedelta
import secrets
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[2]
IDENTITY = os.environ.get("CASHFLOW_E2E_IDENTITY_URL", "http://127.0.0.1:8080")
TRANSACTIONS = os.environ.get("CASHFLOW_E2E_TRANSACTIONS_URL", "http://127.0.0.1:8081")
CONSOLIDATION = os.environ.get("CASHFLOW_E2E_CONSOLIDATION_URL", "http://127.0.0.1:8082")
STOPPED = ("consolidation-api", "consolidation-worker", "postgres-consolidation")
REQUIRED = (
    "identity", "transactions-api", "transactions-worker", "postgres",
    "rabbitmq", *STOPPED,
)


def compose(*args):
    result = subprocess.run(
        ["docker", "compose", *args], cwd=ROOT, text=True,
        capture_output=True, check=False,
    )
    if result.returncode:
        raise RuntimeError(
            f"docker compose {' '.join(args)} failed ({result.returncode}): "
            f"{result.stderr.strip()}"
        )
    return result.stdout


def services():
    return {
        row["Service"]: row
        for line in compose("ps", "--all", "--format", "json").splitlines()
        if (row := json.loads(line))
    }


def wait_healthy(service, seconds=90):
    deadline = time.monotonic() + seconds
    last = "missing"
    while time.monotonic() < deadline:
        row = services().get(service)
        last = "missing" if row is None else f"{row['State']}/{row['Health']}"
        if row and row["State"] == "running" and row["Health"] == "healthy":
            print(f"RESTORED {service}: {last}", flush=True)
            return
        time.sleep(0.5)
    raise AssertionError(f"{service} did not become healthy within {seconds}s: {last}")


def http(url, *, token=None, method="GET", body=None, headers=None):
    request_headers = dict(headers or {})
    if token:
        request_headers["Authorization"] = f"Bearer {token}"
    data = json.dumps(body).encode() if body is not None else None
    if data is not None:
        request_headers["Content-Type"] = "application/json"
    request = urllib.request.Request(
        url, data=data, headers=request_headers, method=method,
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read().decode()
            return response.status, json.loads(raw) if raw.startswith("{") else raw
    except urllib.error.HTTPError as error:
        raw = error.read().decode()
        return error.code, json.loads(raw) if raw.startswith("{") else raw


def expect_status(label, url, expected, *, token=None):
    status, body = http(url, token=token)
    print(f"{label}: HTTP {status}", flush=True)
    if status != expected:
        raise AssertionError(f"{label}: expected HTTP {expected}, got {status}: {body}")
    return body


def token():
    secret = os.environ.get("DEMO_CLIENT_SECRET")
    if not secret:
        raise RuntimeError("Set DEMO_CLIENT_SECRET from the ignored local .env file.")
    request = urllib.request.Request(
        f"{IDENTITY}/realms/cashflow/protocol/openid-connect/token",
        data=urllib.parse.urlencode({
            "grant_type": "client_credentials",
            "client_id": "cashflow-demo",
            "client_secret": secret,
        }).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=10) as response:
        if response.status != 200:
            raise AssertionError(f"Keycloak returned HTTP {response.status}")
        value = json.load(response)["access_token"]
    print("KEYCLOAK cashflow-demo: HTTP 200", flush=True)
    return value


def isolated_date(jwt):
    first = date(1900, 1, 1)
    for _ in range(20):
        day = first + timedelta(days=secrets.randbelow(36_525))
        value = day.isoformat()
        status, transaction_page = http(
            f"{TRANSACTIONS}/transactions?from={value}&to={value}", token=jwt,
        )
        if status != 200:
            raise AssertionError(f"Transactions date check: HTTP {status}: {transaction_page}")
        if transaction_page["items"]:
            continue
        status, projection = http(f"{CONSOLIDATION}/consolidations/{value}", token=jwt)
        if status != 200:
            raise AssertionError(f"Consolidation date check: HTTP {status}: {projection}")
        if projection["transactionCount"] == 0:
            print(f"ISOLATED businessDate={value}", flush=True)
            return value
    raise AssertionError("No empty business date found in 20 candidates.")


def create(jwt, day, kind, amount):
    status, result = http(
        f"{TRANSACTIONS}/transactions", token=jwt, method="POST",
        headers={
            "Idempotency-Key": str(uuid.uuid4()),
            "X-Correlation-Id": str(uuid.uuid4()),
        },
        body={
            "type": kind,
            "amount": amount,
            "occurredAt": f"{day}T12:00:00-03:00",
            "description": "T06 consolidation outage",
        },
    )
    print(f"POST {kind} {amount} DURING_OUTAGE: HTTP {status}", flush=True)
    if status != 201 or result.get("businessDate") != day:
        raise AssertionError(f"POST {kind} failed: HTTP {status}: {result}")
    return result["id"]


def wait_projection(jwt, day, seconds=60):
    deadline = time.monotonic() + seconds
    last = "no response"
    while time.monotonic() < deadline:
        status, body = http(f"{CONSOLIDATION}/consolidations/{day}", token=jwt)
        last = f"HTTP {status}: {body}"
        if status != 200:
            raise AssertionError(f"Consolidation query after recovery failed: {last}")
        if body["transactionCount"] == 2:
            expected = ("100.00", "20.00", "80.00", 2)
            actual = (
                body["totalCredits"], body["totalDebits"],
                body["balance"], body["transactionCount"],
            )
            if actual != expected:
                raise AssertionError(f"Wrong final projection: {actual}; expected {expected}")
            print(
                f"FINAL businessDate={day} totalCredits={actual[0]} "
                f"totalDebits={actual[1]} balance={actual[2]} "
                f"transactionCount={actual[3]}",
                flush=True,
            )
            return
        if body["transactionCount"] not in (0, 1):
            raise AssertionError(f"Unexpected projection during convergence: {last}")
        time.sleep(0.25)
    raise AssertionError(
        f"Consolidation did not converge within {seconds}s for {day}. Last: {last}"
    )


def restore():
    compose("start", "postgres-consolidation")
    wait_healthy("postgres-consolidation")
    compose("start", "consolidation-worker")
    wait_healthy("consolidation-worker")
    compose("start", "consolidation-api")
    wait_healthy("consolidation-api")


def main():
    current = services()
    for service in REQUIRED:
        row = current.get(service)
        if row is None or row["State"] != "running" or row["Health"] != "healthy":
            raise AssertionError(f"Compose must start healthy: {service}={row}")
    print("COMPOSE: all required services healthy", flush=True)
    jwt = token()
    expect_status("TRANSACTIONS_LIVE_BEFORE", f"{TRANSACTIONS}/health/live", 200)
    expect_status("TRANSACTIONS_READY_BEFORE", f"{TRANSACTIONS}/health/ready", 200)
    day = isolated_date(jwt)

    try:
        compose("stop", *STOPPED)
        after_stop = services()
        for service in STOPPED:
            row = after_stop.get(service)
            if row is None or row["State"] != "exited":
                raise AssertionError(f"{service} did not stop: {row}")
            print(f"STOPPED {service}: {row['State']}", flush=True)
        expect_status("TRANSACTIONS_LIVE_DURING_OUTAGE", f"{TRANSACTIONS}/health/live", 200)
        expect_status("TRANSACTIONS_READY_DURING_OUTAGE", f"{TRANSACTIONS}/health/ready", 200)
        credit_id = create(jwt, day, "CREDIT", "100.00")
        debit_id = create(jwt, day, "DEBIT", "20.00")
        if credit_id == debit_id:
            raise AssertionError("POSTs returned the same transaction ID.")
    finally:
        restore()

    expect_status("CONSOLIDATION_READY_AFTER_RECOVERY", f"{CONSOLIDATION}/health/ready", 200)
    wait_projection(jwt, day)


if __name__ == "__main__":
    main()
