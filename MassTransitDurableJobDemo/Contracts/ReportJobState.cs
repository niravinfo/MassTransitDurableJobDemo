namespace MassTransitDurableJobDemo.Contracts;

/// <summary>
/// Internal state tracked by the job consumer across stages.
/// Saved via JobContext.SaveJobState so it survives retries/cancellation.
/// </summary>
public record ReportJobState
{
    /// <summary>
    /// Which stage the job reached before being interrupted (0 = not started).
    /// </summary>
    public int CurrentStage { get; init; }

    /// <summary>
    /// Human-readable description of the current stage.
    /// </summary>
    public string StageDescription { get; init; } = "Not started";

    /// <summary>
    /// When processing started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Fetched data details.
    /// </summary>
    public string? DataSummary { get; init; }

    /// <summary>
    /// Aggregated data details.
    /// </summary>
    public string? AggregationSummary { get; init; }

    /// <summary>
    /// Generated file path.
    /// </summary>
    public string? GeneratedFilePath { get; init; }

    /// <summary>
    /// Upload destination.
    /// </summary>
    public string? UploadDestination { get; init; }
}
