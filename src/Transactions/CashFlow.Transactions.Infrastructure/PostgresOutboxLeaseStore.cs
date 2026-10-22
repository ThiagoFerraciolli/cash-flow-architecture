using System.Data;
using System.Text.Json;
using CashFlow.Transactions.Application;
using Npgsql;

namespace CashFlow.Transactions.Infrastructure;

public sealed class PostgresOutboxLeaseStore(NpgsqlDataSource source) : IOutboxLeaseStore
{
    public async Task<IReadOnlyList<OutboxLease>> ClaimAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = new NpgsqlCommand("""
            WITH eligible AS (
                SELECT event_id FROM outbox_messages
                WHERE state = 'PENDING' AND next_attempt_at <= clock_timestamp()
                  AND (lease_until IS NULL OR lease_until <= clock_timestamp())
                ORDER BY next_attempt_at, created_at, event_id
                FOR UPDATE SKIP LOCKED LIMIT $1
            )
            UPDATE outbox_messages AS o
            SET lease_token = gen_random_uuid(), lease_until = clock_timestamp() + interval '60 seconds'
            FROM eligible WHERE o.event_id = eligible.event_id
            RETURNING o.event_id, o.aggregate_id, o.lease_token, o.payload::text, o.headers::text, o.attempts
            """, connection, transaction);
        command.Parameters.AddWithValue(limit);
        var leases = new List<OutboxLease>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4))
                    ?? new Dictionary<string, string>();
                leases.Add(new OutboxLease(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                    reader.GetString(3), headers, reader.GetInt32(5)));
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return leases;
    }

    public async Task<bool> RenewAsync(OutboxLease lease, CancellationToken cancellationToken) =>
        await UpdateAsync("""
            UPDATE outbox_messages SET lease_until = clock_timestamp() + interval '60 seconds'
            WHERE event_id = $1 AND lease_token = $2 AND state = 'PENDING'
              AND lease_until > clock_timestamp()
            """, lease, null, cancellationToken);

    public async Task<bool> MarkPublishedAsync(OutboxLease lease, CancellationToken cancellationToken) =>
        await UpdateAsync("""
            UPDATE outbox_messages
            SET state = 'PUBLISHED', published_at = clock_timestamp(),
                lease_token = NULL, lease_until = NULL, last_error = NULL
            WHERE event_id = $1 AND lease_token = $2 AND state = 'PENDING'
              AND lease_until > clock_timestamp()
            """, lease, null, cancellationToken);

    public async Task<bool> MarkFailureAsync(OutboxLease lease, OutboxFailureKind kind, TimeSpan delay,
        string reason, CancellationToken cancellationToken)
    {
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(60)) throw new ArgumentOutOfRangeException(nameof(delay));
        if (reason.Length > 200) reason = reason[..200];
        return await UpdateAsync("""
            UPDATE outbox_messages
            SET state = CASE WHEN $3 = 'Permanent' OR ($3 = 'Isolated' AND attempts + 1 >= 20)
                             THEN 'SUSPENDED' ELSE 'PENDING' END,
                attempts = attempts + CASE WHEN $3 = 'Isolated' THEN 1 ELSE 0 END,
                next_attempt_at = clock_timestamp() + $4,
                lease_token = NULL, lease_until = NULL, last_error = $5
            WHERE event_id = $1 AND lease_token = $2 AND state = 'PENDING'
              AND lease_until > clock_timestamp()
            """, lease, command =>
        {
            command.Parameters.AddWithValue(kind.ToString());
            command.Parameters.AddWithValue(delay);
            command.Parameters.AddWithValue(reason);
        }, cancellationToken);
    }

    private async Task<bool> UpdateAsync(string sql, OutboxLease lease, Action<NpgsqlCommand>? parameters,
        CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand(sql);
        command.Parameters.AddWithValue(lease.EventId);
        command.Parameters.AddWithValue(lease.LeaseToken);
        parameters?.Invoke(command);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
