using Application.Services;
using Domain.Common;
using Domain.Errors;
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

    public Task<Result<IReadOnlyList<MovieCatalogItem>>> SearchAsync(
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

        return CallAsync(
            "search_movies_by_description",
            arguments,
            result =>
            {
                var hits = McpToolResults.Deserialize<List<McpMovieResult>>(result) ?? [];
                return Result<IReadOnlyList<MovieCatalogItem>>.Success(hits.Select(hit => hit.ToCatalogItem()).ToList());
            },
            MovieErrors.SearchFailed(),
            cancellationToken);
    }

    public Task<Result<MovieCatalogItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?> { ["movie_id"] = id.ToString() };

        return CallAsync(
            "get_movie_by_id",
            arguments,
            result =>
            {
                var movie = McpToolResults.Deserialize<McpMovieResult>(result)?.ToCatalogItem();
                return movie is null
                    ? Result<MovieCatalogItem>.Failure(MovieErrors.NotFound(id.ToString()))
                    : Result<MovieCatalogItem>.Success(movie);
            },
            MovieErrors.McpServerUnavailable(),
            cancellationToken);
    }

    public Task<Result<MovieCatalogItem>> GetByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?> { ["title"] = title };

        return CallAsync(
            "get_movie_by_title",
            arguments,
            result =>
            {
                var movie = McpToolResults.Deserialize<McpMovieResult>(result)?.ToCatalogItem();
                return movie is null
                    ? Result<MovieCatalogItem>.Failure(MovieErrors.TitleNotFound(title))
                    : Result<MovieCatalogItem>.Success(movie);
            },
            MovieErrors.McpServerUnavailable(),
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<MovieCatalogItem>>> GetSimilarAsync(
        Guid id,
        int topK,
        CancellationToken cancellationToken = default)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["movie_id"] = id.ToString(),
            ["top_k"] = topK,
        };

        try
        {
            var result = await CallToolAsync("get_similar_movies", arguments, cancellationToken);
            if (result.IsError == true)
            {
                // The tool signals an unknown movie with a "does not exist" error
                // (mapped to a NotFound result); anything else is a real failure.
                var message = McpToolResults.ErrorMessage(result);
                if (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<IReadOnlyList<MovieCatalogItem>>.Failure(MovieErrors.NotFound(id.ToString()));
                }

                logger.LogError("MCP tool 'get_similar_movies' failed: {Message}", message);
                return Result<IReadOnlyList<MovieCatalogItem>>.Failure(MovieErrors.McpServerUnavailable());
            }

            var hits = McpToolResults.Deserialize<List<McpMovieResult>>(result) ?? [];
            return Result<IReadOnlyList<MovieCatalogItem>>.Success(hits.Select(hit => hit.ToCatalogItem()).ToList());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MCP tool 'get_similar_movies' threw");
            return Result<IReadOnlyList<MovieCatalogItem>>.Failure(MovieErrors.McpServerUnavailable());
        }
    }

    public Task<Result<IReadOnlyList<string>>> GetGenresAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync(
            "list_genres",
            arguments: [],
            result => Result<IReadOnlyList<string>>.Success(McpToolResults.Deserialize<List<string>>(result) ?? []),
            MovieErrors.McpServerUnavailable(),
            cancellationToken);
    }

    public Task<Result<MovieStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync(
            "get_dataset_stats",
            arguments: [],
            result =>
            {
                var stats = McpToolResults.Deserialize<McpDatasetStats>(result);
                return stats is null
                    ? Result<MovieStatistics>.Failure(MovieErrors.StatsUnavailable())
                    : Result<MovieStatistics>.Success(stats.ToStatistics());
            },
            MovieErrors.StatsUnavailable(),
            cancellationToken);
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

    /// <summary>
    /// Calls a tool and projects a successful result via <paramref name="onSuccess"/>.
    /// A tool-level error maps to <paramref name="downstreamError"/>; a transport
    /// exception maps to <see cref="MovieErrors.McpServerUnavailable"/>. Never throws.
    /// </summary>
    private async Task<Result<T>> CallAsync<T>(
        string tool,
        Dictionary<string, object?> arguments,
        Func<CallToolResult, Result<T>> onSuccess,
        Error downstreamError,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await CallToolAsync(tool, arguments, cancellationToken);
            if (result.IsError == true)
            {
                logger.LogError("MCP tool '{Tool}' failed: {Message}", tool, McpToolResults.ErrorMessage(result));
                return Result<T>.Failure(downstreamError);
            }

            return onSuccess(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MCP tool '{Tool}' threw", tool);
            return Result<T>.Failure(MovieErrors.McpServerUnavailable());
        }
    }
}
