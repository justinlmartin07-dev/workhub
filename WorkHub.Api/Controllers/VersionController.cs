using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkHub.Api.DTOs.Responses;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1")]
public class VersionController : ControllerBase
{
    private readonly IConfiguration _config;

    public VersionController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("version")]
    [AllowAnonymous]
    public IActionResult GetVersion()
    {
        return Ok(new VersionResponse
        {
            ApiVersion = "1.0.0",
            MinimumAppVersion = _config["MinimumAppVersion"] ?? "1.0.0",
        });
    }
}
