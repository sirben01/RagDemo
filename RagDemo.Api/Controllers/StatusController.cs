using Microsoft.AspNetCore.Mvc;
using RagDemo.Core.Interfaces;

namespace RagDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController(IQdrantService qdrantService) : ControllerBase
{
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetStatusAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest("sessionId is required");

        var stats = await qdrantService.GetSessionStatsAsync(sessionId, cancellationToken);
        return Ok(stats);
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return BadRequest("sessionId is required");

        await qdrantService.DeleteSessionAsync(sessionId, cancellationToken);
        return NoContent();
    }
}
