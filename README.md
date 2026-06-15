# MassTransit Durable Job Demos

A collection of demo applications showcasing **MassTransit's Job Service** for implementing durable, long-running background jobs with full lifecycle management (submit, track, cancel, retry) exposed via REST APIs.

## Projects

| Project | Description | Key Feature |
|---|---|---|
| [MassTransitDurableJobDemo](MassTransitDurableJobDemo/) | Base demo with a 5-stage report generation pipeline | Progress tracking, cancellation, resumability |
| [JobDistributionStrategyDemo](JobDistributionStrategyDemo/) | Extended demo with custom job distribution | Group-based job sequencing and concurrency control |

## Technologies

- .NET 10.0 (ASP.NET Core Minimal API)
- MassTransit 8.5.10 (Job Service, Saga State Machines)
- RabbitMQ (message broker)
- SQLite (default) or MongoDB (alternative) for saga persistence
- Serilog (structured logging)
- Scalar (API documentation UI)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **RabbitMQ** running on `localhost:5672` (required)

## Getting Started

```bash
# Build entire solution
dotnet build MassTransitDurableJobDemo.slnx

# Run the base demo
dotnet run --project MassTransitDurableJobDemo

# Run the distribution strategy demo
dotnet run --project JobDistributionStrategyDemo
```

Each project's API documentation UI opens automatically at `https://localhost:5066/scalar`.

## Shared Configuration

Both projects read from `appsettings.json` with the same settings:

| Setting | Default | Description |
|---|---|---|
| `PersistenceProvider` | `"sqlite"` | Persistence backend (`"sqlite"` or `"mongodb"`) |
| `ConnectionStrings:DefaultConnection` | `"Data Source=jobs.db"` | SQLite connection string |
| `ConnectionStrings:MongoDb` | `"mongodb://localhost:27017"` | MongoDB connection string |
| `RabbitMQ:Host` | `"localhost"` | RabbitMQ hostname |
| `RabbitMQ:Username` | `"guest"` | RabbitMQ username |
| `RabbitMQ:Password` | `"guest"` | RabbitMQ password |

## Solution Structure

```
MassTransitDurableJobDemo/
├── MassTransitDurableJobDemo.slnx
├── MassTransitDurableJobDemo/          # Base demo project
│   ├── Program.cs
│   ├── Consumers/
│   ├── Contracts/
│   └── appsettings.json
├── JobDistributionStrategyDemo/        # Distribution strategy demo
│   ├── Program.cs
│   ├── Consumers/
│   ├── Contracts/
│   ├── DistributionStrategies/
│   └── appsettings.json
└── Collection/                         # Bruno API test collection
```

## API Testing

A pre-configured [Bruno](https://www.usebruno.com/) API collection is included in the `Collection/` directory with requests for all endpoints and environment configuration.
