using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RagDemo.Core.Interfaces;

namespace RagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(IRagPipelineService pipeline, ILogger<IngestController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost]
    public async Task IngestAsync([FromBody] IngestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url) || string.IsNullOrWhiteSpace(request.SessionId))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("url and sessionId are required", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var progress in pipeline.IngestAsync(request.Url, request.SessionId, request.MaxPages, cancellationToken))
            {
                var json = JsonSerializer.Serialize(progress, CamelCase);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Ingest cancelled for session {SessionId}", request.SessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingest error for session {SessionId}", request.SessionId);
            var error = JsonSerializer.Serialize(new { stage = "error", message = ex.Message, isComplete = true, error = ex.Message }, CamelCase);
            await Response.WriteAsync($"data: {error}\n\n", Encoding.UTF8, CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
    }

    public record IngestRequest(string Url, string SessionId, int MaxPages = 10);
}
