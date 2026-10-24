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
[Route("v{version:apiVersion}/admin/documents")]
public class DocumentsController(
    IDataProtector dataProtector,
    IDocumentRepository documentRepository,
    IDocumentCategoryRepository categoryRepository,
    IDocumentService documentService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Streams document deltas using cursor-based pagination.
    /// </summary>
    /// <param name="next">The opaque encrypted cursor token from the previous response. Pass null for the first page.</param>
    /// <param name="pageSize">The number of records per page chunk slice.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream-deltas")]
    [ProducesResponseType(typeof(DeltaModel<DocumentModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeltaModel<DocumentModel>>> StreamDeltas(
        [FromQuery][MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToDeltaCursor<DocumentModel>(dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var until = timeProvider.GetUtcNow();

        var cursorList = await documentRepository.StreamDeltas(
            cursor,
            until,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(new DeltaModel<DocumentModel>(
            [.. cursorList.Items.Select(d => d.Model)],
            cursorList.Next,
            until));
    }

    /// <summary>
    /// Synchronizes an offline batch of documents with the server.
    /// </summary>
    /// <param name="input">Batch of offline modifications.</param>
    /// <response code="200">When there are no errors (there could still be conflicts to resolve)</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When user is not authenticated.</response>
    /// <response code="409">When there are conflicts during synchronization.</response>
    [HttpPost("sync")]
    [Idempotent]
    [ProducesResponseType(typeof(SyncResultModel<DocumentModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResultModel<DocumentModel>>> Sync(
        [FromBody] DocumentSyncModel input)
    {
        var categoryIds = input.Items
            .Select(x => x.CategoryId)
            .Distinct()
            .ToList();
        var categoryIdMap = await categoryRepository.MapIds(
            categoryIds,
            ignoreQueryFilters: false,
            HttpContext.RequestAborted);

        Validate(
            input.Items, 
            categoryIdMap);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var conflicts = await documentService.Sync(
            input.ToDto(categoryIdMap),
            HttpContext.RequestAborted);

        return Ok(new SyncResultModel<DocumentModel>([.. conflicts.Select(d => d.Model)]));
    }

    private void Validate(
        List<DocumentModel> items,
        IReadOnlyDictionary<Guid, long> categoryIdMap)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items.ElementAt(i);
            if (!categoryIdMap.ContainsKey(item.CategoryId))
            {
                var key = $"{nameof(DocumentSyncModel.Items)}[{i}].{nameof(DocumentModel.CategoryId)}";
                ModelState.AddModelError(key, Contracts.Constants.InvalidErrorMessage);
            }
        }
    }
}

