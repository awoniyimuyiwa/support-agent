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
[Route("v{version:apiVersion}/admin/document-versions")]
public class DocumentVersionsController(
    IDataProtector dataProtector,
    IDocumentVersionRepository versionRepository,
    IDocumentRepository documentRepository,
    IDocumentService documentService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Streams document version deltas using cursor-based pagination.
    /// </summary>
    /// <param name="next">The opaque encrypted cursor token from the previous response. Pass null for the first page.</param>
    /// <param name="pageSize">The number of records per page chunk slice.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream-deltas")]
    [ProducesResponseType(typeof(DeltaResponse<DocumentVersionSyncItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeltaResponse<DocumentVersionSyncItem>>> StreamDeltas(
        [FromQuery][MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToDeltaCursor<DocumentVersionSyncItem>(dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }
        
        var until = timeProvider.GetUtcNow();

        var cursorList = await versionRepository.StreamDeltas(
            cursor,
            until,
            pageSize,
            HttpContext.RequestAborted);

        return base.Ok(new DeltaResponse<DocumentVersionSyncItem>(
            [.. cursorList.Items.Select(dv => dv.Model)],
            cursorList.Next,
            until));
    }

    /// <summary>
    /// Synchronizes an offline batch of document versions with the server.
    /// </summary>
    /// <param name="input">Batch of offline modifications.</param>
    /// <response code="200">When there are no errors (there could still be conflicts to resolve)</response>
    /// <response code="401">When user is not authenticated.</response>
    /// <response code="409">When there are conflicts during synchronization.</response>
    [HttpPost("sync")]
    [Idempotent]
    [ProducesResponseType(typeof(SyncResponse<DocumentVersionSyncItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResponse<DocumentVersionSyncItem>>> Sync(
        [FromBody] DocumentVersionSyncRequest input)
    {
        var documentIds = input.Items
            .Select(i => i.DocumentId)
            .Distinct()
            .ToList();
        var documentIdMap = await documentRepository.MapIds(
            documentIds,
            ignoreQueryFilters: false,
            HttpContext.RequestAborted);

        Validate(
            input.Items, 
            documentIdMap);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var conflicts = await documentService.Sync(
            input.ToDto(documentIdMap),
            HttpContext.RequestAborted);

        return base.Ok(new SyncResponse<DocumentVersionSyncItem>([.. conflicts.Select(dv => dv.Model)]));
    }

    private void Validate(
        List<DocumentVersionSyncItem> items,
        IReadOnlyDictionary<Guid, long> documentIdMap)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items.ElementAt(i);
            if (!documentIdMap.ContainsKey(item.DocumentId))
            {
                var key = $"{nameof(DocumentVersionSyncRequest.Items)}[{i}].{nameof(DocumentVersionSyncItem.DocumentId)}";
                ModelState.AddModelError(key, Contracts.Constants.InvalidErrorMessage);
            }
        }
    }
}

