using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CashFlow.Transactions.Application;
using CashFlow.Transactions.Domain;
using Npgsql;
using NpgsqlTypes;

namespace CashFlow.Transactions.Infrastructure;

public sealed class PostgresTransactionCreationStore(NpgsqlDataSource dataSource) : ITransactionCreationStore
{
    public async Task<CreateTransactionResult> CreateOrReplayAsync(AuthenticatedClientIdentity identity, Guid key,
        string requestHash, int canonicalVersion, FinancialTransaction candidate, string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using var reserve = new NpgsqlCommand("""
            INSERT INTO idempotency_records
              (issuer, client_id, idempotency_key, request_hash, canonical_version,
               transaction_id, response_status, response_body, created_at)
            VALUES ($1, $2, $3, $4, $5, $6, 201, $7, $8)
            ON CONFLICT (issuer, client_id, idempotency_key) DO NOTHING
            RETURNING transaction_id
            """, connection, transaction);
        reserve.Parameters.AddWithValue(identity.Issuer);
        reserve.Parameters.AddWithValue(identity.ClientId);
        reserve.Parameters.AddWithValue(key);
        reserve.Parameters.AddWithValue(requestHash);
        reserve.Parameters.AddWithValue(canonicalVersion);
        reserve.Parameters.AddWithValue(candidate.Id);
        reserve.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new { id = candidate.Id }));
        reserve.Parameters.AddWithValue(candidate.CreatedAt);
        var reservedId = await reserve.ExecuteScalarAsync(cancellationToken);

        if (reservedId is null)
        {
            await using var existing = new NpgsqlCommand("""
                SELECT request_hash, canonical_version, transaction_id
                FROM idempotency_records
                WHERE issuer = $1 AND client_id = $2 AND idempotency_key = $3
                """, connection, transaction);
            existing.Parameters.AddWithValue(identity.Issuer);
            existing.Parameters.AddWithValue(identity.ClientId);
            existing.Parameters.AddWithValue(key);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Idempotency reservation disappeared.");
            var sameRequest = reader.GetString(0) == requestHash && reader.GetInt32(1) == canonicalVersion;
            var existingId = reader.GetGuid(2);
            await reader.CloseAsync();
            if (!sameRequest)
            {
                await transaction.CommitAsync(cancellationToken);
                return new CreateTransactionResult(CreateTransactionStatus.Conflict, null);
            }
            var replayed = await ReadFact(connection, transaction, existingId, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency fact is missing.");
            await transaction.CommitAsync(cancellationToken);
            return new CreateTransactionResult(CreateTransactionStatus.Replayed, replayed);
        }

        await using (var insertFact = new NpgsqlCommand("""
            INSERT INTO financial_transactions
              (id, creation_event_id, type, amount, description, occurred_at, business_date, created_at,
               actor_issuer, actor_client_id)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            """, connection, transaction))
        {
            insertFact.Parameters.AddWithValue(candidate.Id);
            insertFact.Parameters.AddWithValue(candidate.CreationEventId);
            insertFact.Parameters.AddWithValue(candidate.Type.ToString().ToUpperInvariant());
            insertFact.Parameters.AddWithValue(candidate.Amount);
            insertFact.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)candidate.Description ?? DBNull.Value);
            insertFact.Parameters.AddWithValue(candidate.OccurredAt);
            insertFact.Parameters.AddWithValue(candidate.BusinessDate);
            insertFact.Parameters.AddWithValue(candidate.CreatedAt);
            insertFact.Parameters.AddWithValue(identity.Issuer);
            insertFact.Parameters.AddWithValue(identity.ClientId);
            await insertFact.ExecuteNonQueryAsync(cancellationToken);
        }

        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            eventId = candidate.CreationEventId,
            eventType = "TransactionCreated",
            eventOccurredAt = candidate.CreatedAt.UtcDateTime,
            transactionId = candidate.Id,
            transactionType = candidate.Type.ToString().ToUpperInvariant(),
            amount = candidate.Amount.ToString("F2", CultureInfo.InvariantCulture),
            businessDate = candidate.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
        var headers = new Dictionary<string, string> { ["correlation-id"] = correlationId };
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C, Id: not null } activity)
        {
            headers["traceparent"] = activity.Id;
            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
                headers["tracestate"] = activity.TraceStateString;
        }
        await using (var insertOutbox = new NpgsqlCommand("""
            INSERT INTO outbox_messages
              (event_id, aggregate_id, payload, headers, state, created_at, next_attempt_at)
            VALUES ($1, $2, $3, $4, 'PENDING', $5, $5)
            """, connection, transaction))
        {
            insertOutbox.Parameters.AddWithValue(candidate.CreationEventId);
            insertOutbox.Parameters.AddWithValue(candidate.Id);
            insertOutbox.Parameters.AddWithValue(NpgsqlDbType.Jsonb, payload);
            insertOutbox.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(headers));
            insertOutbox.Parameters.AddWithValue(candidate.CreatedAt);
            await insertOutbox.ExecuteNonQueryAsync(cancellationToken);
        }

        var created = await ReadFact(connection, transaction, candidate.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created fact is missing.");
        await transaction.CommitAsync(cancellationToken);
        return new CreateTransactionResult(CreateTransactionStatus.Created, created);
    }

    private static async Task<FinancialTransaction?> ReadFact(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT id, creation_event_id, type, amount, description, occurred_at, business_date, created_at
            FROM financial_transactions WHERE id = $1
            """, connection, transaction);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? PostgresTransactionRepository.Read(reader) : null;
    }
}
