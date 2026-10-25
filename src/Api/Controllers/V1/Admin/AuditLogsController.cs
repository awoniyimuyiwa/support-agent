using Api.BackgroundServices;
using Api.Contracts.Models.V1;
using Api.Extensions;
using Asp.Versioning;
using Domain;
using Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.V1.Admin;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = Domain.Shared.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/audit-logs")]
public class AuditLogsController(
    IAuditLogRepository auditLogRepository,
    IBackgroundServiceStatRepository backgroundServiceStatRepository,
    IDataProtector dataProtector) : ControllerBase
{
    readonly IAuditLogRepository _auditLogRepository = auditLogRepository;
    readonly IBackgroundServiceStatRepository _backgroundServiceStatRepository = backgroundServiceStatRepository;
    readonly IDataProtector _dataProtector = dataProtector;

    /// <summary>
    /// Get a paged list of audit logs.
    /// </summary>
    /// <param name="filter">The filtering criteria for the audit logs.</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedListResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedListResponse<AuditLogResponse>>> Index(
        [FromQuery] AuditLogFilterRequest? filter,
        [FromQuery] int pageNumber = Domain.Shared.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var auditLogs = await _auditLogRepository.Paginate(
            filter?.Dto,
            pageNumber,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(auditLogs.ToModelList(al => al.Model));
    }

    /// <summary>
    /// Streams audit logs using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for audit logs.</param>
    /// <param name="next">The opaque cursor token from the previous response. Pass null for the first page</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorListResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListResponse<AuditLogResponse>>> Stream(
        [FromQuery] AuditLogFilterRequest? filter,
        [FromQuery] [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToCursor<AuditLogResponse>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var auditLogs = await _auditLogRepository.Stream(
            filter?.Dto,
            cursor,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(auditLogs.ToModelList(al => al.Model));
    }

    /// <summary>
    /// Get details of audit log with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Audit log id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When audit log is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FullAuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullAuditLogResponse>> View(Guid id)
    {
        var dto = await _auditLogRepository.Get(al => al.ExternalId == id);

        if (dto is null)
        {
            return NotFound();
        }

        return Ok(dto.Model);
    }

    /// <summary>
    /// Clean up audit logs manually
    /// </summary>
    /// <response code="202">When there are no errors</response>
    [HttpPost("cleanup")]
    public IActionResult Cleanup([FromServices] IAuditLogCleanupTrigger trigger)
    {
        trigger.Trigger();

        return Accepted("Audit log cleanup triggered manually.");
    }

    /// <summary>
    /// Get audit log clean up status
    /// </summary>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When audit log is not found </response>
    [HttpGet("cleanup-status")]
    [ProducesResponseType(typeof(BackgroundServiceStatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackgroundServiceStatResponse>> CleanupStatus()
    {
        var dto = await _backgroundServiceStatRepository.Get
            (bss => bss.Type == BackgroundServiceStatType.AuditLogCleanUp);

        if (dto is null)
        {
            return NotFound("Audit log cleanup has never run.");
        }

        return Ok(dto.Model);
    }
}
