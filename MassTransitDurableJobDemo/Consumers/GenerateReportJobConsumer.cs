using MassTransit;
using MassTransitDurableJobDemo.Contracts;

namespace MassTransitDurableJobDemo.Consumers;

/// <summary>
/// Job Consumer that generates a report through 5 progressive stages.
/// Implements IJobConsumer&lt;GenerateReport&gt; for MassTransit's Job Service.
/// Progress is reported via context.SetJobProgress() and state via context.SaveJobState().
/// </summary>
public class GenerateReportJobConsumer : IJobConsumer<GenerateReport>
{
    private readonly ILogger<GenerateReportJobConsumer> _logger;

    public GenerateReportJobConsumer(ILogger<GenerateReportJobConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Run(JobContext<GenerateReport> context)
    {
        var msg = context.Job;

        _logger.LogInformation(
            "[Job {JobId}] Starting report generation: {ReportName} (Type: {ReportType}), Range: {From} to {To}",
            context.JobId, msg.ReportName, msg.ReportType,
            msg.DateFrom.ToString("yyyy-MM-dd"), msg.DateTo.ToString("yyyy-MM-dd"));

        // Check if there is previously saved state (from a retry after cancellation)
        var state = context.TryGetJobState(out ReportJobState? existingState)
            ? existingState!
            : new ReportJobState();

        var startStage = state.CurrentStage;

        _logger.LogInformation(
            "[Job {JobId}] Resuming from stage {Stage} ({Description})",
            context.JobId, startStage, state.StageDescription);

        try
        {
            // Stage 1: Fetch Data (0% -> 10%)
            if (startStage < 1)
            {
                state = state with { CurrentStage = 1, StageDescription = "Fetching data" };
                _logger.LogInformation("[Job {JobId}] Stage 1/5: Fetching data...", context.JobId);

                await context.SetJobProgress(0, 100);
                await SimulateWork(context.CancellationToken, Random.Shared.Next(3500, 7500));

                state = state with
                {
                    StartedAt = DateTime.UtcNow,
                    DataSummary = $"Fetched {(Random.Shared.Next(100, 5000))} records for {msg.ReportType} report"
                };

                _logger.LogInformation("[Job {JobId}] Stage 1 complete: {Summary}", context.JobId, state.DataSummary);

                await context.SetJobProgress(10, 100);
                await context.SaveJobState(state);
            }

            // Stage 2: Aggregate (10% -> 40%)
            if (startStage < 2)
            {
                state = state with { CurrentStage = 2, StageDescription = "Aggregating data" };
                _logger.LogInformation("[Job {JobId}] Stage 2/5: Aggregating data...", context.JobId);

                // Simulate multi-step aggregation with intermediate progress
                for (int i = 1; i <= 3; i++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await SimulateWork(context.CancellationToken, 2000);

                    // Progress within stage 2: maps to overall progress ~10-40%
                    long progress = 10 + i * 10; // 20, 30, 40
                    await context.SetJobProgress(progress, 100);

                    _logger.LogInformation("[Job {JobId}]   Aggregation step {Step}/3", context.JobId, i);
                }

                state = state with
                {
                    AggregationSummary = $"Aggregated into {Random.Shared.Next(10, 50)} groups with {Random.Shared.Next(5, 20)} metrics"
                };

                _logger.LogInformation("[Job {JobId}] Stage 2 complete: {Summary}", context.JobId, state.AggregationSummary);

                await context.SaveJobState(state);
            }

            // Stage 3: Generate Excel (40% -> 70%)
            if (startStage < 3)
            {
                state = state with { CurrentStage = 3, StageDescription = "Generating Excel file" };
                _logger.LogInformation("[Job {JobId}] Stage 3/5: Generating Excel file...", context.JobId);

                // Simulate writing sheets with intermediate progress
                for (int sheet = 1; sheet <= 3; sheet++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await SimulateWork(context.CancellationToken, 1200);

                    // Progress within stage 3: maps to overall progress ~40-70%
                    long progress = 40 + sheet * 10; // 50, 60, 70
                    await context.SetJobProgress(progress, 100);

                    _logger.LogInformation("[Job {JobId}]   Writing sheet {Sheet}/3", context.JobId, sheet);
                }

                var filePath = $"/reports/{msg.ReportType}_{msg.ReportId:N}.xlsx";
                state = state with { GeneratedFilePath = filePath };

                _logger.LogInformation("[Job {JobId}] Stage 3 complete: File at {Path}", context.JobId, filePath);

                await context.SaveJobState(state);
            }

            // Stage 4: Upload File (70% -> 90%)
            if (startStage < 4)
            {
                state = state with { CurrentStage = 4, StageDescription = "Uploading file" };
                _logger.LogInformation("[Job {JobId}] Stage 4/5: Uploading file...", context.JobId);

                await SimulateWork(context.CancellationToken, 2000);

                var destination = $"https://storage.example.com/reports/{msg.ReportId:N}";
                state = state with { UploadDestination = destination };
                _logger.LogInformation("[Job {JobId}] Stage 4 complete: Uploaded to {Dest}", context.JobId, destination);

                await context.SetJobProgress(90, 100);
                await context.SaveJobState(state);
            }

            // Stage 5: Complete (90% -> 100%)
            if (startStage < 5)
            {
                state = state with
                {
                    CurrentStage = 5,
                    StageDescription = "Completed"
                };
                _logger.LogInformation("[Job {JobId}] Stage 5/5: Finalizing...", context.JobId);

                await SimulateWork(context.CancellationToken, 500);
                await context.SetJobProgress(100, 100);

                _logger.LogInformation(
                    "[Job {JobId}] Report generation COMPLETE: {ReportName} | File: {File} | Uploaded to: {Dest}",
                    context.JobId, msg.ReportName, state.GeneratedFilePath, state.UploadDestination);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Save state before rethrowing so it can resume on retry
            _logger.LogWarning("[Job {JobId}] Job CANCELED at stage {Stage} ({Desc}), saving state...",
                context.JobId, state.CurrentStage, state.StageDescription);
            await context.SaveJobState(state);
            throw;
        }
    }

    private static async Task SimulateWork(CancellationToken cancellationToken, int milliseconds)
    {
        // Simulate work in smaller chunks so cancellation is responsive
        var remaining = milliseconds;
        var chunk = Math.Min(500, remaining);
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(chunk, cancellationToken);
            remaining -= chunk;
        }
    }
}
