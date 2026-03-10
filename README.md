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
|-------------------------------|------------------------|-------------------------------------------------------------|
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

## Adding a new aggregator

1. **Create a new project** under `src/`:

   ```sh
   mkdir src/Aggregator.Aggregators.MySource
   ```

   Create `src/Aggregator.Aggregators.MySource/Aggregator.Aggregators.MySource.csproj`:

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Library</OutputType>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\Aggregator.Aggregators.Abstractions\Aggregator.Aggregators.Abstractions.csproj" />
       <ProjectReference Include="..\Aggregator.Core\Aggregator.Core.csproj" />
     </ItemGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
       <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
       <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
     </ItemGroup>
   </Project>
   ```

2. **Implement the aggregator** by extending `BaseAggregator`:

   ```csharp
   using Aggregator.Aggregators.Abstractions;
   using Aggregator.Core.Dtos;
   using Microsoft.Extensions.Logging;

   namespace Aggregator.Aggregators.MySource;

   [AggregatorPlugin("mysource", "My Source")]
   public class MySourceAggregator(
       HttpClient httpClient,
       ILogger<MySourceAggregator> logger) : BaseAggregator
   {
       public override string Name => "mysource";
       public override string DisplayName => "My Source";

       public override async Task<IEnumerable<AggregatedNewsDto>> FetchAsync(
           CancellationToken cancellationToken = default)
       {
           // Fetch from your source, map to DTOs using MapToDto()
           return [];
       }
   }
   ```

3. **Register with DI** by adding a `ServiceCollectionExtensions.cs`:

   ```csharp
   using Aggregator.Core.Infrastructure;
   using Microsoft.Extensions.DependencyInjection;

   namespace Aggregator.Aggregators.MySource;

   public static class ServiceCollectionExtensions
   {
       public static IServiceCollection AddMySourceAggregator(
           this IServiceCollection services)
       {
           services.AddHttpClient<MySourceAggregator>();
           services.AddScoped<INewsAggregator, MySourceAggregator>();
           return services;
       }
   }
   ```

4. **Add the project reference** to the entry points you want it active in (`Aggregator.Console.csproj` and/or `Aggregator.BackgroundService.csproj`):

   ```xml
   <ProjectReference Include="..\Aggregator.Aggregators.MySource\Aggregator.Aggregators.MySource.csproj" />
   ```

5. **Call the registration method** in `Program.cs` for each entry point:

   ```csharp
   services.AddMySourceAggregator();
   ```

6. **Add the project to the solution**:

   ```xml
   <!-- In Aggregator.slnx, inside the /src/ folder -->
   <Project Path="src/Aggregator.Aggregators.MySource/Aggregator.Aggregators.MySource.csproj" />
   ```

No other code changes are needed. The polling service discovers all registered `INewsAggregator` implementations automatically.
