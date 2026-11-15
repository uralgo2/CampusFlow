using CampusFlow.Application.Parsing;
using CampusFlow.Contracts.Auth;
using CampusFlow.Contracts.Bookings;
using CampusFlow.Contracts.Rooms;
using CampusFlow.Domain;

namespace CampusFlow.Application.Mapping;

/// <summary>Перевод доменных сущностей в DTO контракта.</summary>
public static class DtoMapper
{
    /// <summary>Пользователь.</summary>
    public static UserDto ToDto(User user) =>
        new(user.Id, user.Login, user.DisplayName, UserRoles.ToCode(user.Role));

    /// <summary>Аудитория.</summary>
    public static RoomDto ToDto(Room room) =>
        new(
            room.Id,
            room.Name,
            room.Building,
            room.Floor,
            room.Capacity,
            room.Equipment.Select(EquipmentCatalog.ToCode).ToList());

    /// <summary>Бронь. Требует заполненных навигационных свойств <c>Room</c>.</summary>
    public static BookingDto ToDto(Booking booking) =>
        new(
            booking.Id,
            booking.RoomId,
            booking.Room?.Name ?? string.Empty,
            booking.UserId,
            booking.User?.DisplayName ?? string.Empty,
            ApiParsing.FormatDate(booking.Date),
            ApiParsing.FormatTime(booking.StartTime),
            ApiParsing.FormatTime(booking.EndTime),
            BookingStatuses.ToCode(booking.Status),
            booking.Comment);

    /// <summary>Свободный интервал.</summary>
    public static FreeSlotDto ToDto((TimeOnly Start, TimeOnly End) slot) =>
        new(ApiParsing.FormatTime(slot.Start), ApiParsing.FormatTime(slot.End));
}
