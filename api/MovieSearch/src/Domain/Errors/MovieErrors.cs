namespace Domain.Errors;

public static class MovieErrors
{
    // Lookup / retrieval
    public static Error NotFound(string id) => new(
        "Movie.NotFound", $"Movie with id '{id}' does not exist");
    public static Error TitleNotFound(string title) => new(
        "Movie.TitleNotFound", $"No movie found matching title '{title}'");

    // Search request validation (GET /api/v1/movies/search)
    public static Error EmptyQuery() => new(
        "Movie.EmptyQuery", "The search query 'q' is required and cannot be empty");
    public static Error TopKOutOfRange(int topK, int max) => new(
        "Movie.TopKOutOfRange", $"'top_k' must be between 1 and {max}, but was {topK}");
    public static Error InvalidImdbRating(double value) => new(
        "Movie.InvalidImdbRating", $"'min_imdb_rating' must be between 0 and 10, but was {value}");
    public static Error InvalidMpaaRating(string value) => new(
        "Movie.InvalidMpaaRating", $"'mpaa_rating' value '{value}' is not a recognised MPAA rating");
    public static Error InvalidDecade(int value) => new(
        "Movie.InvalidDecade", $"'decade' must be a valid four-digit decade (e.g. 1990), but was {value}");
    public static Error InvalidGenre(string value) => new(
        "Movie.InvalidGenre", $"'genre' value '{value}' is not a known genre");

    // Downstream dependencies (MCP server, embeddings, database)
    public static Error SearchFailed() => new(
        "Movie.SearchFailed", "An error occurred while performing the semantic search");
    public static Error McpServerUnavailable() => new(
        "Movie.McpServerUnavailable", "The MCP server is unavailable; search cannot be completed");
    public static Error EmbeddingFailed() => new(
        "Movie.EmbeddingFailed", "An error occurred while generating the query embedding");
    public static Error DatabaseUnavailable() => new(
        "Movie.DatabaseUnavailable", "The movie datastore is currently unavailable");

    // Statistics (GET /api/v1/stats)
    public static Error StatsUnavailable() => new(
        "Movie.StatsUnavailable", "An error occurred while retrieving dataset statistics");

    // Authentication & authorization
    public static Error Unauthorized() => new(
        "Movie.Unauthorized", "A valid bearer token is required to access this endpoint");
    public static Error InvalidCredentials() => new(
        "Movie.InvalidCredentials", "The supplied client credentials are invalid");
    public static Error TokenExpired() => new(
        "Movie.TokenExpired", "The bearer token has expired; request a new token from /auth/token");
    public static Error Forbidden() => new(
        "Movie.Forbidden", "The 'admin' role is required to access this endpoint");

    // Rate limiting (60 req/min per authenticated user)
    public static Error RateLimitExceeded() => new(
        "Movie.RateLimitExceeded", "Rate limit exceeded; a maximum of 60 requests per minute is allowed");

    public static Error RequestNull() => new(
        "Movie.RequestNull", "The request cannot be null");
}
