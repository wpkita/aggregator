using System.Net.Http.Json;
using System.Text.Json;
using Aggregator.Aggregators.Abstractions;
using Aggregator.Core.Dtos;
using Aggregator.Core.Entities;
using Microsoft.Extensions.Logging;

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
            using HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage response =
                await httpClient.GetAsync(config.Url, cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = await response.Content.ReadFromJsonAsync<JsonDocument>(
                cancellationToken) ?? throw new InvalidOperationException("Empty response body");

            JsonElement items = ResolveItemsArray(document.RootElement);
            return MapItems(items);
        }
        catch (Exception ex)
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
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    return property.Value;
                }
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    JsonElement nested = ResolveItemsArray(property.Value);
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

        foreach (JsonElement item in items.EnumerateArray())
        {
            // Items may be wrapped in a container object (e.g. Reddit's {kind, data})
            JsonElement data = item.ValueKind == JsonValueKind.Object
                ? UnwrapIfNeeded(item)
                : item;

            string? title = GetStringValue(data, config.TitleField);
            string? url = GetStringValue(data, config.UrlField);

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

            DateTime publishedAt = ParseDateTime(data, config.PublishedAtField);
            int? score = config.ScoreField is not null ? GetIntValue(data, config.ScoreField) : null;
            int? commentCount = config.CommentCountField is not null
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
        int objectCount = 0;

        foreach (JsonProperty property in item.EnumerateObject())
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
        JsonElement current = element;

        foreach (string segment in path.Split('.'))
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
        JsonElement? node = ResolvePath(element, path);
        return node?.ValueKind == JsonValueKind.String ? node.Value.GetString() : null;
    }

    private static int? GetIntValue(JsonElement element, string path)
    {
        JsonElement? node = ResolvePath(element, path);
        if (node is null)
        {
            return null;
        }

        if (node.Value.ValueKind == JsonValueKind.Number && node.Value.TryGetInt32(out int value))
        {
            return value;
        }

        return null;
    }

    private static DateTime ParseDateTime(JsonElement element, string path)
    {
        JsonElement? node = ResolvePath(element, path);
        if (node is null)
        {
            return DateTime.UtcNow;
        }

        // Unix timestamp (number)
        if (node.Value.ValueKind == JsonValueKind.Number
            && node.Value.TryGetInt64(out long unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        // ISO 8601 string
        if (node.Value.ValueKind == JsonValueKind.String)
        {
            string? raw = node.Value.GetString();
            if (raw is not null && DateTime.TryParse(
                raw,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return DateTime.UtcNow;
    }
}
