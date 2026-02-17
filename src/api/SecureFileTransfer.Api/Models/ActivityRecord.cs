using Azure;
using Azure.Data.Tables;

namespace SecureFileTransfer.Api.Models;

public class ActivityRecord : ITableEntity
{
    /// <summary>PartitionKey is the container name (e.g., "sft-acme").</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>RowKey is a reverse-chronological ticks key for natural sort order.</summary>
    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Action { get; set; } = string.Empty; // Upload, Download, Delete
    public string FileName { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
