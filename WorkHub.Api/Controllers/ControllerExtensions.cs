using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace WorkHub.Api.Controllers;

// Thrown when an authenticated request is somehow missing a usable user id claim.
// Mapped to 401 by the global exception handler — kept distinct from the framework's
// UnauthorizedAccessException so unrelated I/O permission errors aren't masked as 401.
public class MissingUserClaimException : Exception
{
    public MissingUserClaimException(string message) : base(message) { }
}

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var id))
            throw new MissingUserClaimException("Missing or invalid user identifier claim.");
        return id;
    }
}
