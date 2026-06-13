using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Core.Services;

public class AnthropicOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 4096;
}

public class AnthropicService(
    IHttpClientFactory httpClientFactory,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicService> logger) : IAnthropicService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string question,
        IReadOnlyList<SearchResult> context,
        IReadOnlyList<ConversationMessage>? history = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var client = httpClientFactory.CreateClient("anthropic");

        var messages = new List<object>();
        if (history is not null)
        {
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });
        }
        messages.Add(new { role = "user", content = question });

        var requestBody = new
        {
            model = options.Value.Model,
            max_tokens = options.Value.MaxTokens,
            stream = true,
            system = systemPrompt,
            messages
        };

        var requestJson = JsonSerializer.Serialize(requestBody, JsonOpts);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while (!cancellationToken.IsCancellationRequested &&
               (line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            StreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<StreamEvent>(data, JsonOpts);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse SSE event: {Data}", data);
                continue;
            }

            if (evt?.Type == "message_stop") break;

            if (evt?.Type == "content_block_delta" && evt.Delta?.Type == "text_delta")
                yield return evt.Delta.Text ?? "";
        }
    }

    private static string BuildSystemPrompt(IReadOnlyList<SearchResult> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant. Answer the user's question using only the context below.");
        sb.AppendLine("Chunks are ordered by relevance score (higher = more relevant).");
        sb.AppendLine("Cite the source URL when referencing specific information.");
        sb.AppendLine("If the context does not contain enough information to answer, say so clearly — do not guess.");
        sb.AppendLine();

        if (context.Count == 0)
        {
            sb.AppendLine("CONTEXT: No relevant content was found for this question.");
            return sb.ToString();
        }

        sb.AppendLine("CONTEXT:");
        for (var i = 0; i < context.Count; i++)
        {
            var r = context[i];
            sb.AppendLine($"[{i + 1}] Score: {r.Score:F2} — Source: {r.SourceUrl}");
            sb.AppendLine(r.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private record StreamEvent(string Type, StreamDelta? Delta);
    private record StreamDelta(string Type, string? Text);
}
