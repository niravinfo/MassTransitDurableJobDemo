namespace JobDistributionStrategyDemo.Contracts;

public record GenerateReport
{
    public Guid ReportId { get; init; }
    public required string ReportName { get; init; }
    public required string ReportType { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public string? RequestedBy { get; init; }
    public string? GroupKey { get; init; }
}

public record SubmitReportRequest
{
    public required string ReportName { get; init; }
    public required string ReportType { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? RequestedBy { get; init; }
    public string? GroupKey { get; init; }
}

public record SubmitReportResponse
{
    public Guid JobId { get; init; }
    public Guid ReportId { get; init; }
    public string Message { get; init; } = default!;
    public DateTime SubmittedAt { get; init; }
}

public record BulkSubmitReportRequest
{
    public int Count { get; init; }
    public bool UseDifferentGroupKeys { get; init; } = true;
}

public record BulkSubmitReportResponse
{
    public int TotalSubmitted { get; init; }
    public DateTime SubmittedAt { get; init; }
    public List<BulkJobResult> Jobs { get; init; } = new();
}

public record BulkJobResult
{
    public Guid JobId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = default!;
    public string ReportType { get; init; } = default!;
    public string? GroupKey { get; init; }
}

public record ReportStatusResponse
{
    public Guid JobId { get; init; }
    public Guid ReportId { get; init; }
    public string ReportName { get; init; } = default!;
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

public record GetJobDataResponse
{
    public Guid JobId { get; init; }
    public Dictionary<string, object> Job { get; init; } = new();
    public Dictionary<string, object>? JobState { get; init; }
    public long? ProgressLimit { get; init; }
    public double? ProgressPercentage { get; init; }
    public DateTime? Submitted { get; init; }
    public DateTime? Started { get; init; }
    public DateTime? Completed { get; init; }
    public DateTime? Faulted { get; init; }
    public string? Reason { get; init; }
    public int RetryAttempt { get; init; }
}
