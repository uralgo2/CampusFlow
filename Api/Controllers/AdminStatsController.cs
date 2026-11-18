using CampusFlow.Application.Services;
using CampusFlow.Contracts.Admin;
using CampusFlow.Contracts.Common;
using CampusFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Статистика загрузки — только для администратора.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/stats")]
[Produces("application/json")]
public sealed class AdminStatsController : ControllerBase
{
    private readonly IStatsService _stats;

    public AdminStatsController(IStatsService stats)
    {
        _stats = stats;
    }

    /// <summary>Загрузка по дням, корпусам и аудиториям. По умолчанию — 7 дней, начиная с сегодня.</summary>
    /// <param name="from">Начало периода, YYYY-MM-DD.</param>
    /// <param name="to">Конец периода включительно, YYYY-MM-DD.</param>
    /// <param name="building">Корпус: A, B, C или D.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <response code="200">Сводка загрузки.</response>
    /// <response code="400">Неверные даты или корпус.</response>
    /// <response code="403">Пользователь не администратор.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AdminStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminStatsResponse>> Get(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? building,
        CancellationToken cancellationToken)
    {
        return Ok(await _stats.GetStatsAsync(from, to, building, cancellationToken));
    }
}