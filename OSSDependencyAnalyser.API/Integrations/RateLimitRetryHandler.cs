namespace OSSDependencyAnalyzer.API.Integrations;

/// <summary>
/// Retries transient upstream failures (429, 408, 5xx, GitHub secondary rate limits) honouring
/// Retry-After / X-RateLimit-Reset, with exponential backoff otherwise.
/// </summary>
public sealed class RateLimitRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    private readonly ILogger<RateLimitRetryHandler> _logger;

    public RateLimitRetryHandler(ILogger<RateLimitRetryHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            using var retryRequest = CloneRequest(request, content);
            var response = await base.SendAsync(retryRequest, cancellationToken);
            if (attempt >= MaxRetries || !ShouldRetry(response))
                return response;

            var delay = GetRetryDelay(response, attempt);
            if (delay > MaxRetryDelay)
            {
                // e.g. GitHub's hourly quota: waiting is pointless, let the caller fall back.
                _logger.LogWarning("Upstream asked us to wait {Delay}; returning HTTP {StatusCode} to the caller instead",
                    delay, (int)response.StatusCode);
                return response;
            }

            _logger.LogWarning(
                "Received upstream HTTP {StatusCode}; retrying attempt {Attempt} of {MaxRetries} after {Delay}",
                (int)response.StatusCode,
                attempt + 1,
                MaxRetries,
                delay);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source, byte[]? content)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (content is not null)
        {
            clone.Content = new ByteArrayContent(content);
            foreach (var header in source.Content!.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.InternalServerError
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout)
            return true;

        if (response.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return false;
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
            && remaining.FirstOrDefault() == "0")
            return true;
        if (response.Headers.RetryAfter is not null)
            return true;
        return false;
    }

    internal static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        if (retryAfter?.Date is { } date)
            return date > DateTimeOffset.UtcNow ? date - DateTimeOffset.UtcNow : TimeSpan.Zero;

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
            && long.TryParse(resetValues.FirstOrDefault(), out var resetSeconds))
        {
            var reset = DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
            return reset > DateTimeOffset.UtcNow ? reset - DateTimeOffset.UtcNow : TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}
