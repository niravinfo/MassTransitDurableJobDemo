namespace MassTransitDurableJobDemo.Contracts;

/// <summary>
/// The job message contract for report generation.
/// This is the message published to trigger the job consumer.
/// </summary>
public record GenerateReport
{
    /// <summary>
    /// Unique identifier for the report request.
    /// </summary>
    public Guid ReportId { get; init; }

    /// <summary>
    /// The name/title of the report to generate.
    /// </summary>
    public required string ReportName { get; init; }

    /// <summary>
    /// The type of report (e.g., "Sales", "Inventory", "Financial").
    /// </summary>
    public required string ReportType { get; init; }

    /// <summary>
    /// The date range start for the report data.
    /// </summary>
    public DateTime DateFrom { get; init; }

    /// <summary>
    /// The date range end for the report data.
    /// </summary>
    public DateTime DateTo { get; init; }

    /// <summary>
    /// The requested by user/actor.
    /// </summary>
    public string? RequestedBy { get; init; }
}

/// <summary>
/// Request model for submitting a report generation job via the API.
/// </summary>
public record SubmitReportRequest
{
    public required string ReportName { get; init; }
    public required string ReportType { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? RequestedBy { get; init; }
}

/// <summary>
/// Response model returned when a report job is submitted.
/// </summary>
public record SubmitReportResponse
{
    public Guid JobId { get; init; }
    public Guid ReportId { get; init; }
    public string Message { get; init; } = default!;
    public DateTime SubmittedAt { get; init; }
}

/// <summary>
/// Response model for job status queries.
/// </summary>
public record ReportStatusResponse
{
    public Guid JobId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = default!;
    public string ReportType { get; init; } = default!;
    public string CurrentState { get; init; } = default!;
    public long ProgressValue { get; init; }
    public long ProgressLimit { get; init; }
    public double ProgressPercentage { get; init; }
    public DateTime? Submitted { get; init; }
    public DateTime? Started { get; init; }
    public DateTime? Completed { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? FaultReason { get; init; }
    public bool IsFaulted { get; init; }
    public bool IsCompleted { get; init; }
}
