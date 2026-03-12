using Aggregator.Aggregators.Abstractions;
using Aggregator.Core.Dtos;
using Aggregator.Core.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aggregator.Aggregators.Dynamic;

public class DynamicAggregator(
    AggregatorConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger<DynamicAggregator> logger) : BaseAggregator
{
    private static readonly Action<ILogger, string, Exception?> LogFetchError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "FetchError"),
            "Error fetching from dynamic aggregator '{Name}'");

    private static readonly Action<ILogger, string, string, Exception?> LogSkippedItem =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(2, "SkippedItem"),
            "Skipping item from aggregator '{Name}': {Reason}");

    public override string Name => config.Name;
    public override string DisplayName => config.DisplayName;

    public override async Task<IEnumerable<AggregatedNewsDto>> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            using var response =
                await httpClient.GetAsync(config.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(
                cancellationToken) ?? throw new InvalidOperationException("Empty response body");

            var items = ResolveItemsArray(document.RootElement);
            return MapItems(items);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            LogFetchError(logger, config.Name, ex);
            return [];
        }
    }

    // Walks the JSON looking for the first array element. Supports dot-separated
    // paths like "data.children" as well as a bare root array.
    private static JsonElement ResolveItemsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        // Depth-first search for the first array-valued property
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    return property.Value;
                }
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = ResolveItemsArray(property.Value);
                    if (nested.ValueKind == JsonValueKind.Array)
                    {
                        return nested;
                    }
                }
            }
        }

        return default;
    }

    private List<AggregatedNewsDto> MapItems(JsonElement items)
    {
        if (items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<AggregatedNewsDto>();

        foreach (var item in items.EnumerateArray())
        {
            // Items may be wrapped in a container object (e.g. Reddit's {kind, data})
            var data = item.ValueKind == JsonValueKind.Object
                ? UnwrapIfNeeded(item)
                : item;

            var title = GetStringValue(data, config.TitleField);
            var url = GetStringValue(data, config.UrlField);

            if (title is null)
            {
                LogSkippedItem(logger, config.Name, $"missing field '{config.TitleField}'", null);
                continue;
            }

            if (url is null)
            {
                LogSkippedItem(logger, config.Name, $"missing field '{config.UrlField}'", null);
                continue;
            }

            var publishedAt = ParseDateTime(data, config.PublishedAtField);
            var score = config.ScoreField is not null ? GetIntValue(data, config.ScoreField) : null;
            var commentCount = config.CommentCountField is not null
                ? GetIntValue(data, config.CommentCountField)
                : null;

            results.Add(MapToDto(title, url, publishedAt, score, commentCount));
        }

        return results;
    }

    // If the item is a wrapper object with a single nested object value, unwrap it.
    // This handles patterns like Reddit's { "kind": "t3", "data": { ... } }.
    private static JsonElement UnwrapIfNeeded(JsonElement item)
    {
        JsonElement? candidate = null;
        var objectCount = 0;

        foreach (var property in item.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                candidate = property.Value;
                objectCount++;
            }
        }

        return objectCount == 1 && candidate.HasValue ? candidate.Value : item;
    }

    // Resolves a dot-separated path like "score" or "stats.points" against an element.
    private static JsonElement? ResolvePath(JsonElement element, string path)
    {
        var current = element;

        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static string? GetStringValue(JsonElement element, string path)
    {
        var node = ResolvePath(element, path);
        return node?.ValueKind == JsonValueKind.String ? node.Value.GetString() : null;
    }

    private static int? GetIntValue(JsonElement element, string path)
    {
        var node = ResolvePath(element, path);
        if (node is null)
        {
            return null;
        }

        if (node.Value.ValueKind == JsonValueKind.Number && node.Value.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private static DateTime ParseDateTime(JsonElement element, string path)
    {
        var node = ResolvePath(element, path);
        if (node is null)
        {
            return DateTime.UtcNow;
        }

        // Unix timestamp (number)
        if (node.Value.ValueKind == JsonValueKind.Number
            && node.Value.TryGetInt64(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        // ISO 8601 string
        if (node.Value.ValueKind == JsonValueKind.String)
        {
            var raw = node.Value.GetString();
            if (raw is not null && DateTime.TryParse(
                raw,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return DateTime.UtcNow;
    }
}
