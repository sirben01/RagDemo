using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagDemo.Core.Interfaces;

namespace RagDemo.Core.Services;

public class EmbeddingOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "text-embedding-3-small";
}

public class EmbeddingService(
    IHttpClientFactory httpClientFactory,
    IOptions<EmbeddingOptions> options,
    ILogger<EmbeddingService> logger) : IEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private const int MaxRetries = 6;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync([text], cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        logger.LogDebug("Embedding {Count} texts", textList.Count);

        var client = httpClientFactory.CreateClient("openai");
        var request = new EmbedRequest(options.Value.Model, textList);

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var response = await client.PostAsJsonAsync("/v1/embeddings", request, JsonOpts, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var delay = ResolveRetryDelay(response, attempt);
                logger.LogWarning("OpenAI 429 (attempt {Attempt}/{Max}): {Body} — waiting {Delay:0.0}s",
                    attempt + 1, MaxRetries, body, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOpts, cancellationToken)
                ?? throw new InvalidOperationException("Empty response from OpenAI embeddings API");

            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();
        }

        throw new InvalidOperationException($"OpenAI embeddings API rate limit exceeded after {MaxRetries} retries.");
    }

    private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        // Prefer the standard Retry-After delta (seconds)
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta + TimeSpan.FromSeconds(1); // +1s buffer

        // OpenAI also sends retry-after as a plain integer string
        if (response.Headers.TryGetValues("retry-after", out var values) &&
            double.TryParse(values.FirstOrDefault(), out var seconds) && seconds > 0)
            return TimeSpan.FromSeconds(seconds + 1);

        // Fallback: exponential backoff starting at 20s (fits within 3 RPM free-tier window)
        return TimeSpan.FromSeconds(Math.Pow(2, attempt) * 10);
    }

    private record EmbedRequest(string Model, IEnumerable<string> Input);
    private record EmbedResponse(List<EmbedData> Data);
    private record EmbedData(int Index, float[] Embedding);
}
