using MassTransit;
using MassTransit.Contracts.JobService;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransitDurableJobDemo.Consumers;
using MassTransitDurableJobDemo.Contracts;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
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

    string persistenceProvider = builder.Configuration["PersistenceProvider"] ?? "sqlite";

    if (persistenceProvider == "mongodb")
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }
    else
    {
        // EF Core / SQLite
        builder.Services.AddDbContext<JobServiceSagaDbContext>(options =>
        {
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"), m =>
            {
                m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                m.MigrationsHistoryTable($"__{nameof(JobServiceSagaDbContext)}");
            });
        });
    }



    // OpenAPI
    builder.Services.AddOpenApi();


    // MassTransit
    builder.Services.AddMassTransit(x =>
    {
        // Register the job consumer with options
        x.AddConsumer<GenerateReportJobConsumer>(cfg =>
        {
            cfg.Options<JobOptions<GenerateReport>>(options =>
            {
                options.SetJobTimeout(TimeSpan.FromMinutes(30))
                       .SetConcurrentJobLimit(5);

                // Progress Buffer
                options.ProgressBuffer.TimeLimit = TimeSpan.FromSeconds(3);
            });
        });

        if (persistenceProvider == "mongodb")
        {
            // Job Service saga state machines with MongoDB
            x.AddJobSagaStateMachines(options =>
            {
                options.ConcurrentMessageLimit = 2;
                options.FinalizeCompleted = false;
                options.SuspectJobRetryCount = 3;
                options.SuspectJobRetryDelay = TimeSpan.FromSeconds(10);
            })
            .MongoDbRepository(r =>
            {
                r.Connection = builder.Configuration.GetConnectionString("MongoDb");
                r.DatabaseName = "MassTransitDemo";
            });
        }
        else
        {
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
        }


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

        x.DisableUsageTelemetry();
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

    if (persistenceProvider != "mongodb")
    {
        // Auto-migrate database on startup
        using var scope = app.Services.CreateScope();
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

    // GET /api/reports/{jobId}/data - Get raw job data from JobSaga table
    app.MapGet("/api/reports/{jobId:guid}/data", async (
        Guid jobId,
        IConfiguration configuration,
        IServiceProvider serviceProvider) =>
    {
        JobSaga? jobSaga;
        if (configuration.GetValue<string>("PersistenceProvider") == "mongodb")
        {
            IMongoCollection<JobSaga> jobSagas = serviceProvider.GetRequiredService<IMongoCollection<JobSaga>>();
            var asyncCursor = jobSagas.FindAsync(j => j.CorrelationId == jobId);
            jobSaga = await asyncCursor.Result.FirstOrDefaultAsync();
        }
        else
        {
            JobServiceSagaDbContext dbContext = serviceProvider.GetRequiredService<JobServiceSagaDbContext>();
            jobSaga = await EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(dbContext.Set<JobSaga>().AsNoTracking(), j => j.CorrelationId == jobId);
        }

        if (jobSaga is null)
        {
            return Results.NotFound(new { Error = "Job not found" });
        }

        return Results.Ok(new GetJobDataResponse
        {
            JobId = jobSaga.CorrelationId,
            Job = jobSaga.Job,
            JobState = jobSaga.JobState,
            ProgressLimit = jobSaga.LastProgressLimit,
            ProgressPercentage = jobSaga.LastProgressLimit.HasValue && jobSaga.LastProgressLimit.Value > 0
                ? (double)(jobSaga.LastProgressValue ?? 0) / jobSaga.LastProgressLimit.Value * 100
                : null,
            Submitted = jobSaga.Submitted,
            Started = jobSaga.Started,
            Completed = jobSaga.Completed,
            Faulted = jobSaga.Faulted,
            Reason = jobSaga.Reason,
            RetryAttempt = jobSaga.RetryAttempt
        });
    })
    .WithName("GetJobData")
    .Produces<GetJobDataResponse>()
    .Produces(404);

    // POST /api/reports/bulk - Submit multiple report generation jobs
    app.MapPost("/api/reports/bulk", async (
        BulkSubmitReportRequest request,
        IPublishEndpoint publishEndpoint) =>
    {
        var reportTypes = new[] { "Sales", "Inventory", "Financial", "Marketing", "HR", "Operations", "Customer", "Product", "Supply Chain", "Compliance" };
        var reportNames = new[] { "Q1 Summary", "Monthly Overview", "Trends Analysis", "Performance Review", "Budget Report", "Audit Report", "Forecast", "Compliance Check", "Regional Breakdown", "Year-End Wrap", "Profit Analysis", "Cost Report" };
        var requestedByUsers = new[] { "alice", "bob", "charlie", "diana", "eve", "frank" };
        var random = new Random();

        var response = new BulkSubmitReportResponse
        {
            TotalSubmitted = request.Count,
            SubmittedAt = DateTime.UtcNow
        };

        for (int i = 0; i < request.Count; i++)
        {
            var reportId = Guid.NewGuid();
            var dateFrom = DateTime.UtcNow.AddDays(-random.Next(7, 180));
            var message = new GenerateReport
            {
                ReportId = reportId,
                ReportName = reportNames[random.Next(reportNames.Length)],
                ReportType = reportTypes[random.Next(reportTypes.Length)],
                DateFrom = dateFrom,
                DateTo = dateFrom.AddDays(random.Next(7, 90)),
                RequestedBy = requestedByUsers[random.Next(requestedByUsers.Length)]
            };

            var jobId = await publishEndpoint.SubmitJob<GenerateReport>(message, CancellationToken.None);

            response.Jobs.Add(new BulkJobResult
            {
                JobId = jobId,
                ReportId = reportId,
                ReportName = message.ReportName,
                ReportType = message.ReportType
            });
        }

        Log.Information("Bulk report jobs submitted: Count={Count}", request.Count);

        return Results.Ok(response);
    })
    .WithName("BulkSubmitReports")
    .Produces<BulkSubmitReportResponse>();

    // DELETE /api/reports/{jobId} - Cancel a job
    app.MapDelete("/api/reports/{jobId:guid}", async (
        Guid jobId,
        IPublishEndpoint publishEndpoint) =>
    {
        await publishEndpoint.CancelJob(jobId, "User Request");

        Log.Information("Job cancelled: JobId={JobId}", jobId);

        return Results.Ok(new { Message = "Job cancellation requested", JobId = jobId });
    })
    .WithName("CancelJob")
    .Produces(200);

    // POST /api/reports/{jobId}/retry - Retry a job
    app.MapPost("/api/reports/{jobId:guid}/retry", async (
        Guid jobId,
        IPublishEndpoint publishEndpoint) =>
    {
        await publishEndpoint.RetryJob(jobId);

        Log.Information("Job retry requested: JobId={JobId}", jobId);

        return Results.Ok(new { Message = "Job retry requested", JobId = jobId });
    })
    .WithName("RetryJob")
    .Produces(200);

    // GET /api/reports - List available endpoints (health check)
    app.MapGet("/api/reports", () =>
    {
        return Results.Ok(new
        {
            Message = "MassTransit Durable Job Demo API",
            Endpoints = new[]
            {
                "POST /api/reports - Submit a report generation job",
                "POST /api/reports/bulk - Submit multiple report generation jobs",
                "GET /api/reports/{jobId} - Get job status by Job ID",
                "GET /api/reports/{jobId}/data - Get raw job data from JobSaga table",
                "DELETE /api/reports/{jobId} - Cancel a job by Job ID",
                "POST /api/reports/{jobId}/retry - Retry a job by Job ID"
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
