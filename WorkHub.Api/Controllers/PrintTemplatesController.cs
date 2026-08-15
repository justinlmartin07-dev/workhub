using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkHub.Api.DTOs.Responses;

namespace WorkHub.Api.Controllers;

// Serves the print templates from Templates/ so document layout can be changed
// with an API deploy instead of shipping a client update. The client caches the
// last response and falls back to its embedded copies when offline.
[ApiController]
[Route("v1/print-templates")]
[Authorize]
public class PrintTemplatesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public PrintTemplatesController(IWebHostEnvironment env) => _env = env;

    [HttpGet]
    public async Task<IActionResult> GetTemplates()
    {
        var dir = Path.Combine(_env.ContentRootPath, "Templates");
        return Ok(new PrintTemplatesResponse
        {
            JobSummary = await ReadOrNullAsync(Path.Combine(dir, "job-summary.html")),
            CustomerSummary = await ReadOrNullAsync(Path.Combine(dir, "customer-summary.html")),
        });
    }

    private static async Task<string?> ReadOrNullAsync(string path)
        => System.IO.File.Exists(path) ? await System.IO.File.ReadAllTextAsync(path) : null;
}
