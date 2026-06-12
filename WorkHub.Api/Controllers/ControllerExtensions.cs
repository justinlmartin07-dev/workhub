using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace WorkHub.Api.Controllers;

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var id))
            throw new UnauthorizedAccessException("Missing or invalid user identifier claim.");
        return id;
    }
}
