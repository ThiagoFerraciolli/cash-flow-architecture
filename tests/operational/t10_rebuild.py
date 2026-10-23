#!/usr/bin/env python3
"""Create known facts via HTTP, rebuild locally, and verify generation and totals."""

from datetime import date, timedelta
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
import urllib.parse
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[2]
IDENTITY = os.environ.get("CASHFLOW_E2E_IDENTITY_URL", "http://127.0.0.1:8080")
TRANSACTIONS = os.environ.get("CASHFLOW_E2E_TRANSACTIONS_URL", "http://127.0.0.1:8081")
CONSOLIDATION = os.environ.get("CASHFLOW_E2E_CONSOLIDATION_URL", "http://127.0.0.1:8082")


def http(url, token=None, *, method="GET", body=None, headers=None):
    values = dict(headers or {})
    if token:
        values["Authorization"] = f"Bearer {token}"
    data = json.dumps(body).encode() if body is not None else None
    if data is not None:
        values["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=values, method=method)
    with urllib.request.urlopen(req, timeout=10) as response:
        return response.status, json.load(response)


def token(client, secret):
    if not secret:
        raise RuntimeError(f"Missing {client} client secret in the local environment.")
    req = urllib.request.Request(
        f"{IDENTITY}/realms/cashflow/protocol/openid-connect/token",
        data=urllib.parse.urlencode({"grant_type": "client_credentials", "client_id": client,
                                     "client_secret": secret}).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST")
    with urllib.request.urlopen(req, timeout=10) as response:
        return json.load(response)["access_token"]


def isolated_date(jwt):
    start = date(1900, 1, 1)
    for _ in range(20):
        day = (start + timedelta(days=secrets.randbelow(36_525))).isoformat()
        tx = http(f"{TRANSACTIONS}/transactions?from={day}&to={day}", jwt)[1]
        co = http(f"{CONSOLIDATION}/consolidations/{day}", jwt)[1]
        if not tx["items"] and co["transactionCount"] == 0:
            return day
    raise AssertionError("No isolated business date found.")


def create(jwt, day, kind, amount):
    status, body = http(f"{TRANSACTIONS}/transactions", jwt, method="POST",
                        headers={"Idempotency-Key": str(uuid.uuid4()),
                                 "X-Correlation-Id": str(uuid.uuid4())},
                        body={"type": kind, "amount": amount,
                              "occurredAt": f"{day}T12:00:00-03:00",
                              "description": "T10 local rebuild"})
    if status != 201 or body["businessDate"] != day:
        raise AssertionError(f"POST {kind} failed: HTTP {status}: {body}")
    print(f"POST {kind} {amount}: HTTP {status}", flush=True)


def wait_projection(jwt, day, *, generation=None):
    deadline = time.monotonic() + 60
    last = None
    while time.monotonic() < deadline:
        status, body = http(f"{CONSOLIDATION}/consolidations/{day}", jwt)
        last = (status, body)
        if (status == 200 and body["totalCredits"] == "100.00" and
                body["totalDebits"] == "20.00" and body["balance"] == "80.00" and
                body["transactionCount"] == 2 and body["projectionState"] == "AVAILABLE" and
                (generation is None or body["generationId"] != generation)):
            return body
        time.sleep(0.25)
    raise AssertionError(f"Projection did not converge: {last}")


def main():
    demo = token("cashflow-demo", os.environ.get("DEMO_CLIENT_SECRET"))
    reader = token("cashflow-reader", os.environ.get("READER_CLIENT_SECRET"))
    day = isolated_date(demo)
    create(demo, day, "CREDIT", "100.00")
    create(demo, day, "DEBIT", "20.00")
    before = wait_projection(reader, day)
    env = dict(os.environ)
    env["CASHFLOW_RECOVERY_TRANSACTIONS"] = (
        "Host=127.0.0.1;Port=5433;Database=cashflow;Username=cashflow;Password=" +
        os.environ.get("POSTGRES_PASSWORD", "cashflow_local_only"))
    env["CASHFLOW_RECOVERY_CONSOLIDATION"] = (
        "Host=127.0.0.1;Port=5434;Database=consolidation;Username=consolidation;Password=" +
        os.environ.get("CONSOLIDATION_POSTGRES_PASSWORD", "consolidation_local_only"))
    result = subprocess.run(["dotnet", "run", "--project", "tools/CashFlow.Recovery",
                             "--no-build"], cwd=ROOT, env=env, text=True,
                            capture_output=True, check=False)
    if result.returncode:
        raise AssertionError(f"Recovery tool failed ({result.returncode}): {result.stderr.strip()}")
    print(result.stdout.strip(), flush=True)
    after = wait_projection(reader, day, generation=before["generationId"])
    print(f"PASS businessDate={day} generation={before['generationId']} -> "
          f"{after['generationId']} credits={after['totalCredits']} "
          f"debits={after['totalDebits']} balance={after['balance']} "
          f"count={after['transactionCount']}", flush=True)


if __name__ == "__main__":
    main()
