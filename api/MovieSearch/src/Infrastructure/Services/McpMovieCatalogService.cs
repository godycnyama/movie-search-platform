using Application.Services;
using Infrastructure.Contracts.Mcp;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Infrastructure.Services;

/// <summary>
/// MCP-backed implementation of <see cref="IMovieCatalogService"/>: every movie
/// read is a tool call against the platform's MCP server (README §9), which owns
/// query embedding and pgvector search — the API never touches the movies tables.
/// Registered as a singleton; the MCP session is created lazily and shared, and is
/// re-established once per call if the server restarted underneath it.
/// </summary>
public sealed class McpMovieCatalogService(
    IOptionsMonitor<McpSettings> settings,
    ILoggerFactory loggerFactory,
    ILogger<McpMovieCatalogService> logger) : IMovieCatalogService, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private McpClient? _client;

    public async Task<IReadOnlyList<MovieCatalogItem>> SearchAsync(
        string query,
        MovieSearchFilters filters,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["top_k"] = topK,
            ["genre_filter"] = filters.Genre,
            ["min_imdb_rating"] = filters.MinImdbRating,
            ["mpaa_rating"] = filters.MpaaRating,
            ["decade"] = filters.Decade,
        };

        var result = await CallToolAsync("search_movies_by_description", arguments, cancellationToken);
        EnsureSuccess("search_movies_by_description", result);

        var hits = McpToolResults.Deserialize<List<McpMovieResult>>(result) ?? [];
        return hits.Select(hit => hit.ToCatalogItem()).ToList();
    }

    public async Task<MovieCatalogItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?> { ["movie_id"] = id.ToString() };

        var result = await CallToolAsync("get_movie_by_id", arguments, cancellationToken);
        EnsureSuccess("get_movie_by_id", result);

        return McpToolResults.Deserialize<McpMovieResult>(result)?.ToCatalogItem();
    }

    public async Task<MovieCatalogItem?> GetByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?> { ["title"] = title };

        var result = await CallToolAsync("get_movie_by_title", arguments, cancellationToken);
        EnsureSuccess("get_movie_by_title", result);

        return McpToolResults.Deserialize<McpMovieResult>(result)?.ToCatalogItem();
    }

    public async Task<IReadOnlyList<MovieCatalogItem>?> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["movie_id"] = id.ToString(),
            ["top_k"] = topK,
        };

        var result = await CallToolAsync("get_similar_movies", arguments, cancellationToken);
        if (result.IsError == true)
        {
            // The tool signals an unknown movie with a "does not exist" error; the
            // endpoint maps the null to a 404. Anything else is a real failure.
            var message = McpToolResults.ErrorMessage(result);
            if (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw ToolFailure("get_similar_movies", message);
        }

        var hits = McpToolResults.Deserialize<List<McpMovieResult>>(result) ?? [];
        return hits.Select(hit => hit.ToCatalogItem()).ToList();
    }

    public async Task<IReadOnlyList<string>> GetGenresAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallToolAsync("list_genres", arguments: [], cancellationToken);
        EnsureSuccess("list_genres", result);

        return McpToolResults.Deserialize<List<string>>(result) ?? [];
    }

    public async Task<MovieStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var result = await CallToolAsync("get_dataset_stats", arguments: [], cancellationToken);
        EnsureSuccess("get_dataset_stats", result);

        var stats = McpToolResults.Deserialize<McpDatasetStats>(result)
            ?? throw ToolFailure("get_dataset_stats", "empty result payload");
        return stats.ToStatistics();
    }

    /// <summary>Round-trips an MCP ping; used by the API's /health endpoint.</summary>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);
        await client.PingAsync(cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is { } client)
        {
            _client = null;
            await client.DisposeAsync();
        }

        _connectLock.Dispose();
    }

    private async Task<CallToolResult> CallToolAsync(
        string tool,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var client = await GetClientAsync(cancellationToken);
        try
        {
            return await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
                                              or ObjectDisposedException or InvalidOperationException)
        {
            // The shared session died (MCP server restart, dropped SSE stream):
            // reconnect once and retry. JSON-RPC errors (McpException) are not
            // retried — the server answered, it just refused the call.
            logger.LogWarning(exception, "MCP session lost calling '{Tool}'; reconnecting", tool);
            await ResetClientAsync(client);

            client = await GetClientAsync(cancellationToken);
            return await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
        }
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is { } connected)
        {
            return connected;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is null)
            {
                var current = settings.CurrentValue;
                var serverUrl = current.ServerUrl.TrimEnd('/');
                var (mode, path) = string.Equals(current.Transport, "streamable-http", StringComparison.OrdinalIgnoreCase)
                    ? (HttpTransportMode.StreamableHttp, "/mcp")
                    : (HttpTransportMode.Sse, "/sse");

                var transport = new HttpClientTransport(new HttpClientTransportOptions
                {
                    Endpoint = new Uri(serverUrl + path),
                    TransportMode = mode,
                    Name = "movie-search-mcp",
                }, loggerFactory);

                _client = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
                logger.LogInformation("Connected to MCP server at {ServerUrl} ({Transport})", serverUrl, current.Transport);
            }

            return _client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task ResetClientAsync(McpClient failed)
    {
        await _connectLock.WaitAsync(CancellationToken.None);
        try
        {
            if (!ReferenceEquals(_client, failed))
            {
                return; // Another caller already reconnected.
            }

            _client = null;
        }
        finally
        {
            _connectLock.Release();
        }

        try
        {
            await failed.DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Disposing the failed MCP session threw; ignored");
        }
    }

    private static void EnsureSuccess(string tool, CallToolResult result)
    {
        if (result.IsError == true)
        {
            throw ToolFailure(tool, McpToolResults.ErrorMessage(result));
        }
    }

    private static InvalidOperationException ToolFailure(string tool, string message) =>
        new($"MCP tool '{tool}' failed: {message}");
}
