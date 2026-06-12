using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/address")]
[Authorize]
[EnableRateLimiting("thirdparty")]
public class AddressController : ControllerBase
{
    private readonly AddressService _addressService;

    public AddressController(AddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete(
        [FromQuery] string q,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null,
        [FromQuery] int? radius = null,
        [FromQuery] string? session = null)
    {
        if (!_addressService.IsConfigured)
            return Ok(new List<AddressSuggestion>());

        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return Ok(new List<AddressSuggestion>());

        (double Lat, double Lng, double RadiusMeters)? bias = null;
        if (lat.HasValue && lng.HasValue)
            bias = (lat.Value, lng.Value, radius ?? 50_000);

        var suggestions = await _addressService.AutocompleteAsync(q, bias, session);
        return Ok(suggestions);
    }

    [HttpGet("details/{placeId}")]
    public async Task<IActionResult> Details(string placeId, [FromQuery] string? session = null)
    {
        if (!_addressService.IsConfigured)
            return NotFound();

        var details = await _addressService.GetPlaceDetailsAsync(placeId, session);
        if (details == null)
            return NotFound();

        return Ok(details);
    }
}
