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

namespace Api.Controllers.V1.Admin;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = Domain.Shared.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/document-categories")]
public class DocumentCategoriesController(
    IDataProtector dataProtector,
    IDocumentCategoryRepository categoryRepository,
    IDocumentCategoryService categoryService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Streams document category deltas using cursor-based pagination.
    /// </summary>
    /// <param name="next">The opaque encrypted cursor token from the previous response. Pass null for the first page.</param>
    /// <param name="pageSize">The number of records per page chunk slice.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream-deltas")]
    [ProducesResponseType(typeof(DeltaResponse<DocumentCategorySyncItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeltaResponse<DocumentCategorySyncItem>>> StreamDeltas(
        [FromQuery][MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToDeltaCursor<DocumentCategorySyncItem>(dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var until = timeProvider.GetUtcNow();

        var cursorList = await categoryRepository.StreamDeltas(
            cursor,
            until,
            pageSize,
            HttpContext.RequestAborted);

        return base.Ok(new DeltaResponse<DocumentCategorySyncItem>(
            [.. cursorList.Items.Select(dc => dc.Model)],
            cursorList.Next,
            until));
    }

    /// <summary>
    /// Synchronizes an offline batch of document categories with the server.
    /// </summary>
    /// <param name="input">Batch of offline modifications.</param>
    /// <response code="200">When there are no errors (there could still be conflicts to resolve)</response>
    /// <response code="401">When user is not authenticated.</response>
    /// <response code="409">When there are conflicts during synchronization.</response>
    [HttpPost("sync")]
    [Idempotent]
    [ProducesResponseType(typeof(SyncResponse<DocumentCategorySyncItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResponse<DocumentCategorySyncItem>>> Sync(
        [FromBody] DocumentCategorySyncRequest input)
    {
        var conflicts = await categoryService.Sync(
            input.Dto,
            HttpContext.RequestAborted);

        return base.Ok(new SyncResponse<DocumentCategorySyncItem>([.. conflicts.Select(dc => dc.Model)]));
    }
}

