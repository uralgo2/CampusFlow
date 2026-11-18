using CampusFlow.Application.Services;
using CampusFlow.Contracts.Common;
using CampusFlow.Contracts.Rooms;
using CampusFlow.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusFlow.Api.Controllers;

/// <summary>Управление справочником аудиторий — только для администратора.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/rooms")]
[Produces("application/json")]
public sealed class AdminRoomsController : ControllerBase
{
    private readonly IRoomService _rooms;

    public AdminRoomsController(IRoomService rooms)
    {
        _rooms = rooms;
    }

    /// <summary>Создать аудиторию.</summary>
    /// <response code="201">Аудитория создана.</response>
    /// <response code="400">Ошибка валидации полей или название уже занято.</response>
    /// <response code="403">Пользователь не администратор.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RoomDto>> Create(
        [FromBody] CreateRoomRequest? request,
        CancellationToken cancellationToken)
    {
        var room = await _rooms.CreateRoomAsync(request, cancellationToken);
        return Created($"/api/rooms/{room.Id}", room);
    }

    /// <summary>Полностью обновить аудиторию: все поля обязательны.</summary>
    /// <response code="200">Обновлённая аудитория.</response>
    /// <response code="400">Ошибка валидации полей или название уже занято.</response>
    /// <response code="403">Пользователь не администратор.</response>
    /// <response code="404">Аудитории нет.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RoomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> Update(
        int id,
        [FromBody] UpdateRoomRequest? request,
        CancellationToken cancellationToken)
    {
        return Ok(await _rooms.UpdateRoomAsync(id, request, cancellationToken));
    }

    /// <summary>Удалить аудиторию. Аудиторию с действующими бронями удалить нельзя.</summary>
    /// <response code="204">Аудитория удалена.</response>
    /// <response code="403">Пользователь не администратор.</response>
    /// <response code="404">Аудитории нет.</response>
    /// <response code="409">У аудитории есть действующие брони.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _rooms.DeleteRoomAsync(id, cancellationToken);
        return NoContent();
    }
}