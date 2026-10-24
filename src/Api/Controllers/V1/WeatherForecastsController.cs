using Api.Attributes;
using Api.Contracts.Models.V1;
using Api.Extensions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/weather-forecasts")]
public class WeatherForecastsController(
    IDataProtector dataProtector,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Streams weather forecast deltas using cursor-based pagination.
    /// </summary>
    /// <param name="next">The opaque encrypted cursor token from the previous response. Pass null for the first page.</param>
    /// <param name="pageSize">The number of records per page chunk slice.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream-deltas")]
    [ProducesResponseType(typeof(DeltaModel<WeatherForecastModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeltaModel<WeatherForecastModel>>> StreamDeltas(
        [FromQuery][MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        _ = pageSize;
        var cursor = next.ToDeltaCursor<WeatherForecastModel>(dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var until = timeProvider.GetUtcNow();

        // Todo: Get delta list from real weather repository when plugged in later
        return Ok(new DeltaModel<WeatherForecastModel>(
            Items: [],
            Next: null,
            Until: until));
    }

    /// <summary>
    /// Synchronizes an offline batch of weather forecasts with the server using metadata-driven conflict resolution.
    /// </summary>    
    /// <response code="200">When there are no errors (there could still be conflicts to resolve)</response>
    /// <response code="401">When user is not authenticated.</response>
    /// <response code="409">When there are conflicts during synchronization</response>
    [Idempotent]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResultModel<WeatherForecastModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResultModel<WeatherForecastModel>>> Sync(
        [FromBody] WeatherForecastSyncModel request)
    {
        // Any thrown exception bubbles up to the global exception handler
        // Sync and get conflicts from the service based on the 'request' parameter

        // Todo: Get true conflicts from real weather forecast service when plugged in later
        // Defaulting stub to mirror request payload array structure back as a clean loop response
        return Ok(new SyncResultModel<WeatherForecastModel>(
            Conflicts: request.Items
        ));
    }
}
