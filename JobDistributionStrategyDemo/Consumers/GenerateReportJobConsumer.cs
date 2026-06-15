using JobDistributionStrategyDemo.Contracts;
using MassTransit;

namespace JobDistributionStrategyDemo.Consumers;

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
        var groupKey = msg.GroupKey ?? "none";

        _logger.LogInformation(
            "[GroupKey={GroupKey}] [Job {JobId}] Starting report generation: {ReportName} (Type: {ReportType}), Range: {From} to {To}",
            groupKey, context.JobId, msg.ReportName, msg.ReportType,
            msg.DateFrom.ToString("yyyy-MM-dd"), msg.DateTo.ToString("yyyy-MM-dd"));

        var state = context.TryGetJobState(out ReportJobState? existingState)
            ? existingState!
            : new ReportJobState()
            {
                ReportId = msg.ReportId,
                ReportName = msg.ReportName
            };

        var startStage = state.CurrentStage;

        _logger.LogInformation(
            "[GroupKey={GroupKey}] [Job {JobId}] Resuming from stage {Stage} ({Description})",
            groupKey, context.JobId, startStage, state.StageDescription);

        try
        {
            if (startStage < 1)
            {
                state = state with { CurrentStage = 1, StageDescription = "Fetching data" };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 1/5: Fetching data...", groupKey, context.JobId);

                await context.SetJobProgress(0, 100);
                await SimulateWork(context.CancellationToken, Random.Shared.Next(3500, 7500));

                state = state with
                {
                    StartedAt = DateTime.UtcNow,
                    DataSummary = $"Fetched {(Random.Shared.Next(100, 5000))} records for {msg.ReportType} report"
                };

                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 1 complete: {Summary}", groupKey, context.JobId, state.DataSummary);

                await context.SetJobProgress(10, 100);
                await context.SaveJobState(state);
            }

            await SimulateWork(context.CancellationToken, Random.Shared.Next(5_000, 10_000));

            if (startStage < 2)
            {
                state = state with { CurrentStage = 2, StageDescription = "Aggregating data" };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 2/5: Aggregating data...", groupKey, context.JobId);

                for (int i = 1; i <= 3; i++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await SimulateWork(context.CancellationToken, Random.Shared.Next(2_500, 5_000));

                    long progress = 10 + i * 10;
                    await context.SetJobProgress(progress, 100);

                    _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}]   Aggregation step {Step}/3", groupKey, context.JobId, i);
                }

                state = state with
                {
                    AggregationSummary = $"Aggregated into {Random.Shared.Next(10, 50)} groups with {Random.Shared.Next(5, 20)} metrics"
                };

                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 2 complete: {Summary}", groupKey, context.JobId, state.AggregationSummary);

                await context.SaveJobState(state);
            }

            await SimulateWork(context.CancellationToken, Random.Shared.Next(5_000, 10_000));

            if (startStage < 3)
            {
                state = state with { CurrentStage = 3, StageDescription = "Generating Excel file" };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 3/5: Generating Excel file...", groupKey, context.JobId);

                for (int sheet = 1; sheet <= 3; sheet++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    await SimulateWork(context.CancellationToken, Random.Shared.Next(2_500, 5_000));

                    long progress = 40 + sheet * 10;
                    await context.SetJobProgress(progress, 100);

                    _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}]   Writing sheet {Sheet}/3", groupKey, context.JobId, sheet);
                }

                var filePath = $"/reports/{msg.ReportType}_{msg.ReportId:N}.xlsx";
                state = state with { GeneratedFilePath = filePath };

                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 3 complete: File at {Path}", groupKey, context.JobId, filePath);

                await context.SaveJobState(state);
            }

            await SimulateWork(context.CancellationToken, Random.Shared.Next(5_000, 10_000));

            if (startStage < 4)
            {
                state = state with { CurrentStage = 4, StageDescription = "Uploading file" };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 4/5: Uploading file...", groupKey, context.JobId);

                await SimulateWork(context.CancellationToken, Random.Shared.Next(2_500, 5_000));

                var destination = $"https://storage.example.com/reports/{msg.ReportId:N}";
                state = state with { UploadDestination = destination };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 4 complete: Uploaded to {Dest}", groupKey, context.JobId, destination);

                await context.SetJobProgress(90, 100);
                await context.SaveJobState(state);
            }

            await SimulateWork(context.CancellationToken, Random.Shared.Next(5_000, 10_000));

            if (startStage < 5)
            {
                state = state with
                {
                    CurrentStage = 5,
                    StageDescription = "Completed"
                };
                _logger.LogInformation("[GroupKey={GroupKey}] [Job {JobId}] Stage 5/5: Finalizing...", groupKey, context.JobId);

                await SimulateWork(context.CancellationToken, Random.Shared.Next(2_500, 5_000));
                await context.SetJobProgress(100, 100);

                _logger.LogInformation(
                    "[GroupKey={GroupKey}] [Job {JobId}] Report generation COMPLETE: {ReportName} | File: {File} | Uploaded to: {Dest}",
                    groupKey, context.JobId, msg.ReportName, state.GeneratedFilePath, state.UploadDestination);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("[GroupKey={GroupKey}] [Job {JobId}] Job CANCELED at stage {Stage} ({Desc}), saving state...",
                groupKey, context.JobId, state.CurrentStage, state.StageDescription);
            await context.SaveJobState(state);
            throw;
        }
    }

    private static async Task SimulateWork(CancellationToken cancellationToken, int milliseconds)
    {
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
