using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RagDemo.Core.Interfaces;
using RagDemo.Core.Models;

namespace RagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(IRagPipelineService pipeline, ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    public async Task ChatAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("sessionId and question are required", cancellationToken);
            return;
        }

        if (!ValidateHistory(request.History, out var validationError))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(validationError, cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var fullReply = new StringBuilder();

        try
        {
            await foreach (var token in pipeline.ChatAsync(request.SessionId, request.Question, request.History, cancellationToken))
            {
                fullReply.Append(token);
                var json = JsonSerializer.Serialize(new { token });
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            var doneJson = JsonSerializer.Serialize(new { done = true, reply = fullReply.ToString() });
            await Response.WriteAsync($"data: {doneJson}\n\n", CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Chat cancelled for session {SessionId}", request.SessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat error for session {SessionId}", request.SessionId);
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {error}\n\n", Encoding.UTF8, CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }

    private static bool ValidateHistory(IReadOnlyList<ConversationMessage>? history, out string error)
    {
        if (history is null || history.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        foreach (var msg in history)
        {
            var role = msg.Role?.ToLowerInvariant();
            if (role is not "user" and not "assistant")
            {
                error = $"Invalid conversation role '{msg.Role}'. Must be 'user' or 'assistant'.";
                return false;
            }
        }

        if (string.Equals(history[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            error = "History must end with an 'assistant' message before appending a new user question.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public record ChatRequest(string SessionId, string Question, IReadOnlyList<ConversationMessage>? History = null);
}
