using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace WorkHub.Api.Controllers;

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
