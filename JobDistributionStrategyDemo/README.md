# Job Distribution Strategy Demo

An extended demo showcasing **MassTransit's custom job distribution strategies** for controlling how jobs are assigned to worker instances. Builds on the base [MassTransitDurableJobDemo](../MassTransitDurableJobDemo/) with a **group-key based distribution strategy** that enforces sequential execution within groups and limits concurrent groups.

## Key Feature: Group Key Distribution Strategy

The `GroupKeyJobDistributionStrategy` provides:

- **Sequential execution within groups** — jobs with the same `GroupKey` run one at a time
- **Concurrent group limiting** — caps how many distinct groups can run simultaneously
- **Configurable via job type properties** — group key name and max concurrency are set at registration time
- **Fallback to default** — jobs without a `GroupKey` use MassTransit's default distribution

### How It Works

1. When a job is submitted with a `GroupKey`, the strategy checks if any job with that same key is already running
2. If a same-group job is active, the new job is blocked (no slot allocated)
3. If no same-group job is active, it checks whether the max concurrent groups limit has been reached
4. If under the limit, it delegates to the default strategy for instance selection
5. Jobs without a `GroupKey` bypass group logic entirely

## Technologies

- .NET 10.0 (ASP.NET Core Minimal API)
- MassTransit 8.5.10 (Job Service, Saga State Machines)
- Custom `IJobDistributionStrategy` implementation
- RabbitMQ (message broker)
- SQLite (default) or MongoDB (alternative) for saga persistence
- Serilog (structured logging)
- Scalar (API documentation UI)

## Getting Started

```bash
# From solution root
dotnet run --project JobDistributionStrategyDemo
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
| `POST` | `/api/reports` | Submit a report job (with optional `GroupKey`) |
| `GET` | `/api/reports/{jobId}` | Get job status with progress and duration |
| `GET` | `/api/reports/{jobId}/data` | Get raw saga data from persistence store |
| `POST` | `/api/reports/bulk` | Submit multiple jobs (with group key options) |
| `DELETE` | `/api/reports/{jobId}` | Cancel a running job |
| `POST` | `/api/reports/{jobId}/retry` | Retry a cancelled/failed job |
| `GET` | `/api/reports` | List all available endpoints |

### Example: Submit a Job with Group Key

```bash
curl -X POST https://localhost:5066/api/reports \
  -H "Content-Type: application/json" \
  -d '{
    "reportName": "Sales Report",
    "reportType": "Sales",
    "dateFrom": "2025-01-01T00:00:00Z",
    "dateTo": "2025-06-30T23:59:59Z",
    "requestedBy": "demo-user",
    "groupKey": "tenant-1"
  }'
```

### Example: Bulk Submit with Different Group Keys

```bash
curl -X POST https://localhost:5066/api/reports/bulk \
  -H "Content-Type: application/json" \
  -d '{
    "count": 10,
    "useDifferentGroupKeys": true
  }'
```

This submits 10 jobs distributed across multiple group keys (`tenant-1` through `tenant-5`), demonstrating how the strategy limits concurrent groups.

## Strategy Registration

The strategy is registered in `Program.cs`:

```csharp
builder.Services.TryAddJobDistributionStrategy<GroupKeyJobDistributionStrategy>();
```

Job type properties configure the group key behavior:

```csharp
options.SetJobTypeProperties(p =>
{
    p.Set(GroupKeyConstants.JobTypePropertyKey, GroupKeyConstants.PropertyName);
    p.Set(GroupKeyConstants.MaxConcurrentGroupsKey, GroupKeyConstants.DefaultMaxConcurrentGroups);
});
```

## Project Structure

```
JobDistributionStrategyDemo/
├── Program.cs                              # Application entry point and API endpoints
├── appsettings.json                        # Configuration
├── Consumers/
│   └── GenerateReportJobConsumer.cs        # Job consumer with 5-stage pipeline
├── Contracts/
│   ├── GenerateReport.cs                   # Message contracts (includes GroupKey)
│   └── ReportJobState.cs                   # Persistent job state record
├── DistributionStrategies/
│   ├── GroupKeyJobDistributionStrategy.cs  # Custom distribution strategy
│   └── GroupKeyConstants.cs               # Constants for group key configuration
├── jobs.db                                 # SQLite database (auto-created)
└── logs/                                   # Serilog file logs
```
