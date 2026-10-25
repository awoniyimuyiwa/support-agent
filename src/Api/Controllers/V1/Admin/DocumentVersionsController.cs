using Api.Attributes;
using Api.Contracts.Models.V1;
using Api.Extensions;
using Api.Options;
using Application.Contracts;
using Asp.Versioning;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
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
    IOptions<FileStorageOptions> fileStorageOptions,
    IFileStorageService fileStorageService,
    TimeProvider timeProvider) : ControllerBase
{
    readonly FileStorageOptions _fileStorageOptions = fileStorageOptions.Value;

    /// <summary>
    /// Generates a collection of pre-signed cloud storage upload destinations for a batch of new files.
    /// </summary>
    /// <remarks>
    /// This endpoint validates requested MIME types and generates cryptographically signed destination URLs.
    /// 
    /// Clients should validate that individual file sizes do not exceed 
    /// the configured maximum threshold (default is 4MB) before initiating the upload sequence.
    /// 
    /// Files uploaded via these URLs are asynchronously validated by a background worker. 
    /// Any files exceeding the size limit or violating structural magic-byte signatures will be automatically deleted.
    /// </remarks>
    /// <param name="input">The payload containing the list of item identifiers and intended MIME types to authorize.</param>
    /// <response code="200">Returned when the batch allocation is successful, yielding a dictionary mapped to the user's ItemIds.</response>
    /// <response code="400">Returned when validation constraints fail, such as duplicate IDs or unsupported file formats.</response>
    /// <response code="401">Returned when the user is not authenticated.</response>
    /// <response code="403">Returned when the user is not authorized to access this resource.</response>
    [HttpPost("create-upload-urls")]
    [Idempotent]
    [ProducesResponseType(typeof(UploadUrlsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadUrlsResponse>> CreateUploadUrls(CreateUploadUrlRequest input)
    {
        if (input.Items.Count == 0)
        {
            return Ok(new UploadUrlsResponse(ReadOnlyDictionary<Guid, string>.Empty));
        }

        var itemIdDtoMap = input.Items.ToDictionary(
            item => item.ItemId,
            item => new FileDto(Guid.NewGuid(), item.FileType));

        var dtos = itemIdDtoMap.Values.ToList();
        var dtoIdUrlMap = fileStorageService.GenerateUploadUrls(
            dtos,
            _fileStorageOptions.DocumentsContainerName);

        // Link client's ItemId to url
        var itemIdUrlMap = itemIdDtoMap.ToDictionary(
            kvp => kvp.Key,                             
            kvp => dtoIdUrlMap[kvp.Value.Id]);

        return Ok(new UploadUrlsResponse(itemIdUrlMap));
    }

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

