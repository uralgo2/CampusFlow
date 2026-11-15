using CampusFlow.Application.Services;
using CampusFlow.Contracts.Bookings;
using CampusFlow.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Брони текущего пользователя: создание, список, отмена.</summary>
[ApiController]
[Authorize]
[Route("api/bookings")]
[Produces("application/json")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>Создать бронь. Автор брони берётся из токена, поле userId в теле не принимается.</summary>
    /// <response code="201">Бронь создана.</response>
    /// <response code="400">Ошибка валидации даты, времени или обязательных полей.</response>
    /// <response code="401">Нет токена.</response>
    /// <response code="404">Аудитории нет.</response>
    /// <response code="409">Интервал уже занят действующей бронью.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(
        [FromBody] CreateBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, booking);
    }

    /// <summary>Свои брони; фильтр <paramref name="status"/> необязателен.</summary>
    /// <response code="200">Брони текущего пользователя, отсортированные по дате и времени.</response>
    /// <response code="400">Неверный статус.</response>
    /// <response code="401">Нет токена.</response>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _bookings.GetMyAsync(status, cancellationToken));

    /// <summary>Отменить свою бронь. Чужую бронь отменяет только администратор через админ-endpoint.</summary>
    /// <response code="200">Бронь отменена, слот снова свободен.</response>
    /// <response code="401">Нет токена.</response>
    /// <response code="403">Бронь принадлежит другому пользователю.</response>
    /// <response code="404">Брони нет.</response>
    /// <response code="409">Бронь уже отменена.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(CancelBookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelBookingResponse>> Cancel(int id, CancellationToken cancellationToken) =>
        Ok(await _bookings.CancelOwnAsync(id, cancellationToken));
}
