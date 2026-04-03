using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkHub.Api.Services;

namespace WorkHub.Api.Controllers;

[ApiController]
[Route("v1/address")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly AddressService _addressService;

    public AddressController(AddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete([FromQuery] string q)
    {
        if (!_addressService.IsConfigured)
            return Ok(new List<AddressSuggestion>());

        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return Ok(new List<AddressSuggestion>());

        var suggestions = await _addressService.AutocompleteAsync(q);
        return Ok(suggestions);
    }

    [HttpGet("details/{placeId}")]
    public async Task<IActionResult> Details(string placeId)
    {
        if (!_addressService.IsConfigured)
            return NotFound();

        var details = await _addressService.GetPlaceDetailsAsync(placeId);
        if (details == null)
            return NotFound();

        return Ok(details);
    }
}
