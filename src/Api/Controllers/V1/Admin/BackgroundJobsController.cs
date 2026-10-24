using Api.BackgroundServices;
using Api.Contracts.Models.V1;
using Api.Contracts.Models.V1.BackgroundJobs;
using Api.Extensions;
using Asp.Versioning;
using Domain;
using Domain.BackgroundJobs;
using Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.V1.Admin;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = Domain.Shared.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/background-jobs")]
public class BackgroundJobsController(
    IBackgroundJobRepository backgroundJobRepository,
    IBackgroundJobTrigger backgroundJobTrigger,
    IBackgroundServiceStatRepository backgroundServiceStatRepository,
    IDataProtector dataProtector) : ControllerBase
{
    readonly IBackgroundJobRepository _backgroundJobRepository = backgroundJobRepository;
    readonly IBackgroundJobTrigger _backgroundJobTrigger = backgroundJobTrigger;
    readonly IBackgroundServiceStatRepository _backgroundServiceStatRepository = backgroundServiceStatRepository;
    readonly IDataProtector _dataProtector = dataProtector;

    /// <summary>
    /// Retrieves a paginated list of background jobs for administrative grids.
    /// </summary>
    /// <param name="filter">Filtering criteria (Status, Type, etc.).</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedListModel<BackgroundJobModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedListModel<BackgroundJobModel>>> Index(
        [FromQuery] GetBackgroundJobFilterModel? filter,
        [FromQuery] int pageNumber = Domain.Shared.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var backgroundJobs = await _backgroundJobRepository.Paginate(
            filter?.Dto,
            pageNumber,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(backgroundJobs.ToModelList(bj => bj.Model));
    }

    /// <summary>
    /// Streams background jobs using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for jobs.</param>
    /// <param name="next">The opaque cursor token from the previous response. Pass null for the first page</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorListModel<BackgroundJobModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListModel<BackgroundJobModel>>> Stream(
        [FromQuery] GetBackgroundJobFilterModel? filter,
        [FromQuery] [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Shared.Constants.MaxPageSize)
    {
        var cursor = next.ToCursor<BackgroundJobModel>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Contracts.Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var backgroundJobs = await _backgroundJobRepository.Stream(
            filter?.Dto,
            cursor,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(backgroundJobs.ToModelList(bj => bj.Model));
    }

    /// <summary>
    /// Get details of background job with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Background job id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When background job is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BackgroundJobModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackgroundJobModel>> View(Guid id)
    {
        var dto = await _backgroundJobRepository.Get(bj => bj.ExternalId == id);

        if (dto is null)
        {
            return NotFound();
        }

        return Ok(dto.Model);
    }

    /// <summary>
    /// Process background jobs manually
    /// </summary>
    /// <response code="202">When there are no errors</response>
    [HttpPost("process")]
    public IActionResult Process()
    {
        _backgroundJobTrigger.Trigger();

        return Accepted("Background job processing triggered manually.");
    }

    /// <summary>
    /// Re-activates background jobs that have reached their maximum retry limit or encountered a terminal failure.
    /// </summary>
    /// <param name="filter">Filtering criteria for jobs.</param>
    /// <response code="200">When there are no errors</response>
    [HttpPost("requeue")]
    public async Task<IActionResult> Requeue(
        [FromQuery] RequeueBackgroundJobFilterModel? filter)
    {
        var count = await _backgroundJobRepository.RequeueFailed(
            filter?.Dto,
            HttpContext.RequestAborted);

        if (count > 0)
        {
            _backgroundJobTrigger.Trigger();
        }

        return Ok($"Requeued jobs: {count}");
    }

    /// <summary>
    /// Get background job processing status
    /// </summary>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When background job is not found </response>
    [HttpGet("processing-status")]
    [ProducesResponseType(typeof(BackgroundServiceStatModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackgroundServiceStatModel>> ProcessingStatus()
    {
        var dto = await _backgroundServiceStatRepository.Get
            (bss => bss.Type == BackgroundServiceStatType.BackgroundJob);

        if (dto is null)
        {
            return NotFound("Background job processing has never run.");
        }

        return Ok(dto.Model);
    }
}
