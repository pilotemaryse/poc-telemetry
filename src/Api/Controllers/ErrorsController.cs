using Microsoft.AspNetCore.Mvc;

namespace DockerCrudDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorsController : ControllerBase
{
    private readonly ILogger<ErrorsController> _logger;
    public ErrorsController(ILogger<ErrorsController> logger) => _logger = logger;

    [HttpGet("throw")]
    public IActionResult Throw()
    {
        _logger.LogError("Triggered manual exception for trace testing");
        throw new InvalidOperationException("Simulated error for trace testing");
    }

    [HttpGet("notfound")]
    public IActionResult NotFoundResponse()
    {
        _logger.LogWarning("Returning 404 for trace testing");
        return NotFound(new { message = "Simulated 404" });
    }

    [HttpGet("badrequest")]
    public IActionResult BadRequestResponse()
    {
        _logger.LogWarning("Returning 400 for trace testing");
        return BadRequest(new { message = "Simulated bad request" });
    }

    [HttpGet("slow")]
    public async Task<IActionResult> Slow(CancellationToken ct)
    {
        _logger.LogInformation("Starting slow request");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        _logger.LogInformation("Slow request done");
        return Ok(new { message = "Took 3s" });
    }
}
