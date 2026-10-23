#!/usr/bin/env python3
"""Prove a permanently invalid RabbitMQ message reaches DLQ without financial effect."""

import base64
from datetime import date, timedelta
import json
import os
import secrets
import time
import urllib.parse
import urllib.request
import uuid


IDENTITY = os.environ.get("CASHFLOW_E2E_IDENTITY_URL", "http://127.0.0.1:8080")
CONSOLIDATION = os.environ.get("CASHFLOW_E2E_CONSOLIDATION_URL", "http://127.0.0.1:8082")
MANAGEMENT = os.environ.get("CASHFLOW_RABBITMQ_MANAGEMENT_URL", "http://127.0.0.1:15672")
QUEUE = "consolidation.transactions.v1.dlq"


def request(url, *, method="GET", body=None, authorization=None):
    headers = {}
    if authorization:
        headers["Authorization"] = authorization
    data = json.dumps(body).encode() if body is not None else None
    if data is not None:
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=10) as response:
        return response.status, json.load(response)


def main():
    reader_secret = os.environ.get("READER_CLIENT_SECRET")
    broker_password = os.environ.get("RABBITMQ_PASSWORD", "cashflow_local_only")
    if not reader_secret:
        raise RuntimeError("Set READER_CLIENT_SECRET from the ignored .env file.")
    token_req = urllib.request.Request(
        f"{IDENTITY}/realms/cashflow/protocol/openid-connect/token",
        data=urllib.parse.urlencode({"grant_type": "client_credentials",
                                     "client_id": "cashflow-reader",
                                     "client_secret": reader_secret}).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST")
    with urllib.request.urlopen(token_req, timeout=10) as response:
        token = json.load(response)["access_token"]
        print(f"Keycloak reader token: HTTP {response.status}", flush=True)
    bearer = f"Bearer {token}"
    basic = "Basic " + base64.b64encode(f"cashflow:{broker_password}".encode()).decode()
    queue_url = f"{MANAGEMENT}/api/queues/%2F/{QUEUE}"
    before = request(queue_url, authorization=basic)[1]["messages"]

    start = date(1900, 1, 1)
    for _ in range(20):
        day = (start + timedelta(days=secrets.randbelow(36_525))).isoformat()
        status, projection = request(f"{CONSOLIDATION}/consolidations/{day}", authorization=bearer)
        if status == 200 and projection["transactionCount"] == 0:
            break
    else:
        raise AssertionError("Could not find an empty business date.")
    baseline = (projection["totalCredits"], projection["totalDebits"],
                projection["balance"], projection["transactionCount"])
    event_id = str(uuid.uuid4())
    payload = json.dumps({"schemaVersion": 1, "eventId": event_id,
                          "eventType": "TransactionCreated",
                          "eventOccurredAt": "2026-10-21T15:00:00Z",
                          "transactionId": str(uuid.uuid4()),
                          "transactionType": "CREDIT", "amount": "-1.00",
                          "businessDate": day})
    status, published = request(
        f"{MANAGEMENT}/api/exchanges/%2F/cashflow.events/publish",
        method="POST", authorization=basic,
        body={"properties": {"delivery_mode": 2, "message_id": event_id,
                             "content_type": "application/json"},
              "routing_key": "transaction.created.v1", "payload": payload,
              "payload_encoding": "string"})
    if status != 200 or not published.get("routed"):
        raise AssertionError(f"Invalid message was not routed: HTTP {status}, {published}")
    print(f"Published invalid message {event_id}: routed=true", flush=True)

    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        messages = request(queue_url, authorization=basic)[1]["messages"]
        if messages >= before + 1:
            break
        time.sleep(0.2)
    else:
        raise AssertionError(f"DLQ count did not increase: before={before}, after={messages}")
    status, entries = request(f"{queue_url}/get", method="POST", authorization=basic,
                              body={"count": messages, "ackmode": "ack_requeue_true",
                                    "encoding": "auto", "truncate": 50000})
    if status != 200 or not any(row.get("properties", {}).get("message_id") == event_id
                                for row in entries):
        raise AssertionError("Invalid message ID was not found in the DLQ.")
    status, after = request(f"{CONSOLIDATION}/consolidations/{day}", authorization=bearer)
    final = (after["totalCredits"], after["totalDebits"],
             after["balance"], after["transactionCount"])
    if status != 200 or final != baseline:
        raise AssertionError(f"Financial projection changed: before={baseline}, after={final}")
    print(f"PASS: DLQ {before}->{messages}, messageId={event_id}, "
          f"businessDate={day}, projection={final}", flush=True)


if __name__ == "__main__":
    main()
