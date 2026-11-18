using CampusFlow.Application.Services;
using CampusFlow.Contracts.Common;
using CampusFlow.Contracts.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Публичный справочник аудиторий и просмотр свободных слотов.</summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _rooms;

    public RoomsController(IRoomService rooms)
    {
        _rooms = rooms;
    }

    /// <summary>Список аудиторий с фильтрами по корпусу, вместимости и оборудованию.</summary>
    /// <param name="building">Корпус: A, B, C или D.</param>
    /// <param name="minCapacity">Минимальная вместимость, больше 0.</param>
    /// <param name="equipment">Оборудование через запятую; должны присутствовать все перечисленные.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <response code="200">Подходящие аудитории.</response>
    /// <response code="400">Неверный корпус, вместимость или оборудование.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> GetRooms(
        [FromQuery] string? building,
        [FromQuery] int? minCapacity,
        [FromQuery] string? equipment,
        CancellationToken cancellationToken)
    {
        return Ok(await _rooms.GetRoomsAsync(building, minCapacity, equipment, cancellationToken));
    }

    /// <summary>Карточка аудитории.</summary>
    /// <response code="200">Аудитория.</response>
    /// <response code="404">Аудитории нет.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> GetRoom(int id, CancellationToken cancellationToken)
    {
        return Ok(await _rooms.GetRoomAsync(id, cancellationToken));
    }

    /// <summary>Свободные слоты аудитории на дату: рабочий день 09:00–18:00, сетка 30 минут.</summary>
    /// <param name="id">Идентификатор аудитории.</param>
    /// <param name="date">Дата в формате YYYY-MM-DD, не в прошлом.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    /// <response code="200">Максимальные непрерывные свободные интервалы; пустой массив, если свободного времени нет.</response>
    /// <response code="400">Дата отсутствует, неверного формата или в прошлом.</response>
    /// <response code="404">Аудитории нет.</response>
    [HttpGet("{id:int}/free-slots")]
    [ProducesResponseType(typeof(FreeSlotsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FreeSlotsResponse>> GetFreeSlots(
        int id,
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        return Ok(await _rooms.GetFreeSlotsAsync(id, date, cancellationToken));
    }

    /// <summary>
    ///     Аудитории, свободные на заданный интервал, с фильтрами по корпусу, вместимости и оборудованию.
    /// </summary>
    /// <response code="200">Подходящие свободные аудитории.</response>
    /// <response code="400">Неверные дата, время или фильтры.</response>
    [HttpGet("free")]
    [ProducesResponseType(typeof(FreeRoomsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FreeRoomsResponse>> FindFreeRooms(
        [FromQuery] string? date,
        [FromQuery] string? startTime,
        [FromQuery] string? endTime,
        [FromQuery] string? building,
        [FromQuery] int? minCapacity,
        [FromQuery] string? equipment,
        CancellationToken cancellationToken)
    {
        return Ok(await _rooms.FindFreeRoomsAsync(date, startTime, endTime, building, minCapacity, equipment,
            cancellationToken));
    }
}