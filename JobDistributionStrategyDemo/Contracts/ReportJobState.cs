namespace JobDistributionStrategyDemo.Contracts;

public record ReportJobState
{
    public Guid ReportId { get; init; } = Guid.Empty;
    public string ReportName { get; init; } = "";
    public int CurrentStage { get; init; }
    public string StageDescription { get; init; } = "Not started";
    public DateTime? StartedAt { get; init; }
    public string? DataSummary { get; init; }
    public string? AggregationSummary { get; init; }
    public string? GeneratedFilePath { get; init; }
    public string? UploadDestination { get; init; }
}
