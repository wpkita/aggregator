# Aggregator

A pluggable .NET 10 news aggregator that polls remote sources (starting with Hacker News) and stores items in a local SQLite database.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```sh
dotnet build
```

## Test

```sh
dotnet test
```

## Database

The database is created automatically on first run. SQLite writes a `news.db` file in the working directory of whichever entry point you run. No manual setup is required.

The default connection string comes from `appsettings.json` in each entry point project. Override at runtime with environment variables:

| Variable                      | Applies to             | Description                                                 |
| ----------------------------- | ---------------------- | ----------------------------------------------------------- |
| `ConnectionStrings__Default`  | Both                   | SQLite connection string (e.g. `Data Source=/data/news.db`) |
| `Worker__PollIntervalSeconds` | BackgroundService only | Polling interval in seconds (default: 300)                  |

## Run the console app

Polls all registered aggregators once and exits.

```sh
dotnet run --project src/Aggregator.Console
```

## Run the background service

Polls all registered aggregators every 5 minutes until stopped.

```sh
dotnet run --project src/Aggregator.BackgroundService
```

Stop with `Ctrl+C`.

## Adding a New Aggregator

# Show help

```bash
aggy --help
```

### Add Reddit r/programming

```bash
aggy add https://www.reddit.com/r/programming/.json \
    -n reddit-programming \
    -d "Reddit r/programming" \
    -t title \
    -u url \
    -p created_utc \
    -s score \
    -c num_comments
```

### List all aggregators

`aggy list`

### Show top item from each aggregator (polls first)

`aggy top`

### Remove a dynamic aggregator

`aggy remove reddit-programming`

## Linting & Formatting

[`dotnet format`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) enforces
`.editorconfig` rules. `EnforceCodeStyleInBuild` is enabled, so `dotnet build` also fails on
style violations.

Verify without modifying files (for CI):

```sh
dotnet format --verify-no-changes
```

Auto-fix all issues:

```sh
dotnet format
```

Run individual checks:

```sh
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes
dotnet format analyzers --verify-no-changes
```
