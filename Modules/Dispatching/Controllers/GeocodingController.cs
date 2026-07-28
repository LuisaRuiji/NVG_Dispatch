using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NVGInventory.Modules.Dispatching.Services;

namespace NVGInventory.Modules.Dispatching.Controllers;

[ApiController]
[Route("api/dispatch/geocode")]
[Authorize]
public sealed class GeocodingController : ControllerBase
{
    private readonly IGeocodingService _geocodingService;

    public GeocodingController(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    [HttpGet]
    public async Task<ActionResult<GeocodingResult>> Geocode([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameters 'q' is required.");
        }

        var result = await _geocodingService.GeocodeAddressAsync(q, cancellationToken);
        if (result == null)
        {
            return NotFound("Location could not be geocoded.");
        }

        return Ok(result);
    }
}
