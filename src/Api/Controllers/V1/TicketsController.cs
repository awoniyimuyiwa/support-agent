using Api.Attributes;
using Api.Contracts.Models.V1;
using Api.Extensions;
using Application.Contracts;
using Asp.Versioning;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/tickets")]
public class TicketsController(
    IDataProtector dataProtector,
    ITicketRepository ticketRepository,
    ITicketService ticketService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Streams ticket deltas using cursor-based pagination.
    /// </summary>
    /// <param name="next">The opaque encrypted cursor token from the previous response. Pass null for the first page.</param>
    /// <param name="pageSize">The number of records per page chunk slice.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream-deltas")]
    [ProducesResponseType(typeof(DeltaModel<TicketModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeltaModel<TicketModel>>> StreamDeltas(
        [FromQuery][MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToDeltaCursor<TicketModel>(dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var until = timeProvider.GetUtcNow();

        var cursorList = await ticketRepository.StreamDeltas(
            cursor,
            until,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(new DeltaModel<TicketModel>(
            [.. cursorList.Items.Select(t => t.Model)],
            cursorList.Next,
            until));
    }

    /// <summary>
    /// Synchronizes an offline batch of tickets with the server using metadata-driven conflict resolution.
    /// </summary>
    /// <param name="request">The encapsulated read-only batch array of offline modifications and tracking timestamps.</param>
    /// <response code="200">When there are no errors (there could still be conflicts to resolve)</response>
    /// <response code="401">When user is not authenticated.</response>
    /// <response code="409">When there are conflicts during synchronization.</response>
    [Idempotent]
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncResultModel<TicketModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResultModel<TicketModel>>> Sync(
        [FromBody] TicketSyncModel request)
    {
        // Any thrown exception bubbles up to the global exception handler
        var conflicts = await ticketService.Sync(   
            request.Dto,   
            HttpContext.RequestAborted);

        return Ok(new SyncResultModel<TicketModel>([.. conflicts.Select(t => t.Model)]));
    }
}

