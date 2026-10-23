using Npgsql;

namespace CashFlow.Recovery;

public sealed class PostgresRecoveryLease(NpgsqlDataSource source)
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(15);

    public async Task<Guid> AcquireAsync(Guid owner, CancellationToken token)
    {
        await using var command = source.CreateCommand("""
            UPDATE projection_control
            SET maintenance = true, maintenance_run_id = $1,
                maintenance_started_at = clock_timestamp(),
                maintenance_expires_at = clock_timestamp() + $2
            WHERE singleton = true
              AND (maintenance = false OR maintenance_expires_at <= clock_timestamp())
            RETURNING active_generation_id
            """);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(Duration);
        return (Guid?)await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("Projection already has an active maintenance lease or is missing.");
    }

    public async Task<bool> RenewAsync(Guid owner, CancellationToken token)
    {
        await using var command = source.CreateCommand("""
            UPDATE projection_control
            SET maintenance_expires_at = clock_timestamp() + $2
            WHERE singleton = true AND maintenance = true AND maintenance_run_id = $1
              AND maintenance_expires_at > clock_timestamp()
            """);
        command.Parameters.AddWithValue(owner);
        command.Parameters.AddWithValue(Duration);
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task<bool> ReleaseAsync(Guid owner, Guid previous, CancellationToken token)
    {
        await using var command = source.CreateCommand("""
            UPDATE projection_control
            SET maintenance = false, maintenance_run_id = NULL,
                maintenance_started_at = NULL, maintenance_expires_at = NULL
            WHERE singleton = true AND active_generation_id = $1
              AND maintenance = true AND maintenance_run_id = $2
              AND maintenance_expires_at > clock_timestamp()
            """);
        command.Parameters.AddWithValue(previous);
        command.Parameters.AddWithValue(owner);
        return await command.ExecuteNonQueryAsync(token) == 1;
    }
}
