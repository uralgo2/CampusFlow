using System.Text;
using CampusFlow.Application.Services;
using CampusFlow.Contracts.Bookings;
using CampusFlow.Contracts.Common;
using CampusFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Все брони всех пользователей — только для администратора.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/bookings")]
[Produces("application/json")]
public sealed class AdminBookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public AdminBookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    /// <summary>Все брони с необязательными фильтрами по дате, аудитории, автору и статусу.</summary>
    /// <response code="200">Список броней.</response>
    /// <response code="403">Пользователь не администратор.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetAll(
        [FromQuery] string? date,
        [FromQuery] int? roomId,
        [FromQuery] int? userId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _bookings.GetForAdminAsync(date, roomId, userId, status, cancellationToken));
    }

    /// <summary>Экспорт броней в CSV с теми же фильтрами.</summary>
    /// <response code="200">CSV-файл.</response>
    /// <response code="403">Пользователь не администратор.</response>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        [FromQuery] string? date,
        [FromQuery] int? roomId,
        [FromQuery] int? userId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var csv = await _bookings.ExportCsvAsync(date, roomId, userId, status, cancellationToken);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.Content)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", csv.FileName);
    }

    /// <summary>Отменить любую бронь.</summary>
    /// <response code="200">Бронь отменена.</response>
    /// <response code="403">Пользователь не администратор.</response>
    /// <response code="404">Брони нет.</response>
    /// <response code="409">Бронь уже отменена.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(CancelBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelBookingResponse>> Cancel(int id, CancellationToken cancellationToken)
    {
        return Ok(await _bookings.CancelByAdminAsync(id, cancellationToken));
    }
}