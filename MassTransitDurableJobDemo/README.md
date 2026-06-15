# MassTransit Durable Job Demo

A demo application showcasing **MassTransit's Job Service** for implementing durable, long-running background jobs with full lifecycle management (submit, track, cancel, retry) exposed via a REST API.

Simulates a **multi-stage report generation pipeline** with progress tracking, state persistence, and graceful cancellation/resume support.

## Technologies

- .NET 10.0 (ASP.NET Core Minimal API)
- MassTransit 8.5.10 (Job Service, Saga State Machines)
- RabbitMQ (message broker)
- SQLite (default) or MongoDB (alternative) for saga persistence
- Serilog (structured logging)
- Scalar (API documentation UI)

## Getting Started

```bash
# From solution root
dotnet run --project MassTransitDurableJobDemo
```

The API documentation UI opens automatically at `https://localhost:5066/scalar`.

### Configuration

Edit `appsettings.json` to customize:

| Setting | Default | Description |
|---|---|---|
| `PersistenceProvider` | `"sqlite"` | Persistence backend (`"sqlite"` or `"mongodb"`) |
| `ConnectionStrings:DefaultConnection` | `"Data Source=jobs.db"` | SQLite connection string |
| `ConnectionStrings:MongoDb` | `"mongodb://localhost:27017"` | MongoDB connection string |
| `RabbitMQ:Host` | `"localhost"` | RabbitMQ hostname |
| `RabbitMQ:Username` | `"guest"` | RabbitMQ username |
| `RabbitMQ:Password` | `"guest"` | RabbitMQ password |

## API Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/reports` | Submit a report generation job |
| `GET` | `/api/reports/{jobId}` | Get job status with progress and duration |
| `GET` | `/api/reports/{jobId}/data` | Get raw saga data from persistence store |
| `POST` | `/api/reports/bulk` | Submit multiple report jobs |
| `DELETE` | `/api/reports/{jobId}` | Cancel a running job |
| `POST` | `/api/reports/{jobId}/retry` | Retry a cancelled/failed job |
| `GET` | `/api/reports` | List all available endpoints |

### Example: Submit a Report

```bash
curl -X POST https://localhost:5066/api/reports \
  -H "Content-Type: application/json" \
  -d '{
    "reportName": "Sales Report",
    "reportType": "Sales",
    "dateFrom": "2025-01-01T00:00:00Z",
    "dateTo": "2025-06-30T23:59:59Z",
    "requestedBy": "demo-user"
  }'
```

### Example: Check Job Status

```bash
curl https://localhost:5066/api/reports/{jobId}
```

## How It Works

### Job Pipeline (5 Stages)

The `GenerateReportJobConsumer` simulates a report generation pipeline:

| Stage | Progress | Description |
|---|---|---|
| 1 | 0% → 10% | Fetching data |
| 2 | 10% → 40% | Aggregating data (3 sub-steps) |
| 3 | 40% → 70% | Generating Excel file (3 sheets) |
| 4 | 70% → 90% | Uploading file |
| 5 | 90% → 100% | Finalizing |

### Key Features

- **Durable state tracking** — state is persisted after each stage, surviving process restarts
- **Progress reporting** — real-time progress updates via `SetJobProgress`
- **Resumability** — on retry, the job resumes from the last completed stage
- **Cancellation handling** — gracefully cancels and preserves state for later resume
- **Cancellation-responsive delays** — work is broken into small chunks that check for cancellation

### Architecture

```
[HTTP Client / Bruno / Scalar UI]
        |
        v
[ASP.NET Core Minimal API]  ← Program.cs (endpoints)
        |
        v
[MassTransit SubmitJob]  → publishes GenerateReport message
        |
        v
[RabbitMQ]  (message broker)
        |
        v
[MassTransit Job Service Saga]  (state machine orchestrating job lifecycle)
        |
        v
[GenerateReportJobConsumer]  ← 5-stage pipeline with progress reporting
        |
        v
[SQLite / MongoDB]  (saga state + job state persistence)
```

## Project Structure

```
MassTransitDurableJobDemo/
├── Program.cs                          # Application entry point and API endpoints
├── appsettings.json                    # Configuration
├── Consumers/
│   └── GenerateReportJobConsumer.cs    # Job consumer with 5-stage pipeline
├── Contracts/
│   ├── GenerateReport.cs              # Message contracts and API DTOs
│   └── ReportJobState.cs             # Persistent job state record
├── jobs.db                            # SQLite database (auto-created)
└── logs/                              # Serilog file logs
```
