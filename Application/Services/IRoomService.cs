using CampusFlow.Contracts.Rooms;

namespace CampusFlow.Application.Services;

/// <summary>Справочник аудиторий и свободные слоты.</summary>
public interface IRoomService
{
    /// <summary>Список аудиторий с фильтрами по корпусу, вместимости и оборудованию.</summary>
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(
        string? building,
        int? minCapacity,
        string? equipment,
        CancellationToken cancellationToken);

    /// <summary>Карточка аудитории; 404 ROOM_NOT_FOUND, если её нет.</summary>
    Task<RoomDto> GetRoomAsync(int roomId, CancellationToken cancellationToken);

    /// <summary>Максимальные непрерывные свободные интервалы аудитории на дату.</summary>
    Task<FreeSlotsResponse> GetFreeSlotsAsync(int roomId, string? date, CancellationToken cancellationToken);

    /// <summary>Аудитории, свободные на заданный интервал и подходящие под фильтры.</summary>
    Task<FreeRoomsResponse> FindFreeRoomsAsync(
        string? date,
        string? startTime,
        string? endTime,
        string? building,
        int? minCapacity,
        string? equipment,
        CancellationToken cancellationToken);

    /// <summary>Создание аудитории (администратор).</summary>
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest? request, CancellationToken cancellationToken);

    /// <summary>Полное обновление аудитории (администратор).</summary>
    Task<RoomDto> UpdateRoomAsync(int roomId, UpdateRoomRequest? request, CancellationToken cancellationToken);

    /// <summary>Удаление аудитории (администратор); 409, если есть действующие брони.</summary>
    Task DeleteRoomAsync(int roomId, CancellationToken cancellationToken);
}