using MassTransit;
using MassTransit.Contracts.JobService;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransitDurableJobDemo.Consumers;
using MassTransitDurableJobDemo.Contracts;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Reflection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting MassTransit Durable Job Demo");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog integration
    builder.Host.UseSerilog();

    // EF Core / SQLite
    builder.Services.AddDbContext<JobServiceSagaDbContext>(options =>
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), m =>
        {
            m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
            m.MigrationsHistoryTable($"__{nameof(JobServiceSagaDbContext)}");
        });
    });

    // OpenAPI
    builder.Services.AddOpenApi();

    // MassTransit
    builder.Services.AddMassTransit(x =>
    {
        // Register the job consumer with options
        x.AddConsumer<GenerateReportJobConsumer>(cfg =>
        {
            cfg.Options<JobOptions<GenerateReport>>(options =>
                options.SetJobTimeout(TimeSpan.FromMinutes(30))
                       .SetConcurrentJobLimit(5));
        });

        // Job Service saga state machines with EF Core + SQLite
        x.AddJobSagaStateMachines(options =>
        {
            options.ConcurrentMessageLimit = 2;
            options.FinalizeCompleted = false;
            options.SuspectJobRetryCount = 3;
            options.SuspectJobRetryDelay = TimeSpan.FromSeconds(10);
        })
            .EntityFrameworkRepository(r =>
            {
                r.ExistingDbContext<JobServiceSagaDbContext>();
                r.UseSqlite();
            });

        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.UseDelayedMessageScheduler();
            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddOptions<MassTransitHostOptions>()
        .Configure(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(10);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });

    var app = builder.Build();

    // OpenAPI + Scalar UI
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Auto-migrate database on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<JobServiceSagaDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database initialized (SQLite)");
    }

    // API Endpoints

    // POST /api/reports - Submit a report generation job
    app.MapPost("/api/reports", async (
        SubmitReportRequest request,
        IPublishEndpoint publishEndpoint) =>
    {
        var reportId = Guid.NewGuid();

        var message = new GenerateReport
        {
            ReportId = reportId,
            ReportName = request.ReportName,
            ReportType = request.ReportType,
            DateFrom = request.DateFrom ?? DateTime.UtcNow.AddDays(-30),
            DateTo = request.DateTo ?? DateTime.UtcNow,
            RequestedBy = request.RequestedBy
        };

        var jobId = await publishEndpoint.SubmitJob<GenerateReport>(message, CancellationToken.None);

        Log.Information("Report job submitted: JobId={JobId}, ReportId={ReportId}, Name={Name}",
            jobId, reportId, request.ReportName);

        return Results.Ok(new SubmitReportResponse
        {
            JobId = jobId,
            ReportId = reportId,
            Message = "Report generation job submitted successfully",
            SubmittedAt = DateTime.UtcNow
        });
    })
    .WithName("SubmitReport")
    .Produces<SubmitReportResponse>();

    // GET /api/reports/{jobId} - Get job status
    app.MapGet("/api/reports/{jobId:guid}", async (
        Guid jobId,
        IRequestClient<GetJobState> requestClient) =>
    {
        try
        {
            var jobState = await requestClient.GetJobState<ReportJobState>(jobId);

            return Results.Ok(new ReportStatusResponse
            {
                JobId = jobState.JobId,
                ReportId = jobState.JobState?.ReportId ?? Guid.Empty,
                ReportName = jobState.JobState?.ReportName ?? "",
                CurrentState = jobState.CurrentState ?? "Unknown",
                ProgressValue = jobState.ProgressValue ?? 0,
                ProgressLimit = jobState.ProgressLimit ?? 100,
                ProgressPercentage = jobState.ProgressValue.HasValue && jobState.ProgressLimit.HasValue && jobState.ProgressLimit.Value > 0
                    ? (double)jobState.ProgressValue.Value / jobState.ProgressLimit.Value * 100
                    : 0,
                Submitted = jobState.Submitted,
                Started = jobState.Started,
                Completed = jobState.Completed,
                Duration = jobState.Started.HasValue && jobState.Completed.HasValue
                    ? jobState.Completed.Value - jobState.Started.Value
                    : null,
                FaultReason = jobState.Faulted.HasValue ? jobState.Reason : null,
                IsFaulted = jobState.Faulted.HasValue,
                IsCompleted = jobState.Completed.HasValue
            });
        }
        catch (RequestTimeoutException)
        {
            return Results.NotFound(new { Error = "Job not found or timed out" });
        }
    })
    .WithName("GetReportStatus")
    .Produces<ReportStatusResponse>()
    .Produces(404);

    // GET /api/reports - List available endpoints (health check)
    app.MapGet("/api/reports", () =>
    {
        return Results.Ok(new
        {
            Message = "MassTransit Durable Job Demo API",
            Endpoints = new[]
            {
                "POST /api/reports - Submit a report generation job",
                "GET /api/reports/{jobId} - Get job status by Job ID"
            }
        });
    })
    .WithName("ListEndpoints");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
