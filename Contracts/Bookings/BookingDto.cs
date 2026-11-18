namespace CampusFlow.Contracts.Bookings;

/// <summary>Бронь в ответах API.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="RoomId">Идентификатор аудитории.</param>
/// <param name="RoomName">Название аудитории.</param>
/// <param name="UserId">Идентификатор автора брони.</param>
/// <param name="UserDisplayName">Имя автора брони.</param>
/// <param name="Date">Дата, <c>YYYY-MM-DD</c>.</param>
/// <param name="StartTime">Начало, <c>HH:MM</c>.</param>
/// <param name="EndTime">Окончание, <c>HH:MM</c>.</param>
/// <param name="Status">Статус: <c>active</c> или <c>cancelled</c>.</param>
/// <param name="Comment">Комментарий или <c>null</c>.</param>
public sealed record BookingDto(
    int Id,
    int RoomId,
    string RoomName,
    int UserId,
    string UserDisplayName,
    string Date,
    string StartTime,
    string EndTime,
    string Status,
    string? Comment);