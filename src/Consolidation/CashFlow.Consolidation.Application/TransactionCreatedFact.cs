using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CashFlow.Consolidation.Application;

public sealed record TransactionCreatedFact(Guid EventId, Guid TransactionId, DateOnly BusinessDate,
    string TransactionType, decimal Amount, string FactHash)
{
    public static TransactionCreatedFact Parse(ReadOnlyMemory<byte> payload)
    {
        if (payload.Length > 16 * 1024) throw new InvalidTransactionEventException("Event exceeds 16 KiB.");
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("eventType").GetString() != "TransactionCreated")
                throw new InvalidTransactionEventException("Unsupported event contract.");
            var eventId = root.GetProperty("eventId").GetGuid();
            var transactionId = root.GetProperty("transactionId").GetGuid();
            var kind = root.GetProperty("transactionType").GetString();
            var amountText = root.GetProperty("amount").GetString();
            var dateText = root.GetProperty("businessDate").GetString();
            var occurredText = root.GetProperty("eventOccurredAt").GetString();
            if (eventId == Guid.Empty || transactionId == Guid.Empty ||
                kind is not ("CREDIT" or "DEBIT") ||
                amountText is null || !Regex.IsMatch(amountText, @"^(0|[1-9][0-9]*)\.[0-9]{2}$") ||
                !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
                amount is < 0.01m or > 999999999.99m ||
                !DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) ||
                !DateTimeOffset.TryParse(occurredText, CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var occurred) || occurred.Offset != TimeSpan.Zero)
                throw new InvalidTransactionEventException("Invalid financial event.");
            var canonical = string.Join('|', "v1", transactionId.ToString("D"),
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), kind, amountText);
            var hash = "v1:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            return new(eventId, transactionId, date, kind, amount, hash);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new InvalidTransactionEventException("Invalid TransactionCreatedV1 payload.");
        }
    }
}

public sealed class InvalidTransactionEventException(string message) : Exception(message);
public sealed class FinancialIdentityConflictException(string message) : Exception(message);
public sealed class ProjectionInMaintenanceException() : Exception("Projection is in maintenance.");

public enum ApplyTransactionResult { Applied, Duplicate }

public interface ITransactionProjection
{
    Task<ApplyTransactionResult> ApplyAsync(TransactionCreatedFact fact, CancellationToken cancellationToken);
    Task ProbeAsync(CancellationToken cancellationToken);
}
